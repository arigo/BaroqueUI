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

        public BaseControllerTracker HoverControllerTracker()
        {
            return tracker_hover;
        }

        public BaseControllerTracker GrabbedControllerTracker()
        {
            return is_grabbing ? tracker_hover : null;
        }

        public void GrabFromScript(bool active)
        {
            if (active && tracker_hover != null)
            {
                grabbing_button = null;
                is_grabbing = true;
            }
            else
                is_grabbing = false;
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

        public static Controller GetController(int index) { return BaroqueUI.GetControllers()[index]; }

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

        public static void ForceLeave(BaseControllerTracker tracker)
        {
            foreach (var ctrl in BaroqueUI.GetControllersIfCreated())
            {
                if (tracker == ctrl.tracker_hover)
                {
                    if (ctrl.is_grabbing)
                        ctrl.UnGrab();
                    ctrl.LeaveNow();
                }
            }
        }


        /***************************************************************************************/


        static readonly Vector3 POS_TO_CURSOR = new Vector3(0, -0.006f, 0.056f);
        /*const*/ int BUTTON_COUNT;

        VRControllerState_t controllerState;
        uint bitmask_buttons;
        Vector3 current_position;
        Quaternion current_rotation;
        BaseControllerTracker tracker_hover;
        GameObject pointer_object, pointer_object_prefab;
        int controller_index;

        BaseControllerTracker tracker_hover_next;
        float tracker_hover_next_priority;
        bool is_tracking, is_clicking_now, is_grabbing;
        EControllerButton clicking_button;
        EControllerButton? grabbing_button;

        SteamVR_TrackedObject trackedObject;
        bool scrollWheelVisible;

        Collider[] overlapping_colliders;

        internal void Initialize(int index)
        {
            controller_index = index;
            foreach (EControllerButton button in Enum.GetValues(typeof(EControllerButton)))
                BUTTON_COUNT = Mathf.Max(BUTTON_COUNT, 1 + (int)button);

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
            /* Updates the state fields; updates 'is_tracking' and 'is_clicking_now'.
             * Invokes OverlapSphere(), but should not call user code.  This ensures
             * that OverlapSphere() will not see objects created just now, where
             * Start() has not been called yet.
             */
            is_clicking_now = false;

            var system = OpenVR.System;
            if (system == null || !isActiveAndEnabled ||
                !system.GetControllerState((uint)trackedObject.index, ref controllerState,
                                           (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t))))
            {
                bitmask_buttons = 0;
                is_tracking = false;
            }
            else
            {
                is_tracking = true;

                /* read the button state */
                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger));
                ulong pad = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong grip = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_Grip));
                ulong menu = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_ApplicationMenu));

                uint b = 0;
                if (menu != 0) b |= (1U << (int)EControllerButton.Menu);
                if (grip != 0) {
                    if (!GetButton(EControllerButton.Grip))
                    {
                        is_clicking_now = true; clicking_button = EControllerButton.Grip;
                    }
                    b |= (1U << (int)EControllerButton.Grip);
                }
                if (pad != 0) b |= (1U << (int)EControllerButton.Touchpad);
                if (trigger != 0) {
                    if (!GetButton(EControllerButton.Trigger)) {
                        is_clicking_now = true;  clicking_button = EControllerButton.Trigger;
                    }
                    b |= (1U << (int)EControllerButton.Trigger);
                }
                bitmask_buttons = b;

                overlapping_colliders = Physics.OverlapSphere(ComputePosition(), 0.02f, Physics.AllLayers,
                                                              QueryTriggerInteraction.Collide);
            }
        }

        void FindTrackerHoverNext()
        {
            /* may cause un-grabbing and so 'is_grabbing' may be reset to false;
             * sets 'tracker_hover_next'. */

            if (!is_tracking)
            {
                if (is_grabbing)
                    UnGrab();
                tracker_hover_next = null;
                ResetVelocityEstimates();
            }
            else
            {
                if (is_grabbing && grabbing_button.HasValue && !GetButton(grabbing_button.Value))
                    UnGrab();

                /* read the position/rotation and update the velocity estimation */
                current_position = ComputePosition();
                current_rotation = transform.rotation;
                UpdateVelocityEstimates();

                /* find the next BaseControllerTracker at that position, taking the highest
                 * priority one.  If is_grabbing, then keep the current one instead.
                 */
                if (is_grabbing)
                {
                    overlapping_colliders = null;
                    tracker_hover_next = tracker_hover;
                    tracker_hover_next_priority = float.PositiveInfinity;
                }
                else
                {
                    var potential = new HashSet<Transform>();

                    foreach (var coll in overlapping_colliders)
                    {
                        Transform tr = coll.transform;
                        while (tr != null)
                        {
                            if (!potential.Add(tr))
                                break;     /* already in the set */
                            tr = tr.parent;
                        }
                    }
                    overlapping_colliders = null;

                    float best_priority = float.NegativeInfinity;
                    BaseControllerTracker best = null;

                    foreach (Transform tr in potential)
                    {
                        foreach (var tracker in tr.GetComponents<BaseControllerTracker>())
                        {
                            if (tracker is ControllerTracker && !Matches((tracker as ControllerTracker).selectableControllers))
                                continue;

                            float priority = tracker.GetPriority(this);
                            if (priority == float.NegativeInfinity)
                                continue;
                            Debug.Assert(priority < float.PositiveInfinity);

                            if (priority > best_priority || (priority == best_priority && tracker.creation_order > best.creation_order))
                            {
                                best_priority = priority;
                                best = tracker;
                            }
                        }
                    }
                    tracker_hover_next = best;
                    tracker_hover_next_priority = best_priority;
                }

                /* sanity check */
                if (is_grabbing)
                    Debug.Assert(tracker_hover);
            }
        }

        public bool Matches(EControllerSelection choice)
        {
            return ((int)choice & (1 << index)) != 0;
        }

        void UnGrab()
        {
            Debug.Assert(is_grabbing);
            switch (grabbing_button)
            {
                case EControllerButton.Trigger:
                    tracker_hover.OnTriggerUp(this);
                    break;
                case EControllerButton.Grip:
                    tracker_hover.OnGripUp(this);
                    break;
            }
            is_grabbing = false;
        }

        static void ResolveControllerConflicts(Controller[] controllers)
        {
            Debug.Assert(controllers.Length == 2);   /* for now, always exactly two */
            Controller left_ctrl = controllers[0];
            Controller right_ctrl = controllers[1];
            if (left_ctrl.tracker_hover_next is ControllerTracker && left_ctrl.tracker_hover_next == right_ctrl.tracker_hover_next)
            {
                /* conflict: both controllers are inside the zone corresponding to
                 * the same, non-Concurrent, ControllerTracker */
                Controller forced_out;

                if (right_ctrl.is_clicking_now)
                    forced_out = left_ctrl;
                else if (left_ctrl.is_clicking_now)
                    forced_out = right_ctrl;
                else if (right_ctrl.is_grabbing)
                    forced_out = left_ctrl;
                else if (left_ctrl.is_grabbing)
                    forced_out = right_ctrl;
                else if (right_ctrl.tracker_hover_next_priority >= left_ctrl.tracker_hover_next_priority)
                    forced_out = left_ctrl;
                else
                    forced_out = right_ctrl;

                /* force one of the controllers "out" of the zone, and force a trigger-release if grabbing */
                if (forced_out.is_grabbing)
                    forced_out.UnGrab();
                forced_out.tracker_hover_next = null;
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
                ctrl.ReadControllerState();    /* read state, calls OverlapSphere() */

            foreach (var ctrl in controllers)
                ctrl.FindTrackerHoverNext();    /* call OnTriggerUp(), update hovers */

            ResolveControllerConflicts(controllers);

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
