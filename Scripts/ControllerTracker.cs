using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace BaroqueUI
{
    public delegate float GetPriorityDelegate(Controller controller);
    public delegate void ControllerEvent(Controller controller);
    public delegate void ControllersUpdateEvent(Controller[] controllers);
    public delegate void ControllerVec2Event(Controller controller, Vector2 relative_pos);

    internal enum EEventSet
    {
        Hover = 0x01,
        Trigger = 0x02,
        Grip = 0x04,
        Menu = 0x08,
        Touchpad = 0x10,
        TouchpadAction1 = 0x20,  /* OnTouchPressDown, ... */
        TouchpadAction2 = 0x40,  /* OnTouchScroll         */
        TouchpadAction3 = 0x80,  /* OnTouchDown, ...      */
        IsConcurrent = 0x100,
        IsGlobal = 0x200,
    }

    internal class ControllerTracker
    {
        public readonly MonoBehaviour tracker;
        public readonly int creation_order;
        public GetPriorityDelegate get_priority;

        public EEventSet event_sets;   /* bitmask */
        public ControllersUpdateEvent onControllersUpdate;
        public ControllerEvent onEnter;
        public ControllerEvent onMoveOver;
        public ControllerEvent onLeave;
        public ControllerEvent onTriggerDown;
        public ControllerEvent onTriggerDrag;
        public ControllerEvent onTriggerUp;
        public ControllerEvent onGripDown;
        public ControllerEvent onGripDrag;
        public ControllerEvent onGripUp;
        public ControllerEvent onMenuClick;
        public ControllerEvent onTouchPressDown;
        public ControllerEvent onTouchPressDrag;
        public ControllerEvent onTouchPressUp;
        public ControllerVec2Event onTouchScroll;
        public ControllerEvent onTouchDown;
        public ControllerEvent onTouchDrag;
        public ControllerEvent onTouchUp;

        static int NUMBERING = 0;

        public ControllerTracker(MonoBehaviour tracker)
        {
            this.tracker = tracker;
            creation_order = ++NUMBERING;
            event_sets = 0;
        }

        public void AutoRegister(GetPriorityDelegate get_priority, bool concurrent)
        {
            event_sets = 0;
            if (concurrent)
                event_sets |= EEventSet.IsConcurrent;

            onControllersUpdate = FindMethodArray(0, "OnControllersUpdate");

            onEnter = FindMethod(EEventSet.Hover, "OnEnter");
            onMoveOver = FindMethod(EEventSet.Hover, "OnMoveOver");
            onLeave = FindMethod(EEventSet.Hover, "OnLeave");

            onTriggerDown = FindMethod(EEventSet.Trigger, "OnTriggerDown");
            onTriggerDrag = FindMethod(EEventSet.Trigger, "OnTriggerDrag");
            onTriggerUp   = FindMethod(EEventSet.Trigger, "OnTriggerUp");

            onGripDown = FindMethod(EEventSet.Grip, "OnGripDown");
            onGripDrag = FindMethod(EEventSet.Grip, "OnGripDrag");
            onGripUp   = FindMethod(EEventSet.Grip, "OnGripUp");

            onMenuClick = FindMethod(EEventSet.Menu, "OnMenuClick");

            onTouchPressDown = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction1, "OnTouchPressDown");
            onTouchPressDrag = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction1, "OnTouchPressDrag");
            onTouchPressUp   = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction1, "OnTouchPressUp");

            onTouchScroll = FindMethodVec2(EEventSet.Touchpad | EEventSet.TouchpadAction2, "OnTouchScroll");

            onTouchDown = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction3, "OnTouchDown");
            onTouchDrag = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction3, "OnTouchDrag");
            onTouchUp   = FindMethod(EEventSet.Touchpad | EEventSet.TouchpadAction3, "OnTouchUp");

            if (!IsHover() && tracker.GetComponentInChildren<Collider>() == null)
                event_sets |= EEventSet.IsGlobal;

            if (get_priority == null)
            {
                var colliders = tracker.GetComponentsInChildren<Collider>();
                get_priority = (ctrl) => -ctrl.DistanceToColliderCore(colliders);
            }
            this.get_priority = get_priority;
        }

        public bool IsGlobal()
        {
            return (event_sets & EEventSet.IsGlobal) != 0;
        }

        public bool IsHover()
        {
            return (event_sets & EEventSet.Hover) != 0;
        }

        public bool IsConcurrent()
        {
            return (event_sets & EEventSet.IsConcurrent) != 0;
        }

        public void PickIfBetter(float priority, ref ControllerTracker current_best, ref float current_best_priority)
        {
            if (priority > current_best_priority ||
                    (priority == current_best_priority && creation_order > current_best.creation_order))
            {
                current_best_priority = priority;
                current_best = this;
            }
        }

        MethodInfo FindMethodInfo(EEventSet event_set, string method_name)
        {
            Type type = tracker.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var minfo = type.GetMethod(method_name, flags);
            if (minfo != null)
                event_sets |= event_set;
            /* XXX minfo.CreateDelegate?  only from .NET version 4.5... */
            return minfo;
        }

        ControllerEvent FindMethod(EEventSet event_set, string method_name)
        {
            MethodInfo minfo = FindMethodInfo(event_set, method_name);
            if (minfo == null)
                return (ctrl) => { };
            else
                return (ctrl) => { Run(minfo, new object[] { ctrl }); };
        }

        ControllersUpdateEvent FindMethodArray(EEventSet event_set, string method_name)
        {
            MethodInfo minfo = FindMethodInfo(event_set, method_name);
            if (minfo == null)
                return (ctrls) => { };
            else
                return (ctrls) => { Run(minfo, new object[] { ctrls }); };
        }

        ControllerVec2Event FindMethodVec2(EEventSet event_set, string method_name)
        {
            MethodInfo minfo = FindMethodInfo(event_set, method_name);
            if (minfo == null)
                return (ctrl, pos) => { };
            else
                return (ctrl, pos) => { Run(minfo, new object[] { ctrl, pos }); };
        }

        void Run(MethodInfo minfo, object[] args)
        {
            if (tracker && tracker.isActiveAndEnabled)
            {
                try
                {
                    minfo.Invoke(tracker, args);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

    }
}
