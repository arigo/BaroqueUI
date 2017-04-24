using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{
    public class TeleportAction : ControllerAction
    {
        [Header("Teleport beam parameters")]
        public float beamVelocity = 10f;
        public LayerMask traceLayerMask;
        public Color validArcColor = new Color(0.0f, 0.8f, 1.0f, 0.7f);
        public Color invalidArcColor = new Color(0.8f, 0f, 0.3f, 0.7f);
        public Material teleportMaterial;
        public Transform invalidReticlePrefab, destinationReticlePrefab;

        Valve.VR.InteractionSystem.TeleportArc arc;
        Transform invalid_reticle, destination_reticle;
        Vector3 destination_position;

        T _LoadLibAsset<T>(string relpath) where T: UnityEngine.Object
        {
            T result = AssetDatabase.LoadAssetAtPath<T>("Assets/" + relpath);
            if (result == null)
                result = AssetDatabase.LoadAssetAtPath<T>("Assets/Lib/" + relpath);
            if (result == null)
                Debug.LogWarning("Cannot locate the asset '" + relpath + "'");
            return result;
        }

        void Reset()
        {
            controllerButton = EControllerButton.Touchpad;
            traceLayerMask = 1 << LayerMask.NameToLayer("Default");
            teleportMaterial = _LoadLibAsset<Material>(
                "SteamVR/InteractionSystem/Teleport/Materials/TeleportPointer.mat");

            GameObject go = _LoadLibAsset<GameObject>(
                "SteamVR/InteractionSystem/Teleport/Prefabs/Teleporting.prefab");
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
        }

        public override Hover FindHover(ControllerSnapshot snapshot)
        {
            if (IsPressingButton(snapshot))
                return new DisplayArcHover(this);
            return null;
        }

        internal void StartTeleporting(Vector3 destination)
        {
            FadeToColor(Color.black, 0.1f);
            destination_position = destination;
            Invoke("ChangeLocation", 0.1f);
        }

        void FadeToColor(Color target_color, float duration)
        {
            var compositor = OpenVR.Compositor;
            if (compositor != null)
                compositor.FadeToColor(duration, target_color.r, target_color.g, target_color.b, target_color.a, false);
        }

        void ChangeLocation()
        {
            Transform camera_rig = GetComponentInParent<SteamVR_ControllerManager>().transform;
            Transform steamvr_camera = camera_rig.GetComponentInChildren<SteamVR_Camera>().transform;
            Vector3 v = camera_rig.position + destination_position - steamvr_camera.position;
            v.y = destination_position.y;
            camera_rig.position = v;
            FadeToColor(Color.clear, 0.2f);
        }


        class DisplayArcHover : Hover
        {
            TeleportAction teleport;
            Vector3 destination;
            bool destination_valid = false;

            internal DisplayArcHover(TeleportAction teleport)
            {
                this.teleport = teleport;
            }

            public override void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
            {
                /* This is called just after the class is instantiated, but only if this Hover is really
                 * selected by the logic of priorities is the end. 
                 */
                teleport.arc.Show();
            }

            public override void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
            {
                bool saved = Physics.queriesHitTriggers;
                try
                {
                    Physics.queriesHitTriggers = false;

                    var arc = teleport.arc;
                    arc.SetArcData(teleport.transform.position, teleport.transform.forward * teleport.beamVelocity, true, false);

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
                                                    RADIUS, teleport.traceLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            /* invalid position */
                            teleport.invalid_reticle.position = hitInfo.point;
                            teleport.invalid_reticle.rotation = Quaternion.LookRotation(hitInfo.normal) * Quaternion.Euler(90, 0, 0);
                            show_invalid = true;
                        }
                        else
                        {
                            /* valid position */
                            teleport.invalid_reticle.gameObject.SetActive(false);
                            teleport.destination_reticle.position = destination = hitInfo.point;
                            destination_valid = true;
                        }
                    }
                    teleport.invalid_reticle.gameObject.SetActive(show_invalid);
                    teleport.destination_reticle.gameObject.SetActive(destination_valid);
                    arc.SetColor(destination_valid ? teleport.validArcColor : teleport.invalidArcColor);
                }
                finally
                {
                    Physics.queriesHitTriggers = saved;
                }
            }

            public override void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
            {
                teleport.arc.Hide();
                teleport.invalid_reticle.gameObject.SetActive(false);
                teleport.destination_reticle.gameObject.SetActive(false);

                if (destination_valid)
                    teleport.StartTeleporting(destination);
            }
        }
    }
}