using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{

    public enum EControllerButton
    {
        TriggerClick, TouchpadClick, TouchpadTouch, GripButton, MenuButton, NoRestriction
    }


    public struct ControllerSnapshot
    {
        public BaroqueUI_Controller controller;
        public bool triggerClick, touchpadClick, touchpadTouch, gripButton, menuButton;
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 touchpadPosition;

        public bool GetButton(EControllerButton btn)
        {
            switch (btn)
            {
                case EControllerButton.TriggerClick: return triggerClick;
                case EControllerButton.TouchpadClick: return touchpadClick;
                case EControllerButton.TouchpadTouch: return touchpadTouch;
                case EControllerButton.GripButton: return gripButton;
                case EControllerButton.MenuButton: return menuButton;
                default: throw new ArgumentOutOfRangeException("EControllerButton: unknown value " + btn);
            }
        }

        static public void SetButton(ref ControllerSnapshot snapshot, EControllerButton btn, bool value)
        {
            switch (btn)
            {
                case EControllerButton.TriggerClick: snapshot.triggerClick = value; break;
                case EControllerButton.TouchpadClick: snapshot.touchpadClick = value; break;
                case EControllerButton.TouchpadTouch: snapshot.touchpadTouch = value; break;
                case EControllerButton.GripButton: snapshot.gripButton = value; break;
                case EControllerButton.MenuButton: snapshot.menuButton = value; break;
                default: throw new ArgumentOutOfRangeException();
            }
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


    public abstract class AbstractControllerAction : MonoBehaviour
    {
        public EControllerButton controllerButton;
        public BaroqueUI_Controller controller { get; private set; }

        void Awake()
        {
            controller = BaroqueUI_Controller.BuildFromObjectInside(gameObject);
        }

        public virtual bool HandleButtonDown(ControllerSnapshot snapshot) { return false; }
        public virtual void HandleButtonMove(ControllerSnapshot snapshot) { }
        public virtual bool HandleButtonUp() { return false; }
    }


    public class BaroqueUI_Controller : MonoBehaviour
    {
        public SteamVR_TrackedObject trackedObject;
        public ControllerSnapshot snapshot;

        VRControllerState_t controllerState;
        SteamVR_Events.Action newPosesAppliedAction;
        BaroqueUI_Controller otherController;

        void Awake()
        {
            snapshot.controller = this;
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
            foreach (EControllerButton button in Enum.GetValues(typeof(EControllerButton)))
                if (button != EControllerButton.NoRestriction && snapshot.GetButton(button))
                    ButtonUp(button);
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

                if (menu == 0 && snapshot.menuButton)
                    ButtonUp(EControllerButton.TouchpadClick);
                if (grip == 0 && snapshot.gripButton)
                    ButtonUp(EControllerButton.GripButton);
                if (pad == 0 && snapshot.touchpadClick)
                    ButtonUp(EControllerButton.TouchpadClick);
                if (touch == 0 && snapshot.touchpadTouch)
                    ButtonUp(EControllerButton.TouchpadTouch);
                if (trigger == 0 && snapshot.triggerClick)
                    ButtonUp(EControllerButton.TriggerClick);

                snapshot.position = transform.position;
                snapshot.rotation = transform.rotation;
                snapshot.touchpadPosition = new Vector2(controllerState.rAxis0.x, controllerState.rAxis0.y);

                if (trigger != 0 && !snapshot.triggerClick)
                    ButtonDown(EControllerButton.TriggerClick);
                if (touch != 0 && !snapshot.touchpadTouch)
                    ButtonDown(EControllerButton.TouchpadTouch);
                if (pad != 0 && !snapshot.touchpadClick)
                    ButtonDown(EControllerButton.TouchpadClick);
                if (grip != 0 && !snapshot.gripButton)
                    ButtonDown(EControllerButton.GripButton);
                if (menu != 0 && !snapshot.menuButton)
                    ButtonDown(EControllerButton.TouchpadClick);

                foreach (var action in AllActions())
                {
                    if (action.enabled && snapshot.GetButton(action.controllerButton))
                        action.HandleButtonMove(LocalizedSnapshot(action.transform));
                }
            }
        }

        AbstractControllerAction[] AllActions()
        {
            var result = GetComponentsInChildren<AbstractControllerAction>();
            Array.Reverse(result);
            return result;
        }

        AbstractControllerAction[] ActiveActionsFor(EControllerButton button)
        {
            AbstractControllerAction[] result = AllActions();
            int keep = 0;
            for (int i = 0; i < result.Length; i++)
            {
                var action = result[i];
                if (action.enabled && (
                        action.controllerButton == EControllerButton.NoRestriction ||
                        action.controllerButton == button))
                    result[keep++] = action;
            }
            Array.Resize<AbstractControllerAction>(ref result, keep);
            return result;
        }

        ControllerSnapshot LocalizedSnapshot(Transform tr)
        {
            ControllerSnapshot result = snapshot;
            result.position = tr.position;
            result.rotation = tr.rotation;
            return result;
        }

        void ButtonDown(EControllerButton button)
        {
            ControllerSnapshot.SetButton(ref snapshot, button, true);

            foreach (var aa in ActiveActionsFor(button))
            {
                if (aa.HandleButtonDown(LocalizedSnapshot(aa.transform)))
                    break;
            }
        }

        void ButtonUp(EControllerButton button)
        {
            ControllerSnapshot.SetButton(ref snapshot, button, false);

            foreach (var aa in ActiveActionsFor(button))
            {
                if (aa.HandleButtonUp())
                    break;
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

        public BaroqueUI_Controller GetOtherController()
        {
            if (otherController != null)
                return otherController;

            SteamVR_ControllerManager mgr = GetComponentInParent<SteamVR_ControllerManager>();
            if (mgr == null)
                throw new MissingComponentException("'SteamVR_ControllerManager' not found in any parent of " + name);

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
