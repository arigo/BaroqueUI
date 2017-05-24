using System;
using System.Collections.Generic;
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
    }

    public class ControllerTracker
    {
        public readonly MonoBehaviour tracker;

        internal readonly int creation_order;
        internal EEventSet event_sets;   /* bitmask */
        private static int NUMBERING = 0;

        internal ControllerTracker(MonoBehaviour tracker, bool is_hover)
        {
            this.tracker = tracker;
            creation_order = ++NUMBERING;
            if (is_hover)
            {
                event_sets = EEventSet.Hover;
                SetPriorityFromDistance(0);
            }
            else
            {
                event_sets = 0;
                SetPriority(0);
            }
        }

        public GetPriorityDelegate computePriority { get; set; }

        public void SetPriority(float value)
        {
            computePriority = (ctrl) => value;
        }
        public void SetPriorityFromDistance(float maximum)
        {
            var colliders = tracker.GetComponentsInChildren<Collider>();
            computePriority = (ctrl) => maximum - ctrl.DistanceToColliderCore(colliders);
        }

        public bool isHover {
            get { return (event_sets & EEventSet.Hover) != 0; }
        }
        public bool isConcurrent {
            get { return (event_sets & EEventSet.IsConcurrent) != 0; }
            set { if (value) event_sets |= EEventSet.IsConcurrent; else event_sets &= ~EEventSet.IsConcurrent; }
        }

        internal ControllersUpdateEvent i_onControllersUpdate;
        internal ControllerEvent i_onEnter;
        internal ControllerEvent i_onMoveOver;
        internal ControllerEvent i_onLeave;
        internal ControllerEvent i_onTriggerDown;
        internal ControllerEvent i_onTriggerDrag;
        internal ControllerEvent i_onTriggerUp;
        internal ControllerEvent i_onGripDown;
        internal ControllerEvent i_onGripDrag;
        internal ControllerEvent i_onGripUp;
        internal ControllerEvent i_onMenuClick;
        internal ControllerEvent i_onTouchPressDown;
        internal ControllerEvent i_onTouchPressDrag;
        internal ControllerEvent i_onTouchPressUp;
        internal ControllerVec2Event i_onTouchScroll;
        internal ControllerEvent i_onTouchDown;
        internal ControllerEvent i_onTouchDrag;
        internal ControllerEvent i_onTouchUp;

        public event ControllersUpdateEvent onControllersUpdate {
            add { i_onControllersUpdate += value; }
            remove { i_onControllersUpdate -= value; }
        }

        public event ControllerEvent onEnter {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); i_onEnter += value; }
            remove { i_onEnter -= value; }
        }
        public event ControllerEvent onMoveOver {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); i_onMoveOver += value; }
            remove { i_onMoveOver -= value; }
        }
        public event ControllerEvent onLeave
        {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); i_onLeave += value; }
            remove { i_onLeave -= value; }
        }

        public event ControllerEvent onTriggerDown
        {
            add { i_onTriggerDown += value; event_sets |= EEventSet.Trigger; }
            remove { i_onTriggerDown -= value; check_Trigger(); }
        }
        public event ControllerEvent onTriggerDrag
        {
            add { i_onTriggerDrag += value; event_sets |= EEventSet.Trigger; }
            remove { i_onTriggerDrag -= value; check_Trigger(); }
        }
        public event ControllerEvent onTriggerUp
        {
            add { i_onTriggerUp += value; event_sets |= EEventSet.Trigger; }
            remove { i_onTriggerUp -= value; check_Trigger(); }
        }
        void check_Trigger()
        {
            if (i_onTriggerDown == null && i_onTriggerDrag == null && i_onTriggerUp == null)
                event_sets &= ~EEventSet.Trigger;
        }

        public event ControllerEvent onGripDown
        {
            add { i_onGripDown += value; event_sets |= EEventSet.Grip; }
            remove { i_onGripDown -= value; check_Grip(); }
        }
        public event ControllerEvent onGripDrag
        {
            add { i_onGripDrag += value; event_sets |= EEventSet.Grip; }
            remove { i_onGripDrag -= value; check_Grip(); }
        }
        public event ControllerEvent onGripUp
        {
            add { i_onGripUp += value; event_sets |= EEventSet.Grip; }
            remove { i_onGripUp -= value; check_Grip(); }
        }
        void check_Grip()
        {
            if (i_onGripDown == null && i_onGripDrag == null && i_onGripUp == null)
                event_sets &= ~EEventSet.Grip;
        }

        public event ControllerEvent onMenuClick
        {
            add { i_onMenuClick += value; event_sets |= EEventSet.Menu; }
            remove { i_onMenuClick -= value; check_Menu(); }
        }
        void check_Menu()
        {
            if (i_onMenuClick == null)
                event_sets &= ~EEventSet.Menu;
        }

        public event ControllerEvent onTouchPressDown
        {
            add { i_onTouchPressDown += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction1; }
            remove { i_onTouchPressDown -= value; check_Touchpad(); }
        }
        public event ControllerEvent onTouchPressDrag
        {
            add { i_onTouchPressDrag += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction1; }
            remove { i_onTouchPressDrag -= value; check_Touchpad(); }
        }
        public event ControllerEvent onTouchPressUp
        {
            add { i_onTouchPressUp += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction1; }
            remove { i_onTouchPressUp -= value; check_Touchpad(); }
        }
        public event ControllerVec2Event onTouchScroll
        {
            add { i_onTouchScroll += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction2; }
            remove { i_onTouchScroll -= value; check_Touchpad(); }
        }
        public event ControllerEvent onTouchDown
        {
            add { i_onTouchDown += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction3; }
            remove { i_onTouchDown -= value; check_Touchpad(); }
        }
        public event ControllerEvent onTouchDrag
        {
            add { i_onTouchDrag += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction3; }
            remove { i_onTouchDrag -= value; check_Touchpad(); }
        }
        public event ControllerEvent onTouchUp
        {
            add { i_onTouchUp += value; event_sets |= EEventSet.Touchpad | EEventSet.TouchpadAction3; }
            remove { i_onTouchUp -= value; check_Touchpad(); }
        }
        void check_Touchpad()
        {
            if (i_onTouchPressDown == null && i_onTouchPressDrag == null && i_onTouchPressUp == null)
                event_sets &= ~EEventSet.TouchpadAction1;
            if (i_onTouchScroll == null)
                event_sets &= ~EEventSet.TouchpadAction2;
            if (i_onTouchDown == null && i_onTouchDrag == null && i_onTouchUp == null)
                event_sets &= ~EEventSet.TouchpadAction3;
            if ((event_sets & (EEventSet.TouchpadAction1 | EEventSet.TouchpadAction2 | EEventSet.TouchpadAction3)) == 0)
                event_sets &= ~EEventSet.Touchpad;
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

        internal bool NotDead()
        {
            return tracker && tracker.isActiveAndEnabled;
        }

        internal void Call(ControllersUpdateEvent ev, Controller[] controllers)
        {
            if (ev != null && NotDead())
                try { ev(controllers); } catch (Exception e) { Report(e); }
        }
        internal void Call(ControllerEvent ev, Controller controller)
        {
            if (ev != null && NotDead())
                try { ev(controller); } catch (Exception e) { Report(e); }
        }
        internal void Call(ControllerVec2Event ev, Controller controller, Vector2 pos)
        {
            if (ev != null && NotDead())
                try { ev(controller, pos); } catch (Exception e) { Report(e); }
        }

        void Report(Exception e)
        {
            Debug.LogException(e);
        }
    }
}
