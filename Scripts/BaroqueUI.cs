using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


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


        /* This feels like a hack, but to get UI elements from a 3D position, we need a Camera
         * to issue a Raycast().  This "camera" is set up to "look" from the controller's point 
         * of view, usually orthogonally from the plane of the UI (but it could also be along
         * the controller's direction, if we go for ray-casting selection).  This is inspired 
         * from https://github.com/VREALITY/ViveUGUIModule.
         */
        static public Camera GetControllerCamera()
        {
            if (controllerCamera == null || !controllerCamera.gameObject.activeInHierarchy)
            {
                controllerCamera = new GameObject("Controller Camera").AddComponent<Camera>();
                controllerCamera.clearFlags = CameraClearFlags.Nothing;
                controllerCamera.cullingMask = 0;
                controllerCamera.pixelRect = new Rect { x = 0, y = 0, width = 10, height = 10 };
                controllerCamera.nearClipPlane = 0.001f;
            }
            return controllerCamera;
        }

        static public PointerEventData MoveControllerCamera(Vector3 position, Vector3 forward)
        {
            PointerEventData pevent = new PointerEventData(EventSystem.current);
            MoveControllerCamera(position, forward, pevent);
            return pevent;
        }

        static public void MoveControllerCamera(Vector3 position, Vector3 forward, PointerEventData pevent)
        {
            Camera camera = GetControllerCamera();
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(forward);
#if false
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.rotation = Quaternion.LookRotation(forward);
            go.transform.position = position + go.transform.rotation * new Vector3(0, 0, 0.05f);
            go.transform.localScale = new Vector3(0.003f, 0.003f, 0.1f);
            GameObject.Destroy(go, 0.05f);
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(forward);
            go.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
            GameObject.Destroy(go, 0.05f);
#endif
            pevent.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */
        }


        /*********************************************************************************************/

        static GameObject head, left_controller, right_controller;
        static Controller[] controllers;
        static SteamVR_Events.Action newPosesAppliedAction;
        static Camera controllerCamera;

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
