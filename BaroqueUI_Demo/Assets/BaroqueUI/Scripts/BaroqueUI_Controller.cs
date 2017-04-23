using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{

    public enum EControllerButton
    {
        TriggerClick, TouchpadTouch, TouchpadClick, GripButton, MenuButton
    }


    public struct ControllerSnapshot
    {
        public BaroqueUI_Controller controller;
        internal uint buttons;
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 touchpadPosition;

        public bool GetButton(EControllerButton btn)
        {
            return (buttons & (1U << (int)btn)) != 0;
        }

        public Vector3 back { get { return rotation * Vector3.back; } }
        public Vector3 down { get { return rotation * Vector3.down; } }
        public Vector3 forward { get { return rotation * Vector3.forward; } }
        public Vector3 left { get { return rotation * Vector3.left; } }
        public Vector3 right { get { return rotation * Vector3.right; } }
        public Vector3 up { get { return rotation * Vector3.up; } }

        public GameObject ThisControllerObject()
        {
            return controller.gameObject;
        }

        public GameObject OtherControllerObject()
        {
            return controller.GetOtherController().gameObject;
        }
    }


    public class Hover : IComparable<Hover>
    {
        public float reversed_priority;    /* smaller values have higher priority */
        public int CompareTo(Hover other) { return reversed_priority.CompareTo(other.reversed_priority); }

        public Hover() { }
        public Hover(float reversed_priority) { this.reversed_priority = reversed_priority; }

        public static Hover BestHover(Hover hov1, Hover hov2)
        {
            if (hov1 == null)
                return hov2;
            else if (hov2 == null || hov2.CompareTo(hov1) > 0)
                return hov1;
            else
                return hov2;
        }

        public virtual void OnButtonEnter(EControllerButton button, ControllerSnapshot snapshot) { }
        public virtual void OnButtonOver(EControllerButton button, ControllerSnapshot snapshot) { }
        public virtual void OnButtonLeave(EControllerButton button, ControllerSnapshot snapshot) { }
        public virtual void OnButtonDown(EControllerButton button, ControllerSnapshot snapshot) { }
        public virtual void OnButtonDrag(EControllerButton button, ControllerSnapshot snapshot) { }
        public virtual void OnButtonUp(EControllerButton button, ControllerSnapshot snapshot) { }
    }


    public abstract class AbstractControllerAction : MonoBehaviour
    {
        public EControllerButton controllerButton;
        public BaroqueUI_Controller controller { get; private set; }

        void Awake()
        {
            controller = BaroqueUI_Controller.BuildFromObjectInside(gameObject);
        }

        protected bool IsPressingButton(ControllerSnapshot snapshot)
        {
            return snapshot.GetButton(controllerButton);
        }

        public abstract Hover FindHover(ControllerSnapshot snapshot);
    }


    public class BaroqueUI_Controller : MonoBehaviour
    {
        public SteamVR_TrackedObject trackedObject;
        public ControllerSnapshot snapshot;

        static SteamVR_ControllerManager controllerManager;
        VRControllerState_t controllerState;
        SteamVR_Events.Action newPosesAppliedAction;
        BaroqueUI_Controller otherController;

        Hover[] hovers_current;
        uint hovers_grabbed;
        int BUTTON_COUNT;

        void Awake()
        {
            snapshot.controller = this;

            foreach (EControllerButton button in Enum.GetValues(typeof(EControllerButton)))
                BUTTON_COUNT = Math.Max(BUTTON_COUNT, 1 + (int)button);
            hovers_current = new Hover[BUTTON_COUNT];
            hovers_grabbed = 0;

            trackedObject = GetComponent<SteamVR_TrackedObject>();
            newPosesAppliedAction = SteamVR_Events.NewPosesAppliedAction(OnNewPosesApplied);
            newPosesAppliedAction.enabled = false;
        }

        void OnEnable()
        {
            newPosesAppliedAction.enabled = true;
        }

        void OnDisable()
        {
            newPosesAppliedAction.enabled = false;

            uint button_org = snapshot.buttons;
            snapshot.buttons = 0;
            CallButtonsUps(button_org);
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
                b = button_org = snapshot.buttons;
                if (menu == 0) b &= ~(1U << (int)EControllerButton.MenuButton);
                if (grip == 0) b &= ~(1U << (int)EControllerButton.GripButton);
                if (pad == 0) b &= ~(1U << (int)EControllerButton.TouchpadClick);
                if (touch == 0) b &= ~(1U << (int)EControllerButton.TouchpadTouch);
                if (trigger == 0) b &= ~(1U << (int)EControllerButton.TriggerClick);
                snapshot.buttons = b;
                CallButtonsUps(button_org);
                
                snapshot.position = transform.position;
                snapshot.rotation = transform.rotation;
                snapshot.touchpadPosition = new Vector2(controllerState.rAxis0.x, controllerState.rAxis0.y);

                b = button_org = snapshot.buttons;
                if (trigger != 0) b |= (1U << (int)EControllerButton.TriggerClick);
                if (touch != 0) b |= (1U << (int)EControllerButton.TouchpadTouch);
                if (pad != 0) b |= (1U << (int)EControllerButton.TouchpadClick);
                if (grip != 0) b |= (1U << (int)EControllerButton.GripButton);
                if (menu != 0) b |= (1U << (int)EControllerButton.MenuButton);
                snapshot.buttons = b;
                
                /* XXX try to cache the result of GetComponentsInChildren */
                Hover[] hovers_new = new Hover[BUTTON_COUNT];
                foreach (var action in GetComponentsInChildren<AbstractControllerAction>())
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
                        hovers_new[index] = Hover.BestHover(hovers_new[index], hover);
                    }
                }
                for (int index = BUTTON_COUNT - 1; index >= 0; index--)
                {
                    if ((hovers_grabbed & (1U << index)) == 0)
                    {
                        /* button 'index' is not grabbed so far.  Call OnButtonLeave if we leave the old Hover. */
                        if (hovers_new[index] != hovers_current[index] && hovers_current[index] != null)
                            hovers_current[index].OnButtonLeave((EControllerButton)index, snapshot);
                    }
                }
                for (int index = 0; index < BUTTON_COUNT; index++)
                {
                    if ((hovers_grabbed & (1U << index)) == 0)
                    {
                        /* button 'index' is not grabbed so far.  Update hovers_current and call OnButtonEnter
                         * if that changes. */
                        if (hovers_new[index] != hovers_current[index])
                        {
                            hovers_current[index] = hovers_new[index];
                            if (hovers_current[index] != null)
                                hovers_current[index].OnButtonEnter((EControllerButton)index, snapshot);
                        }
                        if (hovers_current[index] != null)   /* call OnButtonOver. */
                            hovers_current[index].OnButtonOver((EControllerButton)index, snapshot);
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
                            if (hovers_current[index] != null)
                                hovers_current[index].OnButtonDrag((EControllerButton)index, snapshot);
                        }
                    }
                }
            }
        }

        void CallButtonsDowns(uint button_org)
        {
            uint change = ~button_org & snapshot.buttons;
            if (change == 0)
                return;
            for (int index = 0; index < BUTTON_COUNT; index++)
            {
                if ((change & (1U << index)) != 0)
                {
                    if (hovers_current[index] != null)
                        hovers_current[index].OnButtonDown((EControllerButton)index, snapshot);
                    hovers_grabbed |= (1U << index);
                }
            }
        }

        void CallButtonsUps(uint button_org)
        {
            uint change = button_org & ~snapshot.buttons;
            if (change == 0)
                return;
            for (int index = BUTTON_COUNT - 1; index >= 0; index--)
            {
                if ((change & (1U << index)) != 0)
                {
                    if (hovers_current[index] != null)
                        hovers_current[index].OnButtonUp((EControllerButton)index, snapshot);
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
    }
}
