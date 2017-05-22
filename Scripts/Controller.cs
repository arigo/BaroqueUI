using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Valve.VR;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif


namespace BaroqueUI
{
    public enum EControllerButton
    {
        Trigger, Touchpad, Grip, Menu
    }


    public class Controller : MonoBehaviour
    {
        /* Public information properties, giving a snapshot of the controller's state.
         * The position returned here is the "pointer position", which is a bit further in front,
         * where the pointer icons show up.
         */
        public Vector3 position { get { return current_position; } }
        public Quaternion rotation { get { return current_rotation; } }
        public Vector3 forward { get { return current_rotation * Vector3.forward; } }
        public Vector3 right { get { return current_rotation * Vector3.right; } }
        public Vector3 up { get { return current_rotation * Vector3.up; } }
        public Vector3 velocity { get { return DampingEstimateVelocity(); } }
        public Vector3 angularVelocity { get { return DampingEstimateAngularVelocity(); } }

        public bool triggerPressed { get { return GetButton(EControllerButton.Trigger); } }
        public bool touchpadPressed { get { return GetButton(EControllerButton.Touchpad); } }
        public bool gripPressed { get { return GetButton(EControllerButton.Grip); } }
        public bool menuPressed { get { return GetButton(EControllerButton.Menu); } }

