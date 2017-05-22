using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace BaroqueUI
{
    public delegate float GetPriorityDelegate(Controller controller);
    public delegate void ControllerEvent(Controller controller);
    public delegate void ControllersUpdateEvent(Controller[] controllers);

    internal enum EEventSet
    {
        Hover = 0x01,
        HoverConcurrent = 0x02,
        Trigger = 0x04,
        Grip = 0x08,
        Menu = 0x10,
        Touchpad = 0x20,
        IsGlobal = 0x40,
    }

    internal class ControllerTracker
    {
        public readonly Controller controller;
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
        public ControllerEvent onTouchpadDown;
        public ControllerEvent onTouchpadDrag;
        public ControllerEvent onTouchpadUp;
        public ControllerEvent onTouchpadTouching;

        static int NUMBERING = 0;

        public ControllerTracker(Controller controller, MonoBehaviour tracker)
        {
            this.controller = controller;
            this.tracker = tracker;
            creation_order = ++NUMBERING;
            event_sets = 0;
        }

        public void AutoRegister()
        {
            onControllersUpdate = FindMethodArray(0, "OnControllersUpdate");

            onEnter = FindMethod(EEventSet.Hover, "OnEnter");
            if (onEnter == null)
                onEnter = FindMethod(EEventSet.Hover | EEventSet.HoverConcurrent, "OnEnterConcurrent");
            onMoveOver = FindMethod(EEventSet.Hover, "OnMoveOver");
            onLeave = FindMethod(EEventSet.Hover, "OnLeave");

            onTriggerDown = FindMethod(EEventSet.Trigger, "OnTriggerDown");
            onTriggerDrag = FindMethod(EEventSet.Trigger, "OnTriggerDrag");
            onTriggerUp   = FindMethod(EEventSet.Trigger, "OnTriggerUp");

            onGripDown = FindMethod(EEventSet.Grip, "OnGripDown");
            onGripDrag = FindMethod(EEventSet.Grip, "OnGripDrag");
            onGripUp   = FindMethod(EEventSet.Grip, "OnGripUp");

            onMenuClick = FindMethod(EEventSet.Menu, "OnMenuClick");

            onTouchpadDown     = FindMethod(EEventSet.Touchpad, "OnTouchpadDown");
            onTouchpadDrag     = FindMethod(EEventSet.Touchpad, "OnTouchpadDrag");
            onTouchpadUp       = FindMethod(EEventSet.Touchpad, "OnTouchpadUp");
            onTouchpadTouching = FindMethod(EEventSet.Touchpad, "OnTouchpadTouching");

            if ((event_sets & EEventSet.Hover) == 0 && tracker.GetComponentInChildren<Collider>() == null)
                event_sets |= EEventSet.IsGlobal;
        }

        public bool IsGlobal()
        {
            return (event_sets & EEventSet.IsGlobal) != 0;
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
                return Empty;
            else
                return (ctrl) => 
                {
                    if (tracker && tracker.isActiveAndEnabled)
                        minfo.Invoke(tracker, new object[] { ctrl });
                };
        }

        ControllersUpdateEvent FindMethodArray(EEventSet event_set, string method_name)
        {
            MethodInfo minfo = FindMethodInfo(event_set, method_name);
            if (minfo == null)
                return EmptyArray;
            else
                return (ctrls) => 
                {
                    if (tracker && tracker.isActiveAndEnabled)
                        minfo.Invoke(tracker, new object[] { ctrls });
                };
        }

        static void Empty(Controller ctrl) { }
        static void EmptyArray(Controller[] ctrls) { }
    }
}