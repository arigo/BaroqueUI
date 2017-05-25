using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace BaroqueUI
{
    public static class Baroque
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


        static GameObject drawings;

        public static void DrawLine(Vector3 from, Vector3 to)
        {
            if (!Application.isEditor)
                MakeLine(from, to);
        }

        public static void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            if (!Application.isEditor)
            {
                GameObject go = MakeLine(from, to);
                go.GetComponent<Renderer>().material.color = color;
            }
        }

        static GameObject MakeLine(Vector3 from, Vector3 to)
        {
            if (drawings == null)
                drawings = new GameObject("BaroqueUI drawings");

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(drawings.transform);
            go.transform.position = (from + to) * 0.5f;
            go.transform.localScale = new Vector3(0.005f, 0.005f, Vector3.Distance(from, to));
            go.transform.rotation = Quaternion.LookRotation(to - from);
            return go;
        }

        static void RemoveDrawings()
        {
            if (drawings != null)
                GameObject.Destroy(drawings);
            drawings = null;
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
#if UNITY_5_6
                /* hack hack hack for SteamVR */
                if (GetHeadTransform().GetComponent<SteamVR_UpdatePoses>() == null)
                    GetHeadTransform().gameObject.AddComponent<SteamVR_UpdatePoses>();
#endif
                Controller.InitControllers();
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

        static internal void EnsureStarted()
        {
            if (left_controller == null || right_controller == null)   // includes 'has been destroyed'
                InitControllers();
        }

        static void OnNewPosesApplied()
        {
            Controller[] controllers = GetControllers();
            RemoveDrawings();
            if (controllers.Length > 0)
                Controller.UpdateAllControllers(controllers);
        }


        /*********************************************************************************************/

        static internal void InitTests()
        {
            controllersReady = false;
            globallyReady = true;
            InitControllers();
        }
    }
}
