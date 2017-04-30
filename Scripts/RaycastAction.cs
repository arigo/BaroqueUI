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

        public new void Reset()
        {
            base.Reset();
            actionName = "Raycast";
            maxDistance = 12f;
            raycastMaterial = Resources.Load<Material>("BaroqueUI/PointerMaterial");
            rayColorHit = new Color(0, 0.83f, 0.83f);
            rayColorActive = new Color(0, 1, 1);
            rayColorMiss = new Color(0.84f, 0, 0.63f);
        }

        /***************************************************************************************************/


        public RaycastHit currentHitInfo;

        public override Vector3 GetPosition()
        {
            return currentHitInfo.point;
        }

        public override bool HasHoverVisualEffect()
        {
            return true;
        }

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

            /* Sort the hits by distance */
            Array.Sort<RaycastHit>(hit_infos, (x, y) => x.distance.CompareTo(y.distance));

            Color end_color = Color.clear;
            float end_distance = maxDistance;

            /* Find and return the first non-null hover */
            foreach (var hit_info in hit_infos)      /* distance order */
            {
                Hover best_hover = null;
                float best_size_estimate = float.PositiveInfinity;

                currentHitInfo = hit_info;
                foreach (var rd in hit_info.transform.GetComponentsInParent<SceneDelegate>())
                {
                    if (rd.sceneAction == this && rd.findHoverMethod != null)
                    {
                        Hover hover = rd.findHoverMethod(this, snapshot);
                        if (Hover.IsBetterHover(hover, best_hover, true_if_equal: rd.sizeEstimate < best_size_estimate))
                        {
                            best_hover = hover;
                            best_size_estimate = rd.sizeEstimate;
                        }
                    }
                }
                if (best_hover != null)
                {
                    DrawLine(rayColorHit, rayColorHit, currentHitInfo.distance);
                    return best_hover;
                }
                /* stop looking after hitting a non-trigger collider */
                if (!hit_info.collider.isTrigger)
                {
                    end_color = rayColorMiss;
                    end_distance = hit_info.distance;
                    break;
                }
            }
            DrawLine(rayColorMiss, end_color, end_distance);
            return null;
        }

        public override void Dragging(Hover hover, ControllerSnapshot snapshot)
        {
            if (!this || !isActiveAndEnabled)
            {
                RemoveLine();
                return;
            }
            if (hover == null)
                FindHover(snapshot);   /* continue to look for a hover, even though we can't do anything with it until dragging ends */
            else
                DrawLine(rayColorActive, rayColorActive, currentHitInfo.distance);
        }

        const float thickness = 0.004f, thickness_arrow = 0.04f, arrow_fraction = 0.1f;
        LineRenderer line_renderer;

        private void OnDisable()
        {
            RemoveLine();
        }
        private void OnDestroy()
        {
            RemoveLine();
        }

        protected void RemoveLine()
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

                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0, thickness);
                curve.AddKey(1 - arrow_fraction, thickness);
                curve.AddKey(1 - 0.8f * arrow_fraction, thickness_arrow);
                curve.AddKey(1, thickness);
                line_renderer.widthCurve = curve;
                line_renderer.widthMultiplier = 1;
            }
            Vector3 end_point = transform.position + transform.forward * distance;
            if (color_end == Color.clear)
            {
                line_renderer.numPositions = 2;
                line_renderer.SetPosition(1, end_point);
            }
            else
            {
                line_renderer.numPositions = 4;
                line_renderer.SetPosition(3, end_point);
                line_renderer.SetPosition(2, Vector3.Lerp(transform.position, end_point, 1 - 0.8f * arrow_fraction));
                line_renderer.SetPosition(1, Vector3.Lerp(transform.position, end_point, 1 - arrow_fraction));
            }
            line_renderer.SetPosition(0, transform.position);
#if UNITY_5_4
            line_renderer.SetColors( color_start, color_start );
#else
            line_renderer.startColor = line_renderer.endColor = color_start;
            //line_renderer.endColor = color_end;
#endif
        }
    }
}
