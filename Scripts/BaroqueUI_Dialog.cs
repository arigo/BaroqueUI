using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;


namespace BaroqueUI
{
    public class BaroqueUI_Dialog : MonoBehaviour
    {
        public string sceneActionName = "Raycast";
        public float unitsPerMeter = 250;

        /* XXX internals are very Javascript-ish, full of multi-level callbacks.
         * It could also be done using a class hierarchy but it feels like it would be pages longer. */
        delegate object getter_fn();
        delegate void setter_fn(object value);
        delegate void deleter_fn();

        struct WidgetDescr {
            public getter_fn getter;
            public setter_fn setter;
            public deleter_fn detach;
            public setter_fn attach;
        }

        WidgetDescr FindWidget(string widget_name)
        {
            Transform tr = transform.Find(widget_name);
            if (tr == null)
                throw new MissingComponentException(widget_name);

            Slider slider = tr.GetComponent<Slider>();
            if (slider != null)
                return new WidgetDescr() {
                    getter = () => slider.value,
                    setter = (value) => slider.value = (float)value,
                    detach = () => slider.onValueChanged.RemoveAllListeners(),
                    attach = (callback) => slider.onValueChanged.AddListener((UnityAction<float>)callback),
                };

            Text text = tr.GetComponent<Text>();
            if (text != null)
                return new WidgetDescr() {
                    getter = () => text.text,
                    setter = (value) => text.text = (string)value,
                };

            throw new NotImplementedException(widget_name);
        }

        public void Set(string widget_name, object value, object onChange = null)
        {
            var descr = FindWidget(widget_name);
            if (onChange != null)
                descr.detach();
            descr.setter(value);
            if (onChange != null)
                descr.attach(onChange);
        }

        public object Get(string widget_name)
        {
            var descr = FindWidget(widget_name);
            return descr.getter();
        }


        /*******************************************************************************************/

        /* Gross hacks ahead: the Canvas UI objects require a camera when doing a Raycast().
         * This "camera" is set up to "look" from the controller's point of view.  This
         * is inspired from https://github.com/VREALITY/ViveUGUIModule.
         */
        static Camera controllerCamera;

        Dictionary<ControllerAction, ActionTracker> current_actions;

        void Awake()
        {
            if (onEnable == null)     /* must be set to non-null, otherwise the dialog is disabled */
                gameObject.SetActive(false);
        }

        void Start()
        {
            if (controllerCamera == null || !controllerCamera.gameObject.activeInHierarchy)
            {
                controllerCamera = new GameObject("Controller UI Camera").AddComponent<Camera>();
                controllerCamera.clearFlags = CameraClearFlags.Nothing; //CameraClearFlags.Depth;
                controllerCamera.cullingMask = 0; // 1 << LayerMask.NameToLayer("UI");
                controllerCamera.pixelRect = new Rect { x=0, y=0, width=10, height=10 };
                controllerCamera.nearClipPlane = 0.001f;
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
            transform.localScale = Vector3.one / unitsPerMeter;

            SceneAction.Register(sceneActionName, gameObject, 
                buttonEnter: OnButtonEnter, buttonOver: OnButtonOver, buttonLeave: OnButtonLeave,
                buttonDown: OnButtonDown, buttonDrag: OnButtonDrag, buttonUp: OnButtonUp);
        }

        public UnityAction onEnable, onDisable;

        void OnEnable()
        {
            if (onEnable != null)
                onEnable();
        }

        void OnDisable()
        {
            if (onDisable != null)
                onDisable();
        }

        static bool IsBetterRaycastResult(RaycastResult rr1, RaycastResult rr2)
        {
            if (rr1.sortingLayer != rr2.sortingLayer)
                return SortingLayer.GetLayerValueFromID(rr1.sortingLayer) > SortingLayer.GetLayerValueFromID(rr2.sortingLayer);
            if (rr1.sortingOrder != rr2.sortingOrder)
                return rr1.sortingOrder > rr2.sortingOrder;
            if (rr1.depth != rr2.depth)
                return rr1.depth > rr2.depth;
            if (rr1.distance != rr2.distance)
                return rr1.distance < rr2.distance;
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
            internal Transform base_graphic;
            internal PointerEventData pevent;
            internal GameObject current_pressed;

            internal ActionTracker(ControllerAction action, Transform transform)
            {
                this.action = action;
                base_graphic = transform;
                pevent = new PointerEventData(EventSystem.current);
            }

            internal bool UpdateCurrentPoint(bool allow_out_of_bounds = false)
            {
                controllerCamera.transform.position = action.transform.position;
                controllerCamera.transform.rotation = action.transform.rotation;

                pevent.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */

                List<RaycastResult> results = new List<RaycastResult>();
                foreach (var raycaster in base_graphic.GetComponentsInChildren<GraphicRaycaster>())
                    raycaster.Raycast(pevent, results);

                RaycastResult rr;
                if (!BestRaycastResult(results, out rr))
                {
                    if (allow_out_of_bounds)
                    {
                        /* return a "raycast" result that simply projects on the canvas plane Z=0 */
                        Plane plane = new Plane(base_graphic.transform.forward, base_graphic.transform.position);
                        Ray ray = new Ray(action.transform.position, action.transform.forward);
                        float enter;
                        if (plane.Raycast(ray, out enter))
                        {
                            pevent.pointerCurrentRaycast = new RaycastResult
                            {
                                depth = -1,
                                distance = enter,
                                worldNormal = base_graphic.transform.forward,
                                worldPosition = ray.GetPoint(enter),
                            };
                            return true;
                        }
                    }
                    return false;
                }
                pevent.pointerCurrentRaycast = rr;
                return rr.gameObject != null;
            }
        }

        ActionTracker GetTracker(ControllerAction action)
        {
            return current_actions[action];
        }

        ActionTracker AddTracker(ControllerAction action)
        {
            ActionTracker tracker = new ActionTracker(action, transform);
            current_actions[action] = tracker;
            return tracker;
        }

        void RemoveTracker(ControllerAction action)
        {
            current_actions.Remove(action);
        }

        private void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
            ActionTracker tracker = AddTracker(action);
        }

