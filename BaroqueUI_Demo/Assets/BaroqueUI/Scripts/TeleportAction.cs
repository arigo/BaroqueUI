using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{
    public class TeleportAction : AbstractControllerAction
    {
        [Header("Teleport beam parameters")]
        public float beamVelocity = 10f;
        public LayerMask traceLayerMask;
        public Color validArcColor = new Color(0.0f, 0.8f, 1.0f, 0.7f);
        public Color invalidArcColor = new Color(0.8f, 0f, 0.3f, 0.7f);
        public Material teleportMaterial;
        public Transform invalidReticlePrefab, destinationReticlePrefab;

        Valve.VR.InteractionSystem.TeleportArc arc;
        Transform invalidReticle, destinationReticle;

        void Reset()
        {
            controllerButton = EControllerButton.TouchpadClick;
            traceLayerMask = 1 << LayerMask.NameToLayer("Default");
            teleportMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SteamVR/InteractionSystem/Teleport/Materials/TeleportPointer.mat");

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SteamVR/InteractionSystem/Teleport/Prefabs/Teleporting.prefab");
            invalidReticlePrefab = go == null ? null : go.transform.Find("InvalidReticle");
            destinationReticlePrefab = go == null ? null : go.transform.Find("DestinationReticle");
        }

        void Start()
        {
            arc = gameObject.AddComponent<Valve.VR.InteractionSystem.TeleportArc>();
            arc.traceLayerMask = traceLayerMask;
            arc.material = teleportMaterial;
            invalidReticle = Instantiate<Transform>(invalidReticlePrefab);
            invalidReticle.gameObject.SetActive(false);
            destinationReticle = Instantiate<Transform>(destinationReticlePrefab);
            destinationReticle.gameObject.SetActive(false);
        }

        public override bool HandleButtonDown(ControllerSnapshot snapshot)
        {
            DisplayArc.New(gameObject, controllerButton, this);
            arc.Show();
            return true;   /* handled */
        }


        class DisplayArc : AbstractControllerAction
        {
            TeleportAction teleport;
            Vector3 destination;
            bool destination_valid = false;

            public static void New(GameObject where, EControllerButton button, TeleportAction teleport)
            {
                DisplayArc disp = where.AddComponent<DisplayArc>();
                disp.controllerButton = button;
                disp.teleport = teleport;
            }

            public override void HandleButtonMove(ControllerSnapshot snapshot)
            {
                var arc = teleport.arc;
                arc.SetArcData(snapshot.position, snapshot.forward * teleport.beamVelocity, true, false);

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
                        teleport.invalidReticle.position = hitInfo.point;
                        teleport.invalidReticle.rotation = Quaternion.LookRotation(hitInfo.normal) * Quaternion.Euler(90, 0, 0);
                        show_invalid = true;
                    }
                    else
                    {
                        /* valid position */
                        teleport.invalidReticle.gameObject.SetActive(false);
                        teleport.destinationReticle.position = destination = hitInfo.point;
                        destination_valid = true;
                    }
                }
                teleport.invalidReticle.gameObject.SetActive(show_invalid);
                teleport.destinationReticle.gameObject.SetActive(destination_valid);
                arc.SetColor(destination_valid ? teleport.validArcColor : teleport.invalidArcColor);
            }

            public override bool HandleButtonUp()
            {
                teleport.arc.Hide();
                teleport.invalidReticle.gameObject.SetActive(false);
                teleport.destinationReticle.gameObject.SetActive(false);

                if (destination_valid)
                {
                    FadeToColor(Color.black, 0.1f);
                    Invoke("ChangeLocation", 0.1f);
                }
                else
                    Destroy(this);
                return true;    /* handled */
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
                Vector3 v = camera_rig.position + destination - steamvr_camera.position;
                v.y = destination.y;
                camera_rig.position = v;
                FadeToColor(Color.clear, 0.2f);
                Destroy(this);
            }
        }
    }
}