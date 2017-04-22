using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public delegate bool ButtonDownHandler(ControllerSnapshot snapshot);
    public delegate void ButtonMoveHandler(ControllerSnapshot snapshot);
    public delegate bool ButtonUpHandler();

    public class SceneDelegate : MonoBehaviour
    {
        public SceneAction sceneAction;
        public ButtonDownHandler buttonDown;
        public ButtonMoveHandler buttonMove;
        public ButtonUpHandler buttonUp;

        static public SceneDelegate Register(string tag, Component component)
        {
            return Register(SceneAction.ActionByTag(tag), component);
        }

        static public SceneDelegate Register(SceneAction action, Component component)
        {
            foreach (SceneDelegate sd in component.GetComponents<SceneDelegate>())
                if (sd.sceneAction == action)
                    return sd;

            SceneDelegate newsd = component.gameObject.AddComponent<SceneDelegate>();
            newsd.sceneAction = action;
            return newsd;
        }

        static public void Unregister(SceneDelegate sd)
        {
            sd.sceneAction = null;
            Destroy(sd);
        }
    }


    public class SceneAction : AbstractControllerAction
    {
        public int layerMask;
        public QueryTriggerInteraction trigger_interaction;

        void Reset()
        {
            layerMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            trigger_interaction = QueryTriggerInteraction.Collide;
        }

        static public SceneAction ActionByTag(string tag)
        {
            GameObject gobj = GameObject.FindGameObjectWithTag(tag);
            SceneAction sa = gobj == null ? null : gobj.GetComponent<SceneAction>();
            if (sa == null)
                throw new ArgumentException("The tag '" + tag + "' does not exist or refers to a game object without the 'SceneAction' component");
            return sa;
        }


        struct FoundDelegate
        {
            public SceneDelegate scene_delegate;
            public float size;
            public FoundDelegate(SceneDelegate sd, float sz) { scene_delegate = sd; size = sz; }
        }

        SceneDelegate[] FindDelegateOrder()
        {
            /* XXX!  This whole logic could use proper caching */

            /* If there are colliders on the SceneAction's gameObject or subobjects, then we use them
             * as defining the shape of the controller for the purpose of this SceneAction.  They
             * should be trigger colliders only.  If there are none, we default to a very small
             * sphere around this SceneAction's 'transform.position'.  Remember that SceneAction is 
             * meant to be a child of the controller objects in the hierarchy, so 'transform.position'
             * and the colliders follow this controller automatically).
             *
             * We compute SceneShapes by looking through the scene for SceneDelegate that have been 
             * registered for this SceneAction.  For each one, its SceneShape is the union of the 
             * colliders on that GameObject and any children.  We stop if we encounter a child with its 
             * own SceneDelegate for the same SceneAction; this child's colliders will be part of that 
             * child's own "scene shape" but not the parent's.
             * 
             * If there are several possible SceneShapes, for now we pick the one which is the "smallest"
             * according to some approximation.  Later we might consider more advanced logic like
             * considering the parenting between SceneShapes, and whether we are close to the "center"
             * or not for some definition of "center", etc.
             */

            List<FoundDelegate> found = new List<FoundDelegate>();
            Collider[] ctrl_colls = GetComponentsInChildren<Collider>();
            if (ctrl_colls.Length == 0)
                ctrl_colls = new Collider[] { null };   /* hack */

            foreach (var ctrl_coll in ctrl_colls)
            {
                Collider[] lst;
                if (ctrl_coll == null)
                {
                    lst = Physics.OverlapSphere(transform.position, 0.001f, layerMask, trigger_interaction);
                }
                else if (ctrl_coll is SphereCollider)
                {
                    /* NB. maybe not right if lossyScale is really lossy */
                    SphereCollider sc = (SphereCollider)ctrl_coll;
                    Vector3 v = sc.transform.lossyScale;
                    var scale = Mathf.Max(v.x, v.y, v.z);
                    lst = Physics.OverlapSphere(sc.transform.TransformPoint(sc.center),
                                                scale * sc.radius, layerMask, trigger_interaction);
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
                                                 layerMask, trigger_interaction);
                }
                else if (ctrl_coll is BoxCollider)
                {
                    BoxCollider bc = (BoxCollider)ctrl_coll;

                    lst = Physics.OverlapBox(bc.transform.TransformPoint(bc.center),
                                             bc.size * 0.5f,
                                             bc.transform.rotation,
                                             layerMask, trigger_interaction);
                }
                else
                {
                    /* give up and fall back on the axis-aligned bounding box (AABB) */
                    Bounds bounds = ctrl_coll.bounds;
                    lst = Physics.OverlapBox(ctrl_coll.transform.TransformPoint(bounds.center),
                                             bounds.extents * 0.5f,
                                             Quaternion.identity,
                                             layerMask, trigger_interaction);
                }

                foreach (var coll in lst)
                {
                    Vector3 v = coll.bounds.extents;
                    float size = Mathf.Max(v.x, v.y, v.z);

                    foreach (var sd in coll.transform.GetComponentsInParent<SceneDelegate>())
                        if (sd.sceneAction == this)
                            found.Add(new FoundDelegate(sd, size));
                }
            }

            found.Sort((x, y) => x.size.CompareTo(y.size));

            List<SceneDelegate> result_list = new List<SceneDelegate>();
            Dictionary<SceneDelegate, bool> seen = new Dictionary<SceneDelegate, bool>();
            foreach (var f in found)
            {
                if (!seen.ContainsKey(f.scene_delegate))
                {
                    seen[f.scene_delegate] = true;
                    result_list.Add(f.scene_delegate);
                }
            }
            return result_list.ToArray();
        }

        public override bool HandleButtonDown(ControllerSnapshot snapshot)
        {
            foreach (var sd in FindDelegateOrder())
            {
                if (sd.buttonDown != null)
                    if (sd.buttonDown(snapshot))
                        return true;
            }
            return false;
        }

        public override void HandleButtonMove(ControllerSnapshot snapshot)
        {
            foreach (var sd in FindDelegateOrder())
            {
                if (sd.buttonMove != null)
                    sd.buttonMove(snapshot);
            }
        }

        public override bool HandleButtonUp()
        {
            foreach (var sd in FindDelegateOrder())
            {
                if (sd.buttonUp != null)
                    if (sd.buttonUp())
                        return true;
            }
            return false;
        }
    }
}
