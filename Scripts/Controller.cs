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

        public MonoBehaviour HoverTracker()
        {
            return tracker_hover != null ? tracker_hover.tracker : null;
        }

        public void GrabHover(bool active)
        {
            if (tracker_hover != null)
            {
                if (active)
                    tracker_hover_lock |= MANUAL_LOCK;
                else
                    tracker_hover_lock &= ~MANUAL_LOCK;
            }
        }

        public void HapticPulse(int durationMicroSec = 500)
        {
            SteamVR_Controller.Input((int)trackedObject.index).TriggerHapticPulse((ushort)durationMicroSec);
        }

        public Transform SetPointer(string pointer_name)
        {
            if (pointer_name == "")
            {
                if (pointer_object != null)
                    Destroy(pointer_object);
                pointer_object = null;
                pointer_object_prefab = null;
                pointer_str_name = null;
                return null;
            }
            else
            {
                if (pointer_name != pointer_str_name)
                {
                    SetPointer(Resources.Load<GameObject>("Pointers/" + pointer_name));
                    pointer_str_name = pointer_name;
                }
                return pointer_object.transform;
            }
        }

        public Transform SetPointer(GameObject prefab)
        {
            if (pointer_object_prefab != prefab)
            {
                if (pointer_object != null)
                    Destroy(pointer_object);
                pointer_str_name = null;
                pointer_object_prefab = prefab;
                pointer_object = Instantiate(prefab, transform);
                pointer_object.transform.localPosition = POS_TO_CURSOR;
                pointer_object.transform.localRotation = Quaternion.identity;
            }
            return pointer_object.transform;
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

        public static Controller GetController(int index) { return Baroque.GetControllers()[index]; }
        public static Controller[] GetControllers() { return Baroque.GetControllers(); }

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

        public static void Register(MonoBehaviour tracker, float priority, bool concurrent = false)
        {
            BuildControllerTracker(tracker, (ctrl) => priority, concurrent);
        }

        public static void Register(MonoBehaviour tracker, GetPriorityDelegate get_priority = null, bool concurrent = false)
        {
            BuildControllerTracker(tracker, get_priority, concurrent);
        }


        public float DistanceToColliderCore(Collider[] colliders)
        {
            return DistanceToColliderCore(position, colliders);
        }

        public float DistanceToColliderCore(Collider coll)
        {
            return DistanceToColliderCore(position, coll);
        }

        public static float DistanceToColliderCore(Vector3 position, Collider[] colliders)
        {
            if (colliders.Length == 0)
                return 0;

            float min_dist = float.PositiveInfinity;
            foreach (var coll in colliders)
                min_dist = Mathf.Min(min_dist, DistanceToColliderCore(position, coll));
            return min_dist;
        }

        public static float DistanceToColliderCore(Vector3 position, Collider coll)
        {
            Vector3 core;

            if (coll is BoxCollider)
            {
                /* the "core" is an flat rectangle inside the box, defined as the set of points
                   that have maximal distance to the outside of the box.  The same definition
                   is actually used for SphereCollider and CapsuleCollider: for a sphere it
                   is just the central point; for a capsule it is a segment.  For a box, it
                   is a flat rectangle that might collapse into a segment or a point, depending
                   on whether the box is a perfect cube or not.
                 */
                BoxCollider bc = (BoxCollider)coll;
                Vector3 p_in = coll.transform.InverseTransformPoint(position) - bc.center;
                float emin = Mathf.Min(bc.extent.x, bc.extent.y, bc.extent.z);
                float ex = bc.extent.x - emin;
                float ey = bc.extent.y - emin;
                float ez = bc.extent.z - emin;
                if (p_in.x < -ex) p_in.x = -ex;
                if (p_in.x >  ex) p_in.x =  ex;
                if (p_in.y < -ey) p_in.y = -ey;
                if (p_in.y >  ey) p_in.y =  ey;
                if (p_in.z < -ez) p_in.z = -ez;
                if (p_in.z >  ez) p_in.z =  ez;
                core = coll.transform.TransformPoint(bc.center + p_in);
            }
            else if (coll is SphereCollider)
            {
                SphereCollider sc = (SphereCollider)coll;
                core = coll.transform.TransformPoint(sc.center);
            }
            else if (coll is CapsuleCollider)
            {
                CapsuleCollider cc = (CapsuleCollider)coll;
                core = coll.transform.TransformPoint(cc.center);

                Vector3 delta, scale1, scale2;
                switch (cc.direction)
                {
                    case 0: delta = new Vector3(1, 0, 0); scale1 = new Vector3(0, 1, 0); scale2 = new Vector3(0, 0, 1); break;
                    case 1: delta = new Vector3(0, 1, 0); scale1 = new Vector3(0, 0, 1); scale2 = new Vector3(1, 0, 0); break;
                    case 2: delta = new Vector3(0, 0, 1); scale1 = new Vector3(1, 0, 0); scale2 = new Vector3(0, 1, 0); break;
                    default: throw new NotImplementedException();
                }
                Vector3 delta_v = cc.transform.TransformVector(delta) * (cc.height * 0.5f);
                float radius = Mathf.Max(cc.transform.TransformVector(scale1).magnitude,
                                         cc.transform.TransformVector(scale2).magnitude) * cc.radius;
                float delta_v_mag = delta_v.magnitude;
                if (delta_v_mag > radius)
                {
                    delta_v *= (delta_v_mag - radius) / delta_v_mag;
                    float dot = Vector3.Dot(delta_v, position - core);
                    float sqrmag = delta_v.sqrMagnitude;
                    if (dot >= sqrmag)
                        core += delta_v;
                    else if (dot <= -sqrmag)
                        core -= delta_v;
                    else
                        core += Vector3.Project(position - core, delta_v);
                }
            }
            else
            {
                /* fall back on center of the axis-aligned bounding box (AABB) */
                core = coll.transform.TransformPoint(coll.bounds.center);
            }

            Baroque.DrawLine(core, position, Color.cyan);

            return Vector3.Distance(core, position);
        }


        /***************************************************************************************/


        static Dictionary<MonoBehaviour, ControllerTracker> all_trackers;
        static List<ControllerTracker> global_trackers;
        static int auto_free_trackers;

        static ControllerTracker BuildControllerTracker(MonoBehaviour tracker, GetPriorityDelegate get_priority, bool concurrent)
        {
            Baroque.EnsureStarted();

            ControllerTracker ct = new ControllerTracker(tracker);
            ct.AutoRegister(get_priority, concurrent);
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
        const uint MANUAL_LOCK = 0x4000;

        VRControllerState_t controllerState;
        uint bitmask_buttons, bitmask_buttons_down;
        Vector3 current_position;
        Quaternion current_rotation;
        ControllerTracker tracker_hover, active_trigger, active_grip, active_touchpad;
        uint tracker_hover_lock;   /* bitmask: MANUAL_LOCK, 1<<Trigger, 1<<Grip, 1<<Touchpad */
        GameObject pointer_object, pointer_object_prefab;
        string pointer_str_name;
        int controller_index;

        ControllerTracker tracker_hover_next;
        float tracker_hover_next_priority;
        bool is_tracking_active;

        const float ATS_NONE = 0;
        const float ATS_ACTION1 = -1;
        const float ATS_ACTION2 = -2;
        const float ATS_ACTION3 = -3;
        float active_touchpad_state;   /* if > 0, the timeout for the small delay */

        SteamVR_TrackedObject trackedObject;
        bool scrollWheelVisible;

        Dictionary<ControllerTracker, float> overlapping_trackers;
        static List<ControllerTracker> called_controllers_update;

        internal static void InitControllers()
        {
            all_trackers = new Dictionary<MonoBehaviour, ControllerTracker>();
            global_trackers = new List<ControllerTracker>();
            auto_free_trackers = 16;
            called_controllers_update = new List<ControllerTracker>();
        }

        internal void Initialize(int index)
        {
            controller_index = index;

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
            /* may cause un-grabbing (deactivating) of buttons and so 'active_*' may be reset to null;
             * sets 'tracker_hover_next'.
             */
            var system = OpenVR.System;
            if (system == null || !isActiveAndEnabled ||
                !system.GetControllerState((uint)trackedObject.index, ref controllerState,
                                           (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t))))
            {
                bitmask_buttons = 0;
                bitmask_buttons_down = 0;
                is_tracking_active = false;
                tracker_hover_next = null;
                tracker_hover_lock = 0;
                ResetVelocityEstimates();
            }
            else
            {
                /* read the button state */
                uint prev_bitmask_buttons = bitmask_buttons;

                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger));
                ulong pad = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong grip = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_Grip));
                ulong menu = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_ApplicationMenu));

                uint b = 0;
                if (menu != 0) b |= (1U << (int)EControllerButton.Menu);
                if (grip != 0) { b |= (1U << (int)EControllerButton.Grip); }
                if (pad != 0) b |= (1U << (int)EControllerButton.Touchpad);
                if (trigger != 0) { b |= (1U << (int)EControllerButton.Trigger); }
                bitmask_buttons = b;
                bitmask_buttons_down = bitmask_buttons & ~prev_bitmask_buttons;

                /* ignore trigger click when grip is down and vice-versa */
                if (grip != 0)
                    bitmask_buttons_down &= ~(1U << (int)EControllerButton.Trigger);
                if (trigger != 0)
                    bitmask_buttons_down &= ~(1U << (int)EControllerButton.Grip);

                if (!is_tracking_active)
                {
                    bitmask_buttons_down = 0;
                    is_tracking_active = true;
                }
            }

            /* un-grab */

            if (active_trigger != null && !triggerPressed)
                DeactivateTrigger();
            if (active_grip != null && !gripPressed)
                DeactivateGrip();
            if (active_touchpad != null &&
                    (!touchpadTouched || (active_touchpad_state == ATS_ACTION1 && !touchpadPressed)))
                DeactivateTouchpad();

            if (is_tracking_active)
            {
                /* read the position and rotation, and update the velocity estimates */
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
                overlapping_trackers.Clear();   /* should already be cleared, but better safe than sorry */

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

                        overlapping_trackers[ct] = priority;

                        if (ct.IsHover())
                            ct.PickIfBetter(priority, ref best, ref best_priority);
                    }
                }

                /* If tracker_hover_lock is set, we keep tracker_hover.
                 * We still do the loop above in order to get 'overlapping_trackers'.
                 */
                if (tracker_hover_lock != 0)
                {
                    Debug.Assert(tracker_hover != null);
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

        void DeactivateTrigger()
        {
            active_trigger.onTriggerUp(this);
            if (active_trigger == tracker_hover)
                tracker_hover_lock &= ~(1U << (int)EControllerButton.Trigger);
            active_trigger = null;
        }

        void DeactivateGrip()
        {
            active_grip.onGripUp(this);
            if (active_grip == tracker_hover)
                tracker_hover_lock &= ~(1U << (int)EControllerButton.Grip);
            active_grip = null;
        }

        void DeactivateTouchpad()
        {
            if (active_touchpad_state == ATS_ACTION1)
            {
                active_touchpad.onTouchpadUp(this);

            }

            if (active_touchpad == tracker_hover)
                tracker_hover_lock &= ~(1U << (int)EControllerButton.Touchpad);
            active_touchpad = null;
        }

        bool IsClickingNow()
        {
            return bitmask_buttons_down != 0;
        }

        static void ResolveControllerConflicts(Controller[] controllers)
        {
            Debug.Assert(controllers.Length == 2);   /* for now, always exactly two */
            Controller left_ctrl = controllers[0];
            Controller right_ctrl = controllers[1];
            ControllerTracker tracker = left_ctrl.tracker_hover_next;
            if (tracker != null && tracker == right_ctrl.tracker_hover_next && !tracker.IsConcurrent())
            {
                /* conflict: both controllers are inside the zone corresponding to
                 * the same, non-Concurrent, ControllerTracker */
                Controller forced_out;

                /* Heuristics, but will never pick whoever has got 'tracker_hover_lock & MANUAL_LOCK' */
                if ((right_ctrl.tracker_hover_lock & MANUAL_LOCK) != 0)
                    forced_out = left_ctrl;
                else if (left_ctrl.tracker_hover_lock != 0)
                    forced_out = right_ctrl;
                else if (right_ctrl.tracker_hover_lock != 0)
                    forced_out = left_ctrl;
                else if (right_ctrl.IsClickingNow())
                    forced_out = left_ctrl;
                else if (left_ctrl.IsClickingNow())
                    forced_out = right_ctrl;
                else if (right_ctrl.tracker_hover_next_priority > left_ctrl.tracker_hover_next_priority)
                    forced_out = left_ctrl;
                else if (right_ctrl.tracker_hover_next_priority < left_ctrl.tracker_hover_next_priority)
                    forced_out = right_ctrl;
                else if (left_ctrl.touchpadTouched)
                    forced_out = right_ctrl;
                else
                    forced_out = left_ctrl;

                /* Force one of the controllers "out" of the zone, and force a button deactivation if needed.
                 * In principle, afterwards, it is not possible that the tracker remains locked for both
                 * controllers, because tracker_hover_lock == MANUAL_LOCK can only be set if the tracker is 
                 * the current hover in the first place and this tracker cannot be current for both. */
                if (forced_out.active_trigger == tracker)
                    forced_out.DeactivateTrigger();
                if (forced_out.active_grip == tracker)
                    forced_out.DeactivateGrip();
                if (forced_out.active_touchpad == tracker)
                    forced_out.DeactivateTouchpad();

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
            foreach (var ct in called_controllers_update)
            {
                if (!left_cts.ContainsKey(ct) && !right_cts.ContainsKey(ct))
                    ct.onControllersUpdate(new Controller[0]);
            }
            called_controllers_update = cts;

            Controller[] active_controllers = Array.FindAll(controllers, (ctrl) => ctrl.is_tracking_active);
            foreach (var gt in global_trackers)
                gt.onControllersUpdate(active_controllers);
        }

        void CallControllerMove()
        {
            if (active_trigger != null)
                active_trigger.onTriggerDrag(this);
            if (active_grip != null)
                active_grip.onGripDrag(this);
            if (active_touchpad != null)
                active_touchpad.onTouchpadDrag(this);

            if (tracker_hover != null && ((tracker_hover_lock & ~MANUAL_LOCK) == 0))
                tracker_hover.onMoveOver(this);
        }

        void LeaveNow()
        {
            ControllerTracker prev = tracker_hover;
            tracker_hover = null;
            tracker_hover_lock = 0;
            prev.onLeave(this);
            SetPointer("");
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
                tracker_hover = tracker_hover_next;
                tracker_hover.onEnter(this);
            }
            Debug.Assert(tracker_hover == tracker_hover_next);
        }

        bool ActiveWith(ControllerTracker ct)
        {
            return (ct == tracker_hover || ct == active_trigger || ct == active_grip || ct == active_touchpad);
        }

        void HandleButtonDown(EControllerButton btn, EEventSet event_set, ref ControllerTracker active_tracker)
        {
            if ((bitmask_buttons_down & (1U << (int)btn)) == 0)
                return;      /* not pressing this button right now */

            /* does 'tracker_hover' handle the event_set? */
            ControllerTracker best = null;
            if (tracker_hover != null && (tracker_hover.event_sets & event_set) != 0)
            {
                best = tracker_hover;
            }
            else
            {
                /* no, pick the best-priority one among the trackers we touch which
                 * do NOT implement hovering */
                float best_priority = float.NegativeInfinity;

                foreach (var kv in overlapping_trackers)
                {
                    ControllerTracker ct = kv.Key;
                    float priority = kv.Value;

                    if (!ct.IsHover() && (ct.event_sets & event_set) != 0)
                        ct.PickIfBetter(priority, ref best, ref best_priority);
                }

                /* if not found, then look for global trackers */
                if (best == null)
                {
                    foreach (var ct in global_trackers)
                    {
                        if ((ct.event_sets & event_set) != 0)
                            if (ct.tracker && ct.tracker.isActiveAndEnabled)
                            {
                                float priority = ct.get_priority(this);
                                if (priority != float.NegativeInfinity)
                                    ct.PickIfBetter(priority, ref best, ref best_priority);
                            }
                    }
                    if (best == null)
                        return;       /* still not found, ignore the button click */
                }

                /* If it's a non-concurrent tracker and it is used by the other controller, cancel */
                if (!best.IsConcurrent())
                {
                    foreach (var ctrl in Baroque.GetControllers())
                        if (ctrl != this && ctrl.ActiveWith(best))
                            return;
                }
            }

            if (active_tracker != null)
                return;      /* should not occur, but you never know */
            active_tracker = best;

            if (best == tracker_hover && btn != EControllerButton.Menu)
                tracker_hover_lock |= 1U << (int)btn;

            switch (btn)
            {
                case EControllerButton.Trigger:  best.onTriggerDown(this);  break;
                case EControllerButton.Grip:     best.onGripDown(this);     break;
                case EControllerButton.Touchpad: best.onTouchpadDown(this); break;
                case EControllerButton.Menu:     best.onMenuClick(this);    break;
            }
        }

        void CallButtonDown()
        {
            if (bitmask_buttons_down != 0)
            {
                HandleButtonDown(EControllerButton.Trigger, EEventSet.Trigger, ref active_trigger);
                HandleButtonDown(EControllerButton.Grip, EEventSet.Grip, ref active_grip);
                HandleButtonDown(EControllerButton.Touchpad, EEventSet.Touchpad, ref active_touchpad);

                ControllerTracker active_menu = null;
                HandleButtonDown(EControllerButton.Menu, EEventSet.Menu, ref active_menu);
            }
            overlapping_trackers.Clear();
        }

        static internal void UpdateAllControllers(Controller[] controllers)
        {
            foreach (var ctrl in controllers)
                ctrl.ReadControllerState();    /* read state, calls OverlapSphere(), calls OnXxxUp(), update hovers */

            ResolveControllerConflicts(controllers);

            CallControllersUpdate(controllers);   /* calls OnControllerUpdates() */

            foreach (var ctrl in controllers)
                ctrl.CallLeaveEvents();    /* calls OnLeave() */

            foreach (var ctrl in controllers)
                ctrl.CallEnterEvents();    /* calls OnEnter() */

            foreach (var ctrl in controllers)
                ctrl.CallButtonDown();     /* calls OnXxxDown() */

            foreach (var ctrl in controllers)
                ctrl.CallControllerMove();    /* calls OnMoveOver/OnXxxDrag */
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
