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

        public bool IsGrabbingWith(EControllerButton btn)
        {
            return is_grabbing && grabbing_button == btn;
        }

        public int index { get { return controller_index; } }

        public static Controller GetController(int index) { return BaroqueUI.GetControllers()[index]; }


        /***************************************************************************************/


        static readonly Vector3 POS_TO_CURSOR = new Vector3(0, -0.006f, 0.056f);
        /*const*/ int BUTTON_COUNT;

        VRControllerState_t controllerState;
        uint bitmask_buttons;
        Vector3 current_position;
        Quaternion current_rotation;
        BaseControllerTracker tracker_hover;
        int controller_index;

        BaseControllerTracker tracker_hover_next;
        float tracker_hover_next_priority;
        bool is_tracking, is_clicking_now, is_grabbing;
        EControllerButton clicking_button, grabbing_button;

        SteamVR_TrackedObject trackedObject;

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

        void ReadControllerState()
        {
            /* updates the state fields; updates 'is_tracking' and 'is_clicking_now';
             * may cause un-grabbing and so 'is_grabbing' may be reset to false;
             * sets 'tracker_hover_next'. */

            is_clicking_now = false;

            var system = OpenVR.System;
            if (system == null || !isActiveAndEnabled ||
                !system.GetControllerState((uint)trackedObject.index, ref controllerState,
                                           (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t))))
            {
                tracker_hover_next = null;
                bitmask_buttons = 0;

                if (is_tracking)
                {
                    if (is_grabbing)
                        UnGrab();
                    is_tracking = false;
                    ResetVelocityEstimates();
                }
            }
            else
            {
                is_tracking = true;

                /* read the button state */
                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger));
                ulong touch = controllerState.ulButtonTouched & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
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

                if (is_grabbing && (trigger == 0))
                    UnGrab();

                /* read the position/rotation and update the velocity estimation */
                current_position = transform.position + transform.rotation * POS_TO_CURSOR;
                current_rotation = transform.rotation;
                UpdateVelocityEstimates();

                /* find the next BaseControllerTracker at that position, taking the highest
                 * priority one.  If is_grabbing, then keep the current one instead.
                 */
                if (is_grabbing)
                {
                    tracker_hover_next = tracker_hover;
                    tracker_hover_next_priority = float.PositiveInfinity;
                }
                else
                {
                    Collider[] lst = Physics.OverlapSphere(current_position, 0.02f, Physics.AllLayers,
                                                           QueryTriggerInteraction.Collide);
                    var potential = new HashSet<Transform>();

                    foreach (var coll in lst)
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
            BaseControllerTracker left_tracker = controllers[0].tracker_hover_next;
            BaseControllerTracker right_tracker = controllers[1].tracker_hover_next;

            if (left_tracker == right_tracker)
            {
                if (left_tracker == null)
                    return;     /* both controllers are nowhere */

                if (left_tracker is ConcurrentControllerTracker)
                {
                    /* both controllers are over the same ConcurrentControllerTracker */
                    (left_tracker as ConcurrentControllerTracker).OnMove(controllers);
                    return;
                }
            }

            foreach (var ctrl in controllers)
            {
                BaseControllerTracker tracker = ctrl.tracker_hover_next;
                if (tracker is ControllerTracker)
                {
                    if (ctrl.is_grabbing)
                        switch (ctrl.grabbing_button)
                        {
                            case EControllerButton.Trigger:
                                (tracker as ControllerTracker).OnTriggerDrag(ctrl);
                                break;
                            case EControllerButton.Grip:
                                (tracker as ControllerTracker).OnGripDrag(ctrl);
                                break;
                        }
                    else
                        (tracker as ControllerTracker).OnMoveOver(ctrl);
                }
                else if (tracker is ConcurrentControllerTracker)
                {
                    (tracker as ConcurrentControllerTracker).OnMove(new Controller[] { ctrl });
                }
            }
        }

        void CallLeaveEvents()
        {
            if (tracker_hover != null && tracker_hover != tracker_hover_next)
            {
                BaseControllerTracker prev = tracker_hover;
                tracker_hover = null;
                prev.OnLeave(this);
            }
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
                ctrl.ReadControllerState();    /* read state, calls OnTriggerUp() */

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
    }
}
