using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR;


namespace BaroqueUI
{
    public class TeleportAction : MonoBehaviour
    {
        [Header("Teleport beam parameters")]
        //public EControllerSelection controllerSelection;
        //public EControllerButton controllerButton;
        public float beamForwardVelocity;
        public float beamUpVelocity;
        public LayerMask traceLayerMask;
        public Color validArcColor, invalidArcColor;
        public Material teleportMaterial;
        public Transform invalidReticlePrefab, destinationReticlePrefab;
        public UnityEvent onTeleported;

        Valve.VR.InteractionSystem.TeleportArc arc;
        Transform invalid_reticle, destination_reticle;
        Vector3 destination_position;
        bool destination_valid;

        public void Reset()
        {
            beamForwardVelocity = 10f;
            beamUpVelocity = 3f;
            //controllerSelection = EControllerSelection.Either;
            //controllerButton = EControllerButton.Touchpad;
            traceLayerMask = 1 << LayerMask.NameToLayer("Default");
            teleportMaterial = Resources.Load<Material>("BaroqueUI/TeleportPointer");

            validArcColor = new Color(0.0f, 0.8f, 1.0f, 0.7f);
            invalidArcColor = new Color(0.8f, 0f, 0.3f, 0.7f);

            GameObject go = Resources.Load<GameObject>("BaroqueUI/Teleporting");
            invalidReticlePrefab = go == null ? null : go.transform.Find("InvalidReticle");
            destinationReticlePrefab = go == null ? null : go.transform.Find("DestinationReticle");
        }

        void Start()
        {
            arc = gameObject.AddComponent<Valve.VR.InteractionSystem.TeleportArc>();
            arc.traceLayerMask = traceLayerMask;
            arc.material = teleportMaterial;
            invalid_reticle = Instantiate<Transform>(invalidReticlePrefab);
            invalid_reticle.gameObject.SetActive(false);
            destination_reticle = Instantiate<Transform>(destinationReticlePrefab);
            destination_reticle.gameObject.SetActive(false);

            var gt = Controller.GlobalTracker(this);
            gt.SetPriority(-10);
            gt.onTouchPressDown += OnTouchPressDown;
            gt.onTouchPressDrag += OnTouchPressDrag;
            gt.onTouchPressUp += OnTouchPressUp;
        }

        void OnTouchPressDown(Controller controller)
        {
            arc.Show();
        }

        void OnTouchPressDrag(Controller controller)
        {
            bool saved = Physics.queriesHitTriggers;
            try
            {
                Physics.queriesHitTriggers = false;

                Transform tr = controller.transform;
                arc.SetArcData(tr.position, tr.TransformDirection(new Vector3(0, beamUpVelocity, beamForwardVelocity)), true, false);

                destination_valid = false;
                bool show_invalid = false;
                RaycastHit hitInfo;
                if (arc.DrawArc(out hitInfo))
                {
                    /* The teleport destination is accepted if we fit a capsule here.  More precisely:
                        * the capsule starts at ABOVE_GROUND above the hit point of the beam; on top
                        * of that we check the capsule.  The height of that capsule above around is thus
                        * from ABOVE_GROUND to ABOVE_GROUND + RADIUS + DISTANCE + RADIUS.  The parameters
                        * are chosen so that planes of above ~30° cannot be teleported to, because the
                        * bottom of the capsule always intersects that plane. 
                        */
                    const float ABOVE_GROUND = 0.1f, RADIUS = 0.32f, DISTANCE = 1.1f;

                    if (Physics.CheckCapsule(hitInfo.point + (ABOVE_GROUND + RADIUS) * Vector3.up,
                                                hitInfo.point + (ABOVE_GROUND + RADIUS + DISTANCE) * Vector3.up,
                                                RADIUS, traceLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        /* invalid position */
                        invalid_reticle.position = hitInfo.point;
                        invalid_reticle.rotation = Quaternion.LookRotation(hitInfo.normal) * Quaternion.Euler(90, 0, 0);
                        show_invalid = true;
                    }
                    else
                    {
                        /* valid position */
                        invalid_reticle.gameObject.SetActive(false);
                        destination_reticle.position = destination_position = hitInfo.point;
                        destination_valid = true;
                    }
                }
                invalid_reticle.gameObject.SetActive(show_invalid);
                destination_reticle.gameObject.SetActive(destination_valid);
                arc.SetColor(destination_valid ? validArcColor : invalidArcColor);
            }
            finally
            {
                Physics.queriesHitTriggers = saved;
            }
        }

        void OnTouchPressUp(Controller controller)
        {
            arc.Hide();
            invalid_reticle.gameObject.SetActive(false);
            destination_reticle.gameObject.SetActive(false);

            if (destination_valid)
                StartTeleporting();
        }

        void StartTeleporting()
        {
            FadeToColor(Color.black, 0.1f);
            Invoke("ChangeLocation", 0.11f);
        }

        void FadeToColor(Color target_color, float duration)
        {
            var compositor = OpenVR.Compositor;
            if (compositor != null)
                compositor.FadeToColor(duration, target_color.r, target_color.g, target_color.b, target_color.a, false);
        }

        void ChangeLocation()
        {
            Transform camera_rig = Baroque.GetSteamVRManager().transform;
            Transform steamvr_camera = Baroque.GetHeadTransform();
            Vector3 v = camera_rig.position + destination_position - steamvr_camera.position;
            v.y = destination_position.y;
            camera_rig.position = v;
            FadeToColor(Color.clear, 0.2f);
            if (onTeleported != null)
                onTeleported.Invoke();
        }
    }
}