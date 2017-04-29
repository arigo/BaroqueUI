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

        Dictionary<ControllerAction, ActionTracker> current_actions;

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

            current_actions = new Dictionary<ControllerAction, ActionTracker>();

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
            SceneAction.Register(sceneActionName, gameObject, 
                buttonDown: OnButtonDown, buttonDrag: OnButtonDrag, buttonUp: OnButtonUp);
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

        class ActionTracker
        {
            internal ControllerAction action;
            internal GraphicRaycaster raycaster;
            internal PointerEventData pevent;
            internal GameObject current_pressed;

            internal ActionTracker(ControllerAction action, BaroqueUI_CanvasUI canvasui)
            {
                this.action = action;
                raycaster = canvasui.GetComponent<GraphicRaycaster>();
                pevent = new PointerEventData(EventSystem.current);
            }

            internal bool UpdateCurrentPoint()
            {
                controllerCamera.transform.position = action.transform.position;
                controllerCamera.transform.rotation = action.transform.rotation;

                pevent.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */

                List<RaycastResult> results = new List<RaycastResult>();
                raycaster.Raycast(pevent, results);

                RaycastResult rr;
                if (!BestRaycastResult(results, out rr))
                    return false;
                pevent.pointerCurrentRaycast = rr;
                return rr.gameObject != null;
            }
        }

        ActionTracker GetTracker(ControllerAction action)
        {
            ActionTracker tracker;
            if (!current_actions.TryGetValue(action, out tracker))
            {
                tracker = new ActionTracker(action, this);
                current_actions[action] = tracker;
            }
            return tracker;
        }

        void RemoveTracker(ControllerAction action)
        {
            current_actions.Remove(action);
        }

        private void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            ActionTracker tracker = GetTracker(action);
            if (tracker.UpdateCurrentPoint())
            {
                PointerEventData pevent = tracker.pevent;
                pevent.pressPosition = pevent.position;
                pevent.pointerPressRaycast = pevent.pointerCurrentRaycast;
                pevent.pointerPress = null;

                GameObject target = pevent.pointerPressRaycast.gameObject;
                tracker.current_pressed = ExecuteEvents.ExecuteHierarchy(target, pevent, ExecuteEvents.pointerDownHandler);
                
                if (tracker.current_pressed == null)
                {
                    // some UI elements might only have click handler and not pointer down handler
                    tracker.current_pressed = ExecuteEvents.ExecuteHierarchy(target, pevent, ExecuteEvents.pointerClickHandler);
                }
                else
                {
                    // we want to do click on button down at same time, unlike regular mouse processing
                    // which does click when mouse goes up over same object it went down on
                    // reason to do this is head tracking might be jittery and this makes it easier to click buttons
                    ExecuteEvents.Execute(tracker.current_pressed, pevent, ExecuteEvents.pointerClickHandler);
                }

                if (tracker.current_pressed != null)
                {
                    ExecuteEvents.Execute(tracker.current_pressed, pevent, ExecuteEvents.beginDragHandler);
                    pevent.pointerDrag = tracker.current_pressed;
                }
            }
        }

        private void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
        {
            ActionTracker tracker = GetTracker(action);
            if (tracker.current_pressed != null && tracker.UpdateCurrentPoint())
            {
                ExecuteEvents.Execute(tracker.current_pressed, tracker.pevent, ExecuteEvents.dragHandler);
            }
        }

        private void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
        {
            ActionTracker tracker = GetTracker(action);
            if (tracker.current_pressed != null)
            {
                bool in_bounds = tracker.UpdateCurrentPoint();

                ExecuteEvents.Execute(tracker.current_pressed, tracker.pevent, ExecuteEvents.endDragHandler);
                if (in_bounds)
                {
                    ExecuteEvents.ExecuteHierarchy(tracker.current_pressed, tracker.pevent, ExecuteEvents.dropHandler);
                }
                ExecuteEvents.Execute(tracker.current_pressed, tracker.pevent, ExecuteEvents.pointerUpHandler);
            }
            RemoveTracker(action);
        }
    }
}