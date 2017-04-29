using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


namespace BaroqueUI
{
    public class BaroqueUI_CanvasUI : MonoBehaviour
    {
        public string sceneActionName = "Raycast";

        /* Gross hacks ahead: the Canvas UI objects require a camera when doing a Raycast().
         * This "camera" is set up to "look" from the controller's point of view.  This
         * is inspired from https://github.com/VREALITY/ViveUGUIModule.
         */
        static Camera controllerCamera;

        private void Start()
        {
            if (controllerCamera == null || !controllerCamera.gameObject.activeInHierarchy)
            {
                controllerCamera = new GameObject("Controller UI Camera").AddComponent<Camera>();
                controllerCamera.clearFlags = CameraClearFlags.Nothing; //CameraClearFlags.Depth;
                controllerCamera.cullingMask = 0; // 1 << LayerMask.NameToLayer("UI");
                controllerCamera.pixelRect = new Rect { x=0, y=0, width=10, height=10 };
            }
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>())
                canvas.worldCamera = controllerCamera;

            if (GetComponentInChildren<Collider>() == null)
            {
                RectTransform rtr = transform as RectTransform;
                Rect r = rtr.rect;

                BoxCollider coll = gameObject.AddComponent<BoxCollider>();
                coll.isTrigger = true;
                coll.size = new Vector3(r.width, r.height, 1);   /* XXX check what occurs if the Canvas contains components
                                                                    that have a Z coordinate that differs a lot from 0 */
                coll.center = r.center;
            }
            SceneAction.Register(sceneActionName, gameObject, buttonDown: OnButtonDown);
        }

        static bool IsBetterRaycastResult(RaycastResult rr1, RaycastResult rr2)
        {
            if (rr1.depth != rr2.depth)
                return rr1.depth > rr2.depth;
            return rr1.index < rr2.index;
        }

        static bool BestRaycastResult(List<RaycastResult> lst, out RaycastResult best_result)
        {
            best_result = new RaycastResult();
            bool found_any = false;

            foreach (var result in lst)
            {
                if (result.gameObject == null)
                    continue;
                if (!found_any || IsBetterRaycastResult(result, best_result))
                {
                    best_result = result;
                    found_any = true;
                }
            }
            return found_any;
        }

        private void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            controllerCamera.transform.position = action.transform.position;
            controllerCamera.transform.rotation = action.transform.rotation;

            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */

            List<RaycastResult> results = new List<RaycastResult>();
            GetComponent<GraphicRaycaster>().Raycast(pointerData, results);

            RaycastResult rr;
            if (BestRaycastResult(results, out rr))
            {
                Debug.Log(rr.gameObject);
            }
        }
    }
}