using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace BaroqueUI
{
    public enum EControllerSelection { Left = (1 << 0), Right = (1 << 1), Either = Left | Right };


    public abstract class BaseControllerTracker : MonoBehaviour
    {
        public virtual void OnEnter(Controller controller) { }
        public virtual void OnLeave(Controller controller) { }
        public virtual void OnTriggerDown(Controller controller) { }
        public virtual void OnTriggerUp(Controller controller) { }

        public virtual float GetPriority(Controller controller)
        {
            /* Returns the priority of this ControllerTracker when the controller is at
             * its current position.  Higher priority trackers are picked first.  The
             * default implementation uses the size of the bounding box of the collider
             * or colliders in the gameobject, and negates it; this gives a negative
             * number, but also a higher number for smaller objects.  It does not
             * depend on the actual controller position.
             */
            float result = 0;
            foreach (var coll in GetComponentsInChildren<Collider>())
            {
                Vector3 size = coll.bounds.size;
                result = Mathf.Max(result, size.x, size.y, size.z);
            }
            return -result;
        }

        internal static long NUMBER;
        internal long creation_order;

        protected void Awake()
        {
            creation_order = ++NUMBER;
            BaroqueUI.EnsureStarted();
        }
    }

    public abstract class ControllerTracker : BaseControllerTracker
    {
        /* Base MonoBehaviour subclass for scripts you put on gameobjects with colliders
         * (typically trigger-type colliders).  Events occur in a well-defined order:
         * OnEnter/OnLeave when the controller enters or leaves the collider zone;
         * OnMoveOver as long as it is in the collider zone; and if the trigger is
         * pressed in the collider zone, you get OnTriggerDown, followed by OnTriggerDrag
         * (no more OnMoveOver/OnLeave at that point), followed by OnTriggerUp.
         * 
         * With this base class, not both controllers can be in the collider zone at the
         * same time.  If both controllers are in the zone, only one will be reported.
         * If we click the "inactive" controller's trigger, then the "active" controller
         * is first made inactive with OnLeave(), then OnEnter() is called with the other
         * controller.  The same occurs if GetPriority() is overridden with a controller- or
         * controller-position-dependent version.  It simplifies the code, and avoids bugs
         * if we don't test carefully what occurs with both controllers.
         * 
         * Use the ConcurrentControllerTracker subclass to get reports about both 
         * controllers concurrently.
         */
        public EControllerSelection selectableControllers = EControllerSelection.Either;

        public virtual void OnMoveOver(Controller controller) { }
        public virtual void OnTriggerDrag(Controller controller) { }
    }

    public abstract class ConcurrentControllerTracker : BaseControllerTracker
    {
        /* subclass that gives finer control over two-controllers interactions, at the cost
         * of the subclasser needing more care.  OnMove() is called with the non-empty list
         * of controllers that are currently over this zone.  See also 'controller.isGrabbing'.
         */
        public virtual void OnMove(Controller[] controllers) { }
    }
}


#if false
        internal object[] locals;

        public T GetLocal<T>(Controller controller) where T : class, new()
        {
            /* Both controllers can interact at the same time, so subclasses can separate
             * the controller-local fields from the other fields and put them in a local
             * class T.
             */
            int index = controller.index;
            if (locals == null || locals.Length <= index)
                Array.Resize(ref locals, index + 1);

            object result = locals[index];
            if (result == null)
            {
                result = new T();
                locals[index] = result;
            }
            return result as T;
        }

    
    
    static long NUMBER = 0;
        public long _creation_order;

        ControllerTracker DuplicateComponent()
        {
            Type type = GetType();
            var copy = gameObject.AddComponent(type) as ControllerTracker;

            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var finfo in type.GetFields(flags))    /* list of public instance fields */
            {
                finfo.SetValue(copy, finfo.GetValue(this));
            }
            foreach (var pinfo in type.GetProperties(flags))   /* list of properties, including read-only or ones with private setters */
            {
                if (pinfo.GetSetMethod(false) == null)    /* the 'set' method does not exist or is not public */
                    continue;

                if (pinfo.GetIndexParameters().Length != 0)
                {
                    Debug.LogWarningFormat("'{0}' property '{1}' is indexed, ignoring for now", type.FullName, pinfo.Name);
                    continue;
                }
                pinfo.SetValue(copy, pinfo.GetValue(this, null), null);
            }
            return copy;
        }

        protected void Awake()
        {
            /* don't do any magic if 'controller' is already assigned */
            if (attached_controller != null)
                return;

            Controller[] ctrls = BaroqueUI.GetControllers();

            /* find the components of exactly the same type as me */
            Type type = GetType();
            var comps = new List<ControllerTracker>();
            foreach (var comp in GetComponents(type))
            {
                if (comp.GetType() == type)
                    comps.Add(comp as ControllerTracker);
            }
            Debug.Assert(comps.Count >= 1);

            /* assign the 'controller' fields on the components, possibly making 
             * duplicates if there are not enough of them */
            for (int i = 0; i < ctrls.Length; i++)
            {
                if (comps.Count == i)
                    comps.Add(comps[i - 1].DuplicateComponent());
                comps[i].attached_controller = ctrls[i];
                comps[i].attached_creation_order = ++NUMBER;
            }
        }
    }
#endif