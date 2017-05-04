using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;


namespace BaroqueUI
{
    public static class BaroqueUI
    {
        static public void EnsureStarted()
        {
            if (left_controller == null || !left_controller)
                left_controller = InitController(GetSteamVRManager().left, 0);
            if (right_controller == null || !right_controller)
                right_controller = InitController(GetSteamVRManager().right, 1);
        }

        static public SteamVR_ControllerManager GetSteamVRManager()
        {
            GameObject gobj = GameObject.Find("/[CameraRig]");
            if (gobj == null)
                throw new MissingComponentException("'[CameraRig]' gameobject not found at the top level of the scene");

            SteamVR_ControllerManager mgr = gobj.GetComponent<SteamVR_ControllerManager>();
            if (mgr == null)
                throw new MissingComponentException("'[CameraRig]' gameobject is missing a SteamVR_ControllerManager component");

            return mgr;
        }
        
        static public Transform GetHeadTransform()
        {
            if (head == null || !head)
            {
                SteamVR_Camera camera = GetSteamVRManager().GetComponentInChildren<SteamVR_Camera>();
                if (camera == null)
                    throw new MissingComponentException("'[CameraRig'] gameobject has no 'SteamVR_Camera' inside");
                head = camera.gameObject;
            }
            return head.transform;
        }

        static public Controller[] GetControllers()
        {
            EnsureStarted();
            return controllers;
        }

        static public GameObject GetPointerObject(string name)
        {
            return Resources.Load("BaroqueUI/Pointers/" + name) as GameObject;
        }

        /*********************************************************************************************/

        static GameObject head, left_controller, right_controller;
        static Controller[] controllers;
        static SteamVR_Events.Action newPosesAppliedAction;

        static GameObject InitController(GameObject go, int index)
        {
            Controller ctrl = go.GetComponent<Controller>();
            if (ctrl == null)
                ctrl = go.AddComponent<Controller>();

            ctrl.Initialize(index);

            if (controllers == null)
                controllers = new Controller[2];
            controllers[index] = ctrl;

            if (newPosesAppliedAction == null)
            {
                newPosesAppliedAction = SteamVR_Events.NewPosesAppliedAction(OnNewPosesApplied);
                newPosesAppliedAction.enabled = true;
            }

            return go;
        }

        static void OnNewPosesApplied()
        {
            Controller[] controllers = GetControllers();
            Controller.UpdateAllControllers(controllers);
        }
    }
}
