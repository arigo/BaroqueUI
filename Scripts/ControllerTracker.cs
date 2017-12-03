using System;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public delegate float GetPriorityDelegate(Controller controller);
    public delegate void ControllerEvent(Controller controller);
    public delegate void ControllersUpdateEvent(Controller[] controllers);
    public delegate void ControllerVec2Event(Controller controller, Vector2 relative_pos);

    public interface IGlobalControllerTracker
    {
        GetPriorityDelegate computePriority { get; set; }
        void SetPriority(float value);
        void SetPriorityFromDistance(float maximum);
        bool isHover { get; }
        bool isConcurrent { get; set; }
        bool isActiveAndEnabled { get; }
        bool isHapticScrollEnabled { get; set; }
        event ControllersUpdateEvent onControllersUpdate;

        event ControllerEvent onTriggerDown;
        event ControllerEvent onTriggerDrag;
        event ControllerEvent onTriggerUp;

        event ControllerEvent onGripDown;
        event ControllerEvent onGripDrag;
        event ControllerEvent onGripUp;

        event ControllerEvent onMenuClick;

        event ControllerEvent onTouchPressDown;
        event ControllerEvent onTouchPressDrag;
        event ControllerEvent onTouchPressUp;
        event ControllerVec2Event onTouchScroll;
        event ControllerEvent onTouchDown;
        event ControllerEvent onTouchDrag;
        event ControllerEvent onTouchUp;
    }

    public interface IControllerTracker: IGlobalControllerTracker
    {
        event ControllerEvent onEnter;
        event ControllerEvent onMoveOver;
        event ControllerEvent onLeave;
    }

    enum EEventSet
    {
        Hover = 0x01,
        Trigger = 0x02,
        Grip = 0x04,
        Menu = 0x08,
        TouchpadAction1 = 0x20,  /* OnTouchPressDown, ... */
        TouchpadAction2 = 0x40,  /* OnTouchScroll         */
        TouchpadAction3 = 0x80,  /* OnTouchDown, ...      */
        IsConcurrent = 0x100,
        TouchScrollSilent = 0x200,
    }

    public class ControllerTracker: IControllerTracker
    {
        public readonly MonoBehaviour tracker;

        readonly int creation_order;
        EEventSet event_sets;   /* bitmask */
        static int NUMBERING = 0;

        internal EEventSet _event_sets { get { return event_sets; } }

        public ControllerTracker(MonoBehaviour tracker, bool is_hover)
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
        public bool isActiveAndEnabled {
            get { return tracker && tracker.isActiveAndEnabled; }
        }
        public bool isHapticScrollEnabled {
            get { return (event_sets & EEventSet.TouchScrollSilent) == 0; }
            set { if (!value) event_sets |= EEventSet.TouchScrollSilent; else event_sets &= ~EEventSet.TouchScrollSilent; }
        }

        internal ControllersUpdateEvent _i_onControllersUpdate;
        internal ControllerEvent _i_onEnter;
        internal ControllerEvent _i_onMoveOver;
        internal ControllerEvent _i_onLeave;
        internal ControllerEvent _i_onTriggerDown;
        internal ControllerEvent _i_onTriggerDrag;
        internal ControllerEvent _i_onTriggerUp;
        internal ControllerEvent _i_onGripDown;
        internal ControllerEvent _i_onGripDrag;
        internal ControllerEvent _i_onGripUp;
        internal ControllerEvent _i_onMenuClick;
        internal ControllerEvent _i_onTouchPressDown;
        internal ControllerEvent _i_onTouchPressDrag;
        internal ControllerEvent _i_onTouchPressUp;
        internal ControllerVec2Event _i_onTouchScroll;
        internal ControllerEvent _i_onTouchDown;
        internal ControllerEvent _i_onTouchDrag;
        internal ControllerEvent _i_onTouchUp;

        public event ControllersUpdateEvent onControllersUpdate {
            add { _i_onControllersUpdate += value; }
            remove { _i_onControllersUpdate -= value; }
        }

        public event ControllerEvent onEnter {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); _i_onEnter += value; }
            remove { _i_onEnter -= value; }
        }
        public event ControllerEvent onMoveOver {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); _i_onMoveOver += value; }
            remove { _i_onMoveOver -= value; }
        }
        public event ControllerEvent onLeave
        {
            add { Debug.Assert((event_sets & EEventSet.Hover) != 0); _i_onLeave += value; }
            remove { _i_onLeave -= value; }
        }

        public event ControllerEvent onTriggerDown
        {
            add { _i_onTriggerDown += value; event_sets |= EEventSet.Trigger; }
            remove { _i_onTriggerDown -= value; check_Trigger(); }
        }
        public event ControllerEvent onTriggerDrag
        {
            add { _i_onTriggerDrag += value; event_sets |= EEventSet.Trigger; }
            remove { _i_onTriggerDrag -= value; check_Trigger(); }
        }
        public event ControllerEvent onTriggerUp
        {
            add { _i_onTriggerUp += value; event_sets |= EEventSet.Trigger; }
            remove { _i_onTriggerUp -= value; check_Trigger(); }
        }
        void check_Trigger()
        {
            if (_i_onTriggerDown == null && _i_onTriggerDrag == null && _i_onTriggerUp == null)
                event_sets &= ~EEventSet.Trigger;
        }

        public event ControllerEvent onGripDown
        {
            add { _i_onGripDown += value; event_sets |= EEventSet.Grip; }
            remove { _i_onGripDown -= value; check_Grip(); }
        }
        public event ControllerEvent onGripDrag
        {
            add { _i_onGripDrag += value; event_sets |= EEventSet.Grip; }
            remove { _i_onGripDrag -= value; check_Grip(); }
        }
        public event ControllerEvent onGripUp
        {
            add { _i_onGripUp += value; event_sets |= EEventSet.Grip; }
            remove { _i_onGripUp -= value; check_Grip(); }
        }
        void check_Grip()
        {
            if (_i_onGripDown == null && _i_onGripDrag == null && _i_onGripUp == null)
                event_sets &= ~EEventSet.Grip;
        }

        public event ControllerEvent onMenuClick
        {
            add { _i_onMenuClick += value; event_sets |= EEventSet.Menu; }
            remove { _i_onMenuClick -= value; check_Menu(); }
        }
        void check_Menu()
        {
            if (_i_onMenuClick == null)
                event_sets &= ~EEventSet.Menu;
        }

        public event ControllerEvent onTouchPressDown
        {
            add { _i_onTouchPressDown += value; event_sets |= EEventSet.TouchpadAction1; }
            remove { _i_onTouchPressDown -= value; check_Touchpad1(); }
        }
        public event ControllerEvent onTouchPressDrag
        {
            add { _i_onTouchPressDrag += value; event_sets |= EEventSet.TouchpadAction1; }
            remove { _i_onTouchPressDrag -= value; check_Touchpad1(); }
        }
        public event ControllerEvent onTouchPressUp
        {
            add { _i_onTouchPressUp += value; event_sets |= EEventSet.TouchpadAction1; }
            remove { _i_onTouchPressUp -= value; check_Touchpad1(); }
        }
        public event ControllerVec2Event onTouchScroll
        {
            add { _i_onTouchScroll += value; event_sets |= EEventSet.TouchpadAction2; }
            remove { _i_onTouchScroll -= value; check_Touchpad2(); }
        }
        public event ControllerEvent onTouchDown
        {
            add { _i_onTouchDown += value; event_sets |= EEventSet.TouchpadAction3; }
            remove { _i_onTouchDown -= value; check_Touchpad3(); }
        }
        public event ControllerEvent onTouchDrag
        {
            add { _i_onTouchDrag += value; event_sets |= EEventSet.TouchpadAction3; }
            remove { _i_onTouchDrag -= value; check_Touchpad3(); }
        }
        public event ControllerEvent onTouchUp
        {
            add { _i_onTouchUp += value; event_sets |= EEventSet.TouchpadAction3; }
            remove { _i_onTouchUp -= value; check_Touchpad3(); }
        }
        void check_Touchpad1()
        {
            if (_i_onTouchPressDown == null && _i_onTouchPressDrag == null && _i_onTouchPressUp == null)
                event_sets &= ~EEventSet.TouchpadAction1;
        }
        void check_Touchpad2()
        {
            if (_i_onTouchScroll == null)
                event_sets &= ~EEventSet.TouchpadAction2;
        }
        void check_Touchpad3()
        {
            if (_i_onTouchDown == null && _i_onTouchDrag == null && _i_onTouchUp == null)
                event_sets &= ~EEventSet.TouchpadAction3;
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

        internal void _Call(ControllersUpdateEvent ev, Controller[] controllers)
        {
            if (ev != null && isActiveAndEnabled)
                try { ev(controllers); } catch (Exception e) { Report(e); }
        }
        internal void _Call(ControllerEvent ev, Controller controller)
        {
            if (ev != null && isActiveAndEnabled)
                try { ev(controller); } catch (Exception e) { Report(e); }
        }
        internal void _Call(ControllerVec2Event ev, Controller controller, Vector2 pos)
        {
            if (ev != null && isActiveAndEnabled)
                try { ev(controller, pos); } catch (Exception e) { Report(e); }
        }

        void Report(Exception e)
        {
            Debug.LogException(e);
        }
    }
}