        public bool touchpadTouched { get { return (controllerState.ulButtonTouched & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad))) != 0; } }
        public Vector2 touchpadPosition { get { return new Vector2(controllerState.rAxis0.x, controllerState.rAxis0.y); } }

        public bool GetButton(EControllerButton btn)
        {
            return (bitmask_buttons & (1U << (int)btn)) != 0;
        }

        public MonoBehaviour HoverControllerTracker()
        {
            return tracker_hover != null ? tracker_hover.tracker : null;
        }

        public void HapticPulse(int durationMicroSec = 500)
        {
            SteamVR_Controller.Input((int)trackedObject.index).TriggerHapticPulse((ushort)durationMicroSec);
        }

        public GameObject SetPointer(string pointer_name)
        {
            return SetPointerPrefab(pointer_name != null ? Resources.Load<GameObject>("Pointers/" + pointer_name) : null);
        }

        public GameObject SetPointerPrefab(GameObject prefab)
        {
            if (pointer_object_prefab != prefab)
            {
                if (pointer_object != null)
                    Destroy(pointer_object);

                if (prefab == null)
                    pointer_object = null;
                else
                {
                    pointer_object = Instantiate(prefab, transform);
                    pointer_object.transform.localPosition = POS_TO_CURSOR;
                    pointer_object.transform.localRotation = Quaternion.identity;
                }
                pointer_object_prefab = prefab;
            }
            return pointer_object;
        }

        public void SetScrollWheel(bool visible)
        {
            scrollWheelVisible = visible;
            UpdateScrollWheel();
        }

        public void SetControllerHints(string trigger = null, string grip = null, 
            string touchpadTouched = null, string touchpadPressed = null, string menu = null)
        {
            SetControllerHint(ref triggerHint, trigger);
            SetControllerHint(ref gripHint, grip);
            SetControllerHint(ref touchpadTouchedHint, touchpadTouched);
            SetControllerHint(ref touchpadPressedHint, touchpadPressed);
            SetControllerHint(ref menuHint, menu);
        }

        public int index { get { return controller_index; } }

        public static Controller GetController(int index) { return BaroqueUIMain.GetControllers()[index]; }

        public T GetAdditionalData<T>(ref T[] locals) where T: new()
        {
            int index = controller_index;
            int length = locals == null ? 0 : locals.Length;
            while (length <= index)
            {
                Array.Resize<T>(ref locals, length + 1);
                locals[length] = new T();
                length += 1;
            }
            return locals[index];
        }

        public static void Register(MonoBehaviour tracker)
        {
            foreach (var ctrl in BaroqueUIMain.GetControllers())
                ctrl.GetOrBuildControllerTracker(tracker);
        }

        /*public static void ForceLeave(BaseControllerTracker tracker)
        {
            foreach (var ctrl in BaroqueUIMain.GetControllers())
            {
                if (tracker == ctrl.tracker_hover)
                {
                    if (ctrl.is_grabbing)
                        ctrl.UnGrab();
                    ctrl.LeaveNow();
                }
            }
        }*/


        /***************************************************************************************/


        Dictionary<MonoBehaviour, ControllerTracker> all_trackers;
        List<ControllerTracker> global_trackers;
        int auto_free_trackers;

        ControllerTracker GetOrBuildControllerTracker(MonoBehaviour tracker)
        {
            ControllerTracker ct;
            if (all_trackers.TryGetValue(tracker, out ct))
                return ct;
            ct = new ControllerTracker(this, tracker);
            ct.AutoRegister();
            all_trackers[tracker] = ct;
            if (ct.IsGlobal())
                global_trackers.Add(ct);

            if (--auto_free_trackers <= 0)
            {
                var new_trackers = new Dictionary<MonoBehaviour, ControllerTracker>();
                var new_global_trackers = new List<ControllerTracker>();
                foreach (var kv in all_trackers)
                {
                    if (kv.Key != null)    // meaning 'has not been freed'
                    {
                        new_trackers[kv.Key] = kv.Value;
                        if (kv.Value.IsGlobal())
                            new_global_trackers.Add(kv.Value);
                    }
                }
                all_trackers = new_trackers;
                global_trackers = new_global_trackers;
                auto_free_trackers = all_trackers.Count / 2 + 16;
            }

            return ct;
        }


        /***************************************************************************************/


        static readonly Vector3 POS_TO_CURSOR = new Vector3(0, -0.006f, 0.056f);
        const int TOUCHPAD_TOUCHED = 0x10000;

        VRControllerState_t controllerState;
        uint bitmask_buttons, prev_bitmask_buttons;
        Vector3 current_position;
        Quaternion current_rotation;
        ControllerTracker tracker_hover, grabbed_trigger, grabbed_grip, grabbed_touchpad;
        GameObject pointer_object, pointer_object_prefab;
        int controller_index;

        ControllerTracker tracker_hover_next;
        float tracker_hover_next_priority;
        bool is_tracking_active;
        EControllerButton clicking_button;

        SteamVR_TrackedObject trackedObject;
        bool scrollWheelVisible;

        Dictionary<ControllerTracker, float> overlapping_trackers;
        static List<ControllerTracker> called_controllers_update;

        internal void Initialize(int index)
        {
            controller_index = index;
            all_trackers = new Dictionary<MonoBehaviour, ControllerTracker>();
            global_trackers = new List<ControllerTracker>();
            auto_free_trackers = 16;
            overlapping_trackers = new Dictionary<ControllerTracker, float>();

            trackedObject = GetComponent<SteamVR_TrackedObject>();
            if (trackedObject == null)
                throw new MissingComponentException("'[CameraRig]/" + name + "' gameobject is missing a SteamVR_TrackedObject component");
            ResetVelocityEstimates();
        }

        Vector3 ComputePosition()
        {
            return transform.position + transform.rotation * POS_TO_CURSOR;
        }

        void ReadControllerState()
        {
            /* Updates the state fields; updates 'is_tracking' and 'bitmask_buttons';
            /* may cause un-grabbing and so 'grabbed_*' may be reset to null;
             * sets 'tracker_hover_next'.
             */
            prev_bitmask_buttons = bitmask_buttons;

            var system = OpenVR.System;
            if (system == null || !isActiveAndEnabled ||
                !system.GetControllerState((uint)trackedObject.index, ref controllerState,
                                           (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t))))
            {
                bitmask_buttons = 0;
                is_tracking_active = false;
                tracker_hover_next = null;
                ResetVelocityEstimates();
            }
            else
            {
                /* read the button state */
                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger));
                ulong pad = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong pad_touch = controllerState.ulButtonTouched & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong grip = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_Grip));
                ulong menu = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_ApplicationMenu));

                uint b = 0;
                if (menu != 0) b |= (1U << (int)EControllerButton.Menu);
                if (grip != 0) { b |= (1U << (int)EControllerButton.Grip); }
                if (pad != 0) b |= (1U << (int)EControllerButton.Touchpad);
                if (pad_touch != 0) b |= TOUCHPAD_TOUCHED;
                if (trigger != 0) { b |= (1U << (int)EControllerButton.Trigger); }
                bitmask_buttons = b;

                if (!is_tracking_active)
                {
                    prev_bitmask_buttons = bitmask_buttons;
                    is_tracking_active = true;
                }
            }

            /* un-grab */

            if (grabbed_trigger != null && !triggerPressed)
                UnGrabTrigger();
            if (grabbed_grip != null && !gripPressed)
                UnGrabGrip();
            if (grabbed_touchpad != null && !touchpadPressed)
                UnGrabTouchpad();

            if (is_tracking_active)
            {
                /* read the position/rotation and update the velocity estimation */
                current_position = ComputePosition();
                current_rotation = transform.rotation;
                UpdateVelocityEstimates();

                /* find the next BaseControllerTracker at that position, taking the highest
                 * priority one. */
                var potential = new HashSet<Transform>();

                foreach (var coll in Physics.OverlapSphere(current_position, 0.02f, Physics.AllLayers,
                                                           QueryTriggerInteraction.Collide))
                {
                    Transform tr = coll.transform;
                    while (tr != null)
                    {
                        if (!potential.Add(tr))
                            break;     /* already in the set */
                        tr = tr.parent;
                    }
                }

                float best_priority = float.NegativeInfinity;
                ControllerTracker best = null;

                foreach (Transform tr in potential)
                {
                    foreach (var tracker in tr.GetComponents<MonoBehaviour>())
                    {
                        ControllerTracker ct;
                        if (!all_trackers.TryGetValue(tracker, out ct))
                            continue;

                        float priority = ct.get_priority(this);
                        if (priority == float.NegativeInfinity)
                            continue;
                        Debug.Assert(priority < float.PositiveInfinity);

                        overlapping_trackers[ct] = priority;

                        if (priority > best_priority || (priority == best_priority && ct.creation_order > best.creation_order))
                        {
                            best_priority = priority;
                            best = ct;
                        }
                    }
                }

                /* If tracker_hover is also one of the 'grabbed_*', then keep it instead.
                 * We still do the loop above in order to get 'overlapping_trackers'.
                 */
                if (tracker_hover != null && 
                    (tracker_hover == grabbed_trigger || tracker_hover == grabbed_grip || tracker_hover == grabbed_touchpad))
                {
                    tracker_hover_next = tracker_hover;
                    tracker_hover_next_priority = float.PositiveInfinity;
                }
                else
                {
                    tracker_hover_next = best;
                    tracker_hover_next_priority = best_priority;
                }
            }
        }

        void UnGrabTrigger()
        {
            grabbed_trigger.onTriggerUp(this);
            grabbed_trigger = null;
        }

        void UnGrabGrip()
        {
            grabbed_grip.onGripUp(this);
            grabbed_grip = null;
        }

        void UnGrabTouchpad()
        {
            grabbed_touchpad.onTouchpadUp(this);
            grabbed_touchpad = null;
        }

        bool IsClickingNow()
        {
            return (bitmask_buttons & ~prev_bitmask_buttons & ~TOUCHPAD_TOUCHED) != 0;
        }

        static void ResolveControllerConflicts(Controller[] controllers)
        {
            Debug.Assert(controllers.Length == 2);   /* for now, always exactly two */
            Controller left_ctrl = controllers[0];
            Controller right_ctrl = controllers[1];
            ControllerTracker tracker = left_ctrl.tracker_hover_next;
            if (tracker != null && tracker == right_ctrl.tracker_hover_next && (tracker.event_sets & EEventSet.HoverConcurrent) == 0)
            {
                /* conflict: both controllers are inside the zone corresponding to
                 * the same, non-Concurrent, ControllerTracker */
                Controller forced_out;

                if (right_ctrl.IsClickingNow())
                    forced_out = left_ctrl;
                else if (left_ctrl.IsClickingNow())
                    forced_out = right_ctrl;
                else if (right_ctrl.tracker_hover_next_priority > left_ctrl.tracker_hover_next_priority)
                    forced_out = left_ctrl;
                else if (right_ctrl.tracker_hover_next_priority < left_ctrl.tracker_hover_next_priority)
                    forced_out = right_ctrl;
                else if ((left_ctrl.bitmask_buttons & TOUCHPAD_TOUCHED) != 0)
                    forced_out = right_ctrl;
                else
                    forced_out = left_ctrl;

                /* force one of the controllers "out" of the zone, and force a trigger-release if grabbing */
                if (forced_out.grabbed_trigger == tracker)
                    forced_out.UnGrabTrigger();
                if (forced_out.grabbed_grip == tracker)
                    forced_out.UnGrabGrip();
                if (forced_out.grabbed_touchpad == tracker)
                    forced_out.UnGrabTouchpad();

                forced_out.tracker_hover_next = null;
            }
        }

        static void CallControllersUpdate(Controller[] controllers)
        {
            Debug.Assert(controllers.Length == 2);   /* for now, always exactly two */
            Dictionary<ControllerTracker, float> left_cts, right_cts;
            left_cts = controllers[0].overlapping_trackers;
            right_cts = controllers[1].overlapping_trackers;

            var cts = new List<ControllerTracker>(right_cts.Keys);
            foreach (var ct in left_cts.Keys)
                if (!right_cts.ContainsKey(ct))
                    cts.Add(ct);

            cts.Sort((ct2, ct1) =>   // reverse sorting by priority
            {
                float val1l, val1r, val2l, val2r;
                if (!left_cts.TryGetValue(ct1, out val1l)) val1l = float.NegativeInfinity;
                if (!right_cts.TryGetValue(ct1, out val1r)) val1r = float.NegativeInfinity;
                if (!left_cts.TryGetValue(ct2, out val2l)) val2l = float.NegativeInfinity;
                if (!right_cts.TryGetValue(ct2, out val2r)) val2r = float.NegativeInfinity;
                return Mathf.Max(val1l, val1r).CompareTo(Mathf.Max(val2l, val2r));
            });

            foreach (var ct in cts)
            {
                Controller[] ctrls = Array.FindAll(controllers, (ctrl) => ctrl.overlapping_trackers.ContainsKey(ct));
                ct.onControllersUpdate(ctrls);
            }
            foreach (var ctrl in controllers)
                ctrl.overlapping_trackers.Clear();

            foreach (var ct in called_controllers_update)
            {
                if (!left_cts.ContainsKey(ct) && !right_cts.ContainsKey(ct))
                    ct.onControllersUpdate(new Controller[0]);
            }
            called_controllers_update = cts;

            Controller[] active_controllers = Array.FindAll(controllers, (ctrl) => ctrl.is_tracking_active);
            foreach (var ctrl in controllers)
            {
                foreach (var gt in ctrl.global_trackers)
                    gt.onControllersUpdate(active_controllers);
            }
        }

        static void CallControllerMoves(Controller[] controllers)
        {
            Debug.Assert(controllers.Length == 2);   /* for now, always exactly two */
            BaseControllerTracker left_tracker = controllers[0].tracker_hover;
            BaseControllerTracker right_tracker = controllers[1].tracker_hover;

            if (left_tracker == right_tracker)
            {
                if (left_tracker == null)
                    return;     /* both controllers are nowhere */

                if (left_tracker is ConcurrentControllerTracker)
                {
                    /* both controllers are over the same ConcurrentControllerTracker */
                    (left_tracker as ConcurrentControllerTracker).OnMove(FilterNotLeaving(controllers));
                    return;
                }
            }

            foreach (var ctrl in controllers)
            {
                BaseControllerTracker tracker = ctrl.tracker_hover;
                if (tracker is ControllerTracker)
                {
                    switch (ctrl.is_grabbing ? ctrl.grabbing_button : null)
                    {
                        case EControllerButton.Trigger:
                            (tracker as ControllerTracker).OnTriggerDrag(ctrl);
                            break;
                        case EControllerButton.Grip:
                            (tracker as ControllerTracker).OnGripDrag(ctrl);
                            break;
                        default:
                            (tracker as ControllerTracker).OnMoveOver(ctrl);
                            break;
                    }
                }
                else if (tracker is ConcurrentControllerTracker)
                {
                    (tracker as ConcurrentControllerTracker).OnMove(FilterNotLeaving(new Controller[] { ctrl }));
                }
            }
        }

        static Controller[] FilterNotLeaving(Controller[] controllers)
        {
            return Array.FindAll(controllers, c => c.tracker_hover == c.tracker_hover_next);
        }

        void LeaveNow()
        {
            BaseControllerTracker prev = tracker_hover;
            tracker_hover = null;
            prev.OnLeave(this);
            SetPointer(null);
        }

        void CallLeaveEvents()
        {
            if (tracker_hover != null && tracker_hover != tracker_hover_next)
                LeaveNow();
        }

        void CallEnterEvents()
        {
            if (tracker_hover_next == null)
                return;

            if (tracker_hover == null)
            {
                tracker_hover_next.OnEnter(this);
                tracker_hover = tracker_hover_next;
            }
            Debug.Assert(tracker_hover == tracker_hover_next);

            if (is_clicking_now && !is_grabbing)
            {
                switch (clicking_button)
                {
                    case EControllerButton.Trigger:
                        tracker_hover.OnTriggerDown(this);
                        break;
                    case EControllerButton.Grip:
                        tracker_hover.OnGripDown(this);
                        break;
                }
                grabbing_button = clicking_button;
                is_grabbing = true;
            }
        }

        static internal void UpdateAllControllers(Controller[] controllers)
        {
            foreach (var ctrl in controllers)
                ctrl.ReadControllerState();    /* read state, calls OverlapSphere(), calls UnGrabXxx(), update hovers */

            ResolveControllerConflicts(controllers);

            CallControllersUpdate(controllers);   /* calls OnControllerUpdates() */

            CallControllerMoves(controllers);   /* calls OnMoveOver/OnTriggerDrag/OnMove */

            foreach (var ctrl in controllers)
                ctrl.CallLeaveEvents();    /* calls OnLeave() */

            foreach (var ctrl in controllers)
                ctrl.CallEnterEvents();    /* calls OnEnter/OnTriggerDown */
        }


        /*****************************************************************************************/

        const int DAMP_VELOCITY = 4;

        struct PrevLocation { internal float time; internal Vector3 position; internal Quaternion rotation; };
        PrevLocation[] damped;

        void ResetVelocityEstimates()
        {
            damped = new PrevLocation[DAMP_VELOCITY + 1];
            for (int i = 0; i <= DAMP_VELOCITY; i++)
                damped[i].time = float.NegativeInfinity;
        }

        void UpdateVelocityEstimates()
        {
            Array.Copy(damped, 1, damped, 0, DAMP_VELOCITY);
            damped[DAMP_VELOCITY].time = Time.time;
            damped[DAMP_VELOCITY].position = current_position;
            damped[DAMP_VELOCITY].rotation = current_rotation;
        }

        Vector3 DampingEstimateVelocity()
        {
            float minSqr = float.PositiveInfinity;
            Vector3 result = Vector3.zero;
            float current_time = damped[DAMP_VELOCITY].time;

            for (int i = 0; i < DAMP_VELOCITY; i++)
            {
                Vector3 v = (damped[DAMP_VELOCITY].position - damped[i].position) / (current_time - damped[i].time);
                float sqr = v.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    result = v;
                }
            }
            return result;
        }

        Vector3 DampingEstimateAngularVelocity()
        {
            float minSqr = float.PositiveInfinity;
            float current_time = damped[DAMP_VELOCITY].time;
            Quaternion inverse_rotation = Quaternion.Inverse(damped[DAMP_VELOCITY].rotation);
            Vector3 result = Vector3.zero;

            for (int i = 0; i < DAMP_VELOCITY; i++)
            {
                Quaternion q = damped[i].rotation * inverse_rotation;
                float angleInDegrees;
                Vector3 rotationAxis;
                q.ToAngleAxis(out angleInDegrees, out rotationAxis);

                Vector3 angularDisplacement = rotationAxis * angleInDegrees * Mathf.Deg2Rad;
                Vector3 v = angularDisplacement / (current_time - damped[i].time);

                float sqr = v.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    result = v;
                }
            }
            return -result;
        }

        void UpdateScrollWheel()
        {
            Transform tr1 = transform.Find("Model/scroll_wheel");
            Transform tr2 = transform.Find("Model/trackpad_scroll_cut");
            Transform tr3 = transform.Find("Model/trackpad");
            if (tr1 != null) tr1.gameObject.SetActive(scrollWheelVisible);
            if (tr2 != null) tr2.gameObject.SetActive(scrollWheelVisible);
            if (tr3 != null) tr3.gameObject.SetActive(!scrollWheelVisible);
        }

        const float HINT_DELAY = 0.75f;
        const float HINT_MAX_VELOCITY = 0.5f;

        void LateUpdate()
        {
            /* haaaack: we need to constantly re-enable the scroll wheel gameobjects in 
             * the Model.  Officially there must be a way to enable the scroll wheel,
             * but I can't find it for now... */
            if (scrollWheelVisible)
                UpdateScrollWheel();

            bool too_fast = DampingEstimateVelocity().sqrMagnitude > HINT_MAX_VELOCITY * HINT_MAX_VELOCITY;
            UpdateControllerHint(ref triggerHint, "Trigger", too_fast);
            UpdateControllerHint(ref gripHint, "Grip", too_fast);
            UpdateControllerHint(ref touchpadTouchedHint, "TouchpadTouched", too_fast);
            UpdateControllerHint(ref touchpadPressedHint, "TouchpadPressed", too_fast);
            UpdateControllerHint(ref menuHint, "Menu", too_fast);
        }

        struct Hint
        {
            public string text;
            public GameObject gobj;
            public UnityEngine.UI.Text tobj;
        }

        Hint triggerHint, gripHint, touchpadTouchedHint, touchpadPressedHint, menuHint;
        public float hintsShowAt;

        void SetControllerHint(ref Hint hint, string text)
        {
            if (text == "")
                text = null;
            if (hint.text == text)
                return;

            hint.text = text;

            if (text == null)
            {
                if (hint.gobj != null && hint.gobj)
                    Destroy(hint.gobj);
                hint.gobj = null;
                return;
            }
            else
            {
                hintsShowAt = Time.time + HINT_DELAY;
            }
        }

        void UpdateControllerHint(ref Hint hint, string kind, bool too_fast)
        {
            if (hint.text == null)
                return;

            if (too_fast)
            {
                if (hint.gobj != null && hint.gobj)
                    Destroy(hint.gobj);
                hint.gobj = null;
                hintsShowAt = Time.time + HINT_DELAY;
            }
            else
            {
                if (hint.gobj == null || !hint.gobj)
                {
                    if (Time.time < hintsShowAt)
                        return;

                    GameObject prefab = Resources.Load<GameObject>("BaroqueUI/ControllerTextHint" + kind);
                    hint.gobj = Instantiate(prefab, transform, instantiateInWorldSpace: false) as GameObject;
                    hint.tobj = hint.gobj.transform.GetComponentInChildren<UnityEngine.UI.Text>();
                }
                if (hint.tobj.text != hint.text)
                    hint.tobj.text = hint.text;
            }
        }
    }
}
