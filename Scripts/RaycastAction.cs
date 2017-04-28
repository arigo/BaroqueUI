using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public class RaycastAction : SceneAction
    {
        [Header("Raycast action parameters")]
        public float maxDistance;
        public Material raycastMaterial;
        public Color rayColorHit, rayColorActive, rayColorMiss;

        protected new void Reset()
        {
            base.Reset();
            actionName = "Raycast";
            maxDistance = 12f;
            raycastMaterial = BaroqueUI_Controller._LoadLibAsset<Material>(
                "SteamVR/InteractionSystem/Teleport/Materials/TeleportPointer.mat");
            rayColorHit = new Color(0, 1, 1);
            rayColorActive = new Color(0, 1, 1);
            rayColorMiss = new Color(0.84f, 0, 0.63f);
        }

        /***************************************************************************************************/


        public RaycastHit currentHitInfo;

        RaycastHit[] IssueRaycast()
        {
            return Physics.RaycastAll(transform.position, transform.forward, maxDistance, layerMask,
                                      collideWithTriggersToo);
        }

        public override Hover FindHover(ControllerSnapshot snapshot)
        {
            if (!snapshot.GetButton(controllerButton) && snapshot.GetAnyButton())
            {
                RemoveLine();
                return null;
            }

            RaycastHit[] hit_infos = IssueRaycast();

            /* First, we trim the ray at the closest non-trigger collider it hits. */
            float limit_distance = float.PositiveInfinity;
            foreach (var hit_info in hit_infos)
            {
                if (!hit_info.collider.isTrigger)
                    limit_distance = Mathf.Min(limit_distance, hit_info.distance);
            }
            /* From now on we only consider hits up to 'max_distance'. */

            /* Find the highest-priority hover and remember the corresponding distance. */
            Hover best_hover = null;
            float best_hover_distance = 0;

            foreach (var hit_info in hit_infos)
            {
                float distance = hit_info.distance;
                if (distance > limit_distance)
                    continue;
                foreach (var rd in hit_info.transform.GetComponentsInParent<SceneDelegate>())
                {
                    if (rd.sceneAction == this && rd.findHoverMethod != null)
                    {
                        currentHitInfo = hit_info;
                        Hover hover = rd.findHoverMethod(this, snapshot);
                        if (Hover.IsBetterHover(hover, best_hover, true_if_equal: distance < best_hover_distance))
                        {
                            best_hover = hover;
                            best_hover_distance = distance;
                        }
                    }
                }
            }
            currentHitInfo.distance = best_hover_distance;
            if (best_hover != null)
                DrawLine(rayColorHit, rayColorHit, best_hover_distance);
            else if (limit_distance <= maxDistance)
                DrawLine(rayColorMiss, rayColorMiss, limit_distance);
            else
                DrawLine(rayColorMiss, Color.clear, maxDistance);

            return best_hover;
        }

        public override void Dragging(Hover hover, ControllerSnapshot snapshot)
        {
            DrawLine(rayColorActive, rayColorActive, currentHitInfo.distance);
        }

        const float thickness = 0.004f, thickness_arrow = 0.04f, arrow_fraction = 0.1f;
        LineRenderer line_renderer;

        private void OnDisable()
        {
            RemoveLine();
        }

        void RemoveLine()
        {
            if (line_renderer != null)
            {
                Destroy(line_renderer.gameObject);
                line_renderer = null;
            }
        }

        void DrawLine(Color color_start, Color color_end, float distance)
        {
            if (color_start == Color.clear && color_end == Color.clear)
            {
                RemoveLine();
                return;
            }
            if (line_renderer == null)
            {
                line_renderer = new GameObject("RaycastLineRenderer").AddComponent<LineRenderer>();
                line_renderer.receiveShadows = false;
                line_renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                line_renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                line_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line_renderer.material = raycastMaterial;
/*
#if UNITY_5_4
                line_renderer.SetWidth( thickness, thickness );
#else
                line_renderer.startWidth = thickness;
                line_renderer.endWidth = thickness_end;
#endif
*/
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0, thickness);
                curve.AddKey(1 - arrow_fraction, thickness);
                curve.AddKey(1 - 0.8f * arrow_fraction, thickness_arrow);
                curve.AddKey(1, thickness);
                line_renderer.widthCurve = curve;
                line_renderer.widthMultiplier = 1;
                line_renderer.numPositions = 4;
            }
            Vector3 end_point = transform.position + transform.forward * distance;
            line_renderer.SetPosition(0, transform.position);
            line_renderer.SetPosition(1, Vector3.Lerp(transform.position, end_point, 1 - arrow_fraction));
            line_renderer.SetPosition(2, Vector3.Lerp(transform.position, end_point, 1 - 0.8f * arrow_fraction));
            line_renderer.SetPosition(3, end_point);
#if UNITY_5_4
            line_renderer.SetColors( color_start, color_end );
#else
            line_renderer.startColor = color_start;
            line_renderer.endColor = color_end;
#endif
        }
    }
}
