using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public delegate Hover FindHoverMethod(ControllerAction action, ControllerSnapshot snapshot);


    public class SceneDelegate : MonoBehaviour
    {
        public SceneAction sceneAction;
        public FindHoverMethod findHoverMethod;
        public float sizeEstimate;
    }


    public class SceneAction : ControllerAction
    {
        [Header("Scene action parameters")]
        public string actionName;
        public bool alsoForHovering;
        public LayerMask layerMask;

        //public QueryTriggerInteraction collideWithTriggersToo; -- should always be Collide, I guess
        protected const QueryTriggerInteraction collideWithTriggersToo = QueryTriggerInteraction.Collide;

        protected void Reset()
        {
            actionName = "Default";
            alsoForHovering = true;
            layerMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            //collideWithTriggersToo = QueryTriggerInteraction.Collide;
        }

        static public List<SceneAction> ActionsByName(string name)
        {
            var result = new List<SceneAction>();
            var mgr = BaroqueUI_Controller.FindSteamVRControllerManager();
            foreach (var sa in mgr.GetComponentsInChildren<SceneAction>(/*includeInactive=*/true))
                if (sa.actionName == name)
                    result.Add(sa);
            return result;
        }

        static public void RegisterHover(string action_name, GameObject game_object, FindHoverMethod method)
        {
            var actions = SceneAction.ActionsByName(action_name);
            if (actions.Count == 0)
                throw new ArgumentException("Found no 'SceneAction' with the name '" + action_name + "'");

            foreach (var action in actions)
                action.RegisterHover(game_object, method);
        }

        public void RegisterHover(GameObject game_object, FindHoverMethod method)
        {
            SceneDelegate sd = null;

            foreach (var sd1 in game_object.GetComponents<SceneDelegate>())
            {
                if (sd1.sceneAction == this)
                {
                    sd = sd1;
                    break;
                }
            }
            if (sd == null)
            {
                sd = game_object.AddComponent<SceneDelegate>();
                sd.sceneAction = this;
            }
            sd.findHoverMethod = method;

            if (sd.sizeEstimate == 0)
            {
                Vector3 scale = game_object.transform.lossyScale;
                sd.sizeEstimate = scale.magnitude;
            }
        }

        static public void Register(string action_name, GameObject game_object, float reversed_priority=0,
                               HoverDelegate buttonEnter = null, HoverDelegate buttonOver = null, HoverDelegate buttonLeave = null,
                               HoverDelegate buttonDown = null, HoverDelegate buttonDrag = null, HoverDelegate buttonUp = null)
        {
            var hover = new DelegatingHover(reversed_priority, buttonEnter, buttonOver, buttonLeave, buttonDown, buttonDrag, buttonUp);
            RegisterHover(action_name, game_object, hover.FindHover);
        }

        public void Register(GameObject game_object, float reversed_priority=0,
                               HoverDelegate buttonEnter = null, HoverDelegate buttonOver = null, HoverDelegate buttonLeave = null,
                               HoverDelegate buttonDown = null, HoverDelegate buttonDrag = null, HoverDelegate buttonUp = null)
        {
            var hover = new DelegatingHover(reversed_priority, buttonEnter, buttonOver, buttonLeave, buttonDown, buttonDrag, buttonUp);
            RegisterHover(game_object, hover.FindHover);
        }

        /***************************************************************************************************/


        IEnumerable<SceneDelegate> FindDelegateOrder()
        {
            /* XXX!  This whole logic could use proper caching */

            /* If there are colliders on the SceneAction's gameObject or subobjects, then we use them
             * as defining the shape of the controller for the purpose of this SceneAction.  They
             * should be trigger colliders only.  If there are none, we default to a very small
             * sphere around this SceneAction's 'transform.position'.  Remember that SceneAction is 
             * meant to be a child of the controller objects in the hierarchy, so 'transform.position'
             * and the colliders follow this controller automatically.
             *
             * We compute SceneShapes by looking through the scene for SceneDelegate that have been 
             * registered for this SceneAction.  For each one, its SceneShape is the union of the 
             * colliders on that GameObject and any children.  We stop if we encounter a child with its 
             * own SceneDelegate for the same SceneAction; this child's colliders will be part of that 
             * child's own "scene shape" but not the parent's.
             */
            var all_sd = new Dictionary<SceneDelegate, bool>();
            Collider[] ctrl_colls = null;

            if (GetComponent<SteamVR_TrackedObject>() != null)
            {
                /* SceneAction directly on the controller gameobject: don't use the Colliders, which 
                 * would be all colliders everywhere in this Controller */
            }
            else
            {
                ctrl_colls = GetComponentsInChildren<Collider>();
            }
            if (ctrl_colls == null || ctrl_colls.Length == 0)
                ctrl_colls = new Collider[] { null };   /* hack */

            foreach (var ctrl_coll in ctrl_colls)
            {
                Collider[] lst;
                if (ctrl_coll == null)
                {
                    lst = Physics.OverlapSphere(transform.position, 0.001f, layerMask, collideWithTriggersToo);
                }
                else if (ctrl_coll is SphereCollider)
                {
                    /* NB. maybe not right if lossyScale is really lossy */
                    SphereCollider sc = (SphereCollider)ctrl_coll;
                    Vector3 v = sc.transform.lossyScale;
                    var scale = Mathf.Max(v.x, v.y, v.z);
                    lst = Physics.OverlapSphere(sc.transform.TransformPoint(sc.center),
                                                scale * sc.radius, layerMask, collideWithTriggersToo);
                }
                else if (ctrl_coll is CapsuleCollider)
                {
                    /* pff, XXX check me very carefully */
                    CapsuleCollider cc = (CapsuleCollider)ctrl_coll;

                    Vector3 delta;
                    Vector3 scale1, scale2;

                    switch (cc.direction)
                    {
                        case 0:
                            delta = new Vector3(1, 0, 0);
                            scale1 = new Vector3(0, 1, 0);
                            scale2 = new Vector3(0, 0, 1);
                            break;

                        case 1:
                            delta = new Vector3(0, 1, 0);
                            scale1 = new Vector3(0, 0, 1);
                            scale2 = new Vector3(1, 0, 0);
                            break;

                        case 2:
                            delta = new Vector3(0, 0, 1);
                            scale1 = new Vector3(1, 0, 0);
                            scale2 = new Vector3(0, 1, 0);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    var radius = Mathf.Max(cc.transform.TransformVector(scale1).magnitude,
                                           cc.transform.TransformVector(scale2).magnitude);
                    radius *= cc.radius;
                    Vector3 delta_v = cc.transform.TransformVector(delta);
                    var delta_length = delta_v.magnitude;
                    delta_length *= cc.height * 0.5f;
                    delta_length -= 2 * radius;
                    if (delta_length < 0)
                        delta_length = 0;

                    delta_v *= delta_length;
                    Vector3 center = cc.transform.TransformPoint(cc.center);

                    lst = Physics.OverlapCapsule(center + delta_v, center - delta_v, radius,
                                                 layerMask, collideWithTriggersToo);
                }
                else if (ctrl_coll is BoxCollider)
                {
                    BoxCollider bc = (BoxCollider)ctrl_coll;

                    lst = Physics.OverlapBox(bc.transform.TransformPoint(bc.center),
                                             bc.size * 0.5f,
                                             bc.transform.rotation,
                                             layerMask, collideWithTriggersToo);
                }
                else
                {
                    /* give up and fall back on the axis-aligned bounding box (AABB) */
                    Bounds bounds = ctrl_coll.bounds;
                    lst = Physics.OverlapBox(ctrl_coll.transform.TransformPoint(bounds.center),
                                             bounds.extents * 0.5f,
                                             Quaternion.identity,
                                             layerMask, collideWithTriggersToo);
                }

                foreach (var coll in lst)
                {
                    foreach (var sd in coll.transform.GetComponentsInParent<SceneDelegate>())
                    {
                        if (sd.sceneAction == this)
                            all_sd[sd] = true;
                    }
                }
            }
            return all_sd.Keys;
        }

        public override Hover FindHover(ControllerSnapshot snapshot)
        {
            if (!alsoForHovering && !IsPressingButton(snapshot))
                return null;

            Hover best_hover = null;
            float best_size_estimate = float.PositiveInfinity;

            foreach (var sd in FindDelegateOrder())
            {
                if (sd.findHoverMethod == null)
                    continue;
                Hover hover = sd.findHoverMethod(this, snapshot);
                if (Hover.IsBetterHover(hover, best_hover, true_if_equal: sd.sizeEstimate < best_size_estimate))
                    best_hover = hover;
            }
            return best_hover;
        }
    }
}
