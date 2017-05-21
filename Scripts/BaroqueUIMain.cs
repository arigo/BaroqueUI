using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace BaroqueUI
{
    public static class BaroqueUIMain
    {
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
            if (head == null)   // includes 'has been destroyed'
            {
                SteamVR_Camera camera = GetSteamVRManager().GetComponentInChildren<SteamVR_Camera>();
                if (camera == null)
                    throw new MissingComponentException("'[CameraRig]' gameobject has no 'SteamVR_Camera' inside");
                head = camera.gameObject;
            }
            return head.transform;
        }

        static public Controller[] GetControllers()
        {
            EnsureStarted();
            return controllers;
        }

        static public GameObject FindPossiblyInactive(string path_in_scene)
        {
            Transform tr = null;
            foreach (var name in path_in_scene.Split('/'))
            {
                if (name == "")
                    continue;
                if (tr == null)
                {
                    foreach (var gobj in SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        if (gobj.name == name)
                        {
                            tr = gobj.transform;
                            break;
                        }
                    }
                }
                else
                {
                    tr = tr.FindChild(name);
                }
                if (tr == null)
                    throw new System.Exception("gameobject not found: '" + path_in_scene + "'");
            }
            return tr.gameObject;
        }


        /*********************************************************************************************/

        static bool controllersReady, globallyReady;
        static GameObject head, left_controller, right_controller;
        static Controller[] controllers;

        static GameObject InitController(GameObject go, int index)
        {
            Controller ctrl = go.GetComponent<Controller>();
            if (ctrl == null)
                ctrl = go.AddComponent<Controller>();
            ctrl.Initialize(index);
            controllers[index] = ctrl;
            return go;
        }

        static void InitControllers()
        {
            if (!controllersReady)
            {
                controllers = new Controller[2];
                left_controller = InitController(GetSteamVRManager().left, 0);
                right_controller = InitController(GetSteamVRManager().right, 1);
                controllersReady = true;

                if (!globallyReady)
                {
                    SteamVR_Events.NewPosesApplied.AddListener(OnNewPosesApplied);

                    SceneManager.sceneUnloaded += (scene) => { controllersReady = false; };
                    globallyReady = true;
                }
            }
            else
            {
                /* this occurs during scene unloading, when the controller objects have already
                 * been destroyed.  Make an empty list in this case. */
                controllers = new Controller[0];
            }
        }

        static void EnsureStarted()
        {
            if (left_controller == null || right_controller == null)   // includes 'has been destroyed'
                InitControllers();
        }

        static void OnNewPosesApplied()
        {
            Controller[] controllers = GetControllers();
            if (controllers.Length > 0)
                Controller.UpdateAllControllers(controllers);
        }
    }
}
