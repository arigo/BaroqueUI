using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{

    public enum EControllerButton
    {
        Trigger, Touchpad, Grip, Menu
    }


    public struct ControllerSnapshot
    {
        public BaroqueUI_Controller controller;
        internal uint _buttons;
        public Vector2? touchpadPosition;   /* null if not touching the touchpad */

        public bool GetButton(EControllerButton btn)
        {
            return (_buttons & (1U << (int)btn)) != 0;
        }

        public GameObject ThisControllerObject()
        {
            return controller.gameObject;
        }

        public GameObject OtherControllerObject()
        {
            return controller.GetOtherController().gameObject;
        }

        public GameObject HeadObject()
        {
            return controller.GetHead().gameObject;
        }
    }


    public delegate void HoverDelegate(ControllerAction action, ControllerSnapshot snapshot);

    public class Hover : IComparable<Hover>
    {
        public float reversed_priority;    /* smaller values have higher priority */
        public int CompareTo(Hover other) { return reversed_priority.CompareTo(other.reversed_priority); }

        public Hover() { }
        public Hover(float reversed_priority) { this.reversed_priority = reversed_priority; }

        public static bool IsBetterHover(Hover hov1, Hover hov2)
        {
            if (hov1 == null)
                return false;
            else if (hov2 == null)
                return true;
            else
            {
                //Debug.LogFormat("IsBetterHover: {0} -- {1}", hov1.reversed_priority, hov2.reversed_priority);
                return hov2.CompareTo(hov1) > 0;
            }
        }

        public virtual void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot) { }
        public virtual void OnButtonOver(ControllerAction action, ControllerSnapshot snapshot) { }
        public virtual void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot) { }
        public virtual void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot) { }
        public virtual void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot) { }
        public virtual void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot) { }
    }

    public class DelegatingHover : Hover
    {
        public HoverDelegate buttonEnter, buttonOver, buttonLeave, buttonDown, buttonDrag, buttonUp;

        public DelegatingHover(float reversed_priority=0,
                               HoverDelegate buttonEnter=null, HoverDelegate buttonOver=null, HoverDelegate buttonLeave=null,
                               HoverDelegate buttonDown=null, HoverDelegate buttonDrag=null, HoverDelegate buttonUp=null)
            : base(reversed_priority)
        {
            this.buttonEnter = buttonEnter;
            this.buttonOver  = buttonOver;
            this.buttonLeave = buttonLeave;
            this.buttonDown  = buttonDown;
            this.buttonDrag  = buttonDrag;
            this.buttonUp    = buttonUp;
        }

        public Hover FindHover(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (action.IsPressingButton(snapshot) || buttonEnter != null || buttonOver != null || buttonLeave != null)
                return this;
            return null;
        }

        public override void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonEnter != null)
                buttonEnter(action, snapshot);
        }
        public override void OnButtonOver(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonOver != null)
                buttonOver(action, snapshot);
        }
        public override void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonLeave != null)
                buttonLeave(action, snapshot);
        }
        public override void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonDown != null)
                buttonDown(action, snapshot);
        }
        public override void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonDrag != null)
                buttonDrag(action, snapshot);
        }
        public override void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
        {
            if (buttonUp != null)
                buttonUp(action, snapshot);
        }
    }


    public abstract class ControllerAction : MonoBehaviour
    {
        public EControllerButton controllerButton;
        public BaroqueUI_Controller controller { get; private set; }

        void Awake()
        {
            controller = BaroqueUI_Controller.BuildFromObjectInside(gameObject);
        }

        public bool IsPressingButton(ControllerSnapshot snapshot)
        {
            return snapshot.GetButton(controllerButton);
        }

        public abstract Hover FindHover(ControllerSnapshot snapshot);
    }


    public class BaroqueUI_Controller : MonoBehaviour
    {
        public SteamVR_TrackedObject trackedObject;
        public ControllerSnapshot snapshot;

        public Vector3 velocity { get { return GetVelocity(); } }
        public Vector3 angularVelocity { get { return GetAngularVelocity(); } }   /* same as Rigidbody's */

        static SteamVR_ControllerManager controllerManager;
        VRControllerState_t controllerState;
        SteamVR_Events.Action newPosesAppliedAction;
        BaroqueUI_Controller otherController;
        SteamVR_Camera head;

        struct HoverAndAction { public Hover hover; public ControllerAction action; };

        HoverAndAction[] hovers_current;
        uint hovers_grabbed;
        int BUTTON_COUNT;

        void Awake()
        {
            snapshot.controller = this;

            foreach (EControllerButton button in Enum.GetValues(typeof(EControllerButton)))
                BUTTON_COUNT = Math.Max(BUTTON_COUNT, 1 + (int)button);

            trackedObject = GetComponent<SteamVR_TrackedObject>();
            newPosesAppliedAction = SteamVR_Events.NewPosesAppliedAction(OnNewPosesApplied);
            SetupDisabledState();   /* until enabled */
        }

        void OnEnable()
        {
            newPosesAppliedAction.enabled = true;
            InitializeVelocityEstimates();
        }

        void OnDisable()
        {
            /* the scene might be unloading right now.  We need to hack to detect that case.
             * I hope this hack actually works; it seems to be the case that the parent is
             * already disabled when we get OnDisable in that case.
             */
            if (transform.parent.gameObject.activeSelf)
                SetupDisabledState();
        }

        void SetupDisabledState()
        {
            newPosesAppliedAction.enabled = false;

            uint button_org = snapshot._buttons;
            snapshot._buttons = 0;
            CallButtonsUps(button_org);
            hovers_grabbed = 0;

            if (hovers_current != null)
            {
                for (int index = BUTTON_COUNT - 1; index >= 0; index--)
                {
                    HoverAndAction cur = hovers_current[index];
                    if (cur.hover != null)
                        cur.hover.OnButtonLeave(cur.action, snapshot);
                }
            }
            hovers_current = new HoverAndAction[BUTTON_COUNT];
        }

        void OnNewPosesApplied()
        {
            var system = OpenVR.System;
            if (system != null && system.GetControllerState((uint)trackedObject.index, ref controllerState,
                                        (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t))))
            {
                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger));
                ulong touch = controllerState.ulButtonTouched & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong pad = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad));
                ulong grip = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_Grip));
                ulong menu = controllerState.ulButtonPressed & (1UL << ((int)EVRButtonId.k_EButton_ApplicationMenu));

                uint button_org, b;
                b = button_org = snapshot._buttons;
                if (menu == 0) b &= ~(1U << (int)EControllerButton.Menu);
                if (grip == 0) b &= ~(1U << (int)EControllerButton.Grip);
                if (pad == 0) b &= ~(1U << (int)EControllerButton.Touchpad);
                if (trigger == 0) b &= ~(1U << (int)EControllerButton.Trigger);
                snapshot._buttons = b;
                CallButtonsUps(button_org);

                UpdatingVelocityEstimates();

                if (touch != 0)
                    snapshot.touchpadPosition = new Vector2(controllerState.rAxis0.x, controllerState.rAxis0.y);
                else
                    snapshot.touchpadPosition = null;

                b = button_org = snapshot._buttons;
                if (trigger != 0) b |= (1U << (int)EControllerButton.Trigger);
                if (pad != 0) b |= (1U << (int)EControllerButton.Touchpad);
                if (grip != 0) b |= (1U << (int)EControllerButton.Grip);
                if (menu != 0) b |= (1U << (int)EControllerButton.Menu);
                snapshot._buttons = b;
                
                /* XXX try to cache the result of GetComponentsInChildren */
                HoverAndAction[] hovers_new = new HoverAndAction[BUTTON_COUNT];
                foreach (var action in GetComponentsInChildren<ControllerAction>())
                {
                    int index = (int)action.controllerButton;
                    if ((hovers_grabbed & (1U << index)) == 0)
                    {
                        /* call FindHover if not grabbed.  This is also true if we just pressed the button now,
                         * because we only set hovers_grabbed below; but in this case 'snapshot' already says the
                         * button is pressed.  That's how FindHover() knows if it is being called for just hovering
                         * or really for pressing, with IsPressingButton().
                         */
                        Hover hover = action.FindHover(snapshot);
                        if (Hover.IsBetterHover(hover, hovers_new[index].hover))
                        {
                            hovers_new[index].hover = hover;
                            hovers_new[index].action = action;
                        }
                    }
                }
                for (int index = BUTTON_COUNT - 1; index >= 0; index--)
                {
                    if ((hovers_grabbed & (1U << index)) == 0)
                    {
                        /* button 'index' is not grabbed so far.  Call OnButtonLeave if we leave the old Hover. */
                        HoverAndAction cur = hovers_current[index];
                        if (cur.hover != null && hovers_new[index].hover != cur.hover)
                            cur.hover.OnButtonLeave(cur.action, snapshot);
                    }
                }
                for (int index = 0; index < BUTTON_COUNT; index++)
                {
                    if ((hovers_grabbed & (1U << index)) == 0)
                    {
                        /* button 'index' is not grabbed so far.  Update hovers_current and call OnButtonEnter
                         * if that changes. */
                        HoverAndAction cur = hovers_current[index];
                        if (hovers_new[index].hover != cur.hover)
                        {
                            cur = hovers_current[index] = hovers_new[index];
                            if (cur.hover != null)
                                cur.hover.OnButtonEnter(cur.action, snapshot);
                        }
                        if (cur.hover != null)   /* call OnButtonOver. */
                            cur.hover.OnButtonOver(cur.action, snapshot);
                    }
                }
                /* Now call any OnButtonDown. */
                CallButtonsDowns(button_org);

                /* Call OnButtonDrag. */
                if (hovers_grabbed != 0)
                {
                    for (int index = 0; index < BUTTON_COUNT; index++)
                    {
                        if ((hovers_grabbed & (1U << index)) != 0)
                        {
                            HoverAndAction cur = hovers_current[index];
                            if (cur.hover != null)
                                cur.hover.OnButtonDrag(cur.action, snapshot);
                        }
                    }
                }
            }
        }

        void CallButtonsDowns(uint button_org)
        {
            uint change = ~button_org & snapshot._buttons;
            if (change == 0)
                return;
            for (int index = 0; index < BUTTON_COUNT; index++)
            {
                if ((change & (1U << index)) != 0)
                {
                    HoverAndAction cur = hovers_current[index];
                    if (cur.hover != null)
                        cur.hover.OnButtonDown(cur.action, snapshot);
                    hovers_grabbed |= (1U << index);
                }
            }
        }

        void CallButtonsUps(uint button_org)
        {
            uint change = button_org & ~snapshot._buttons;
            if (change == 0)
                return;
            for (int index = BUTTON_COUNT - 1; index >= 0; index--)
            {
                if ((change & (1U << index)) != 0)
                {
                    HoverAndAction cur = hovers_current[index];
                    if (cur.hover != null)
                        cur.hover.OnButtonUp(cur.action, snapshot);
                    hovers_grabbed &= ~(1U << index);
                }
            }
        }

        static public BaroqueUI_Controller BuildFromObjectInside(GameObject gobj)
        {
            var ctrl = gobj.GetComponentInParent<BaroqueUI_Controller>();
            if (ctrl != null)
                return ctrl;

            var tobj = gobj.GetComponentInParent<SteamVR_TrackedObject>();
            if (tobj != null)
                return tobj.gameObject.AddComponent<BaroqueUI_Controller>();

            throw new MissingComponentException("'SteamVR_TrackedObject' not found in any parent object of '" + gobj.name + "'.  " +
                                                "You must put this component inside a subobject of 'Controller (left)' or " +
                                                "'Controller (right) in SteamVR's '[CameraRig]'.");
        }

        static public SteamVR_ControllerManager FindSteamVRControllerManager()
        {
            if (controllerManager != null)
                return controllerManager;

            controllerManager = FindObjectOfType<SteamVR_ControllerManager>();
            if (controllerManager == null)
                throw new MissingComponentException("'SteamVR_ControllerManager' not found anywhere");
            return controllerManager;
        }

        public BaroqueUI_Controller GetOtherController()
        {
            if (otherController != null)
                return otherController;

            SteamVR_ControllerManager mgr = FindSteamVRControllerManager();
            GameObject gobj;
            if (mgr.left == gameObject)
                gobj = mgr.right;
            else if (mgr.right == gameObject)
                gobj = mgr.left;
            else
                throw new MissingComponentException(name + " is neither 'left' nor 'right' in the SteamVR_ControllerManager");

            otherController = BuildFromObjectInside(gobj);
            return otherController;
        }

        public SteamVR_Camera GetHead()
        {
            if (head != null)
                return head;

            SteamVR_ControllerManager mgr = FindSteamVRControllerManager();
            head = mgr.GetComponentInChildren<SteamVR_Camera>();
            if (head == null)
                throw new MissingComponentException("'SteamVR_Camera' not found anywhere inside the SteamVR_ControllerManager");

            return head;
        }

        /*****************************************************************************************/

        const int DAMP_VELOCITY = 4;

        struct PrevLocation { public float time; public Vector3 position; public Quaternion rotation; };
        PrevLocation[] damped;

        void InitializeVelocityEstimates()
        {
            damped = new PrevLocation[DAMP_VELOCITY + 1];
            for (int i = 0; i <= DAMP_VELOCITY; i++)
                damped[i].time = float.NegativeInfinity;
        }

        void UpdatingVelocityEstimates()
        {
            Array.Copy(damped, 1, damped, 0, DAMP_VELOCITY);
            damped[DAMP_VELOCITY].time = Time.time;
            damped[DAMP_VELOCITY].position = transform.position;
            damped[DAMP_VELOCITY].rotation = transform.rotation;
        }

        Vector3 GetVelocity()
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

        Vector3 GetAngularVelocity()
        {
            /* xxx check this */
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