        private void OnButtonOver(ControllerAction action, ControllerSnapshot snapshot)
        {
            ActionTracker tracker = GetTracker(action);

            // handle enter and exit events (highlight)
            GameObject new_target = null;
            if (tracker.UpdateCurrentPoint())
                new_target = tracker.pevent.pointerCurrentRaycast.gameObject;

            UpdateHoveringTarget(tracker, new_target);
        }

        void UpdateHoveringTarget(ActionTracker tracker, GameObject new_target)
        {
            if (new_target == tracker.pevent.pointerEnter)
                return;    /* already up-to-date */

            /* pop off any hovered objects from the stack, as long as they are not parents of 'new_target' */
            while (tracker.pevent.hovered.Count > 0)
            {
                GameObject h = tracker.pevent.hovered[tracker.pevent.hovered.Count - 1];
                if (!h)
                {
                    tracker.pevent.hovered.RemoveAt(tracker.pevent.hovered.Count - 1);
                    continue;
                }
                if (new_target != null && new_target.transform.IsChildOf(h.transform))
                    break;
                tracker.pevent.hovered.RemoveAt(tracker.pevent.hovered.Count - 1);
                ExecuteEvents.Execute(h, tracker.pevent, ExecuteEvents.pointerExitHandler);
            }

            /* enter and push any new object going to 'new_target', in order from outside to inside */
            tracker.pevent.pointerEnter = new_target;
            if (new_target != null)
                EnterAndPush(tracker.pevent, new_target.transform, tracker.pevent.hovered.Count == 0 ? transform :
                              tracker.pevent.hovered[tracker.pevent.hovered.Count - 1].transform);
        }

        static void EnterAndPush(PointerEventData pevent, Transform new_target_transform, Transform limit)
        {
            if (new_target_transform != limit)
            {
                EnterAndPush(pevent, new_target_transform.parent, limit);
                ExecuteEvents.Execute(new_target_transform.gameObject, pevent, ExecuteEvents.pointerEnterHandler);
                pevent.hovered.Add(new_target_transform.gameObject);
            }
        }

        private void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot)
        {
            UpdateHoveringTarget(GetTracker(action), null);
            RemoveTracker(action);
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
            if (tracker.current_pressed != null && tracker.UpdateCurrentPoint(allow_out_of_bounds: true))
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

                tracker.current_pressed = null;
            }
        }
    }
}