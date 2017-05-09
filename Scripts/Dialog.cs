using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;


namespace BaroqueUI
{
    public class Dialog : ControllerTracker
    {
        public bool alreadyPositioned = false;
        public float unitsPerMeter = 400;

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

            InputField inputField = tr.GetComponent<InputField>();
            if (inputField != null)
                return new WidgetDescr() {
                    getter = () => inputField.text,
                    setter = (value) => inputField.text = (string)value,
                    detach = () => inputField.onEndEdit.RemoveAllListeners(),
                    attach = (callback) => inputField.onEndEdit.AddListener((UnityAction<string>)callback),
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

        public void DisplayDialog()
        {
            transform.localScale = Vector3.one / unitsPerMeter;
            alreadyPositioned = true;
            gameObject.SetActive(true);
        }

        void Start()
        {
            if (!alreadyPositioned)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (Canvas canvas in GetComponentsInChildren<Canvas>())
                canvas.worldCamera = BaroqueUI.GetControllerCamera();

            foreach (InputField inputField in GetComponentsInChildren<InputField>())
            {
                if (inputField.GetComponent<KeyboardActivator>() == null)
                    inputField.gameObject.AddComponent<KeyboardActivator>();
            }

            if (GetComponentInChildren<Collider>() == null)
            {
                RectTransform rtr = transform as RectTransform;
                Rect r = rtr.rect;
                float zscale = transform.InverseTransformVector(transform.forward * 0.108f).magnitude;

                BoxCollider coll = gameObject.AddComponent<BoxCollider>();
                coll.isTrigger = true;
                coll.size = new Vector3(r.width, r.height, zscale);
                coll.center = new Vector3(r.center.x, r.center.y, zscale * -0.3125f);
            }
        }

        void OnEnable()
        {
            foreach (var selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable.interactable)
                {
                    StartCoroutine(SetSelected(selectable));
                    break;
                }
            }
        }

        IEnumerator SetSelected(Selectable selectable)
        {
            yield return new WaitForSecondsRealtime(0.1f);   /* doesn't work if done immediately :-( */
            if (selectable && selectable.isActiveAndEnabled)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject, null);
                selectable.Select();
            }
        }

#if false
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
#endif

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


        PointerEventData pevent;
        GameObject current_pressed;

        public override void OnEnter(Controller controller)
        {
            pevent = new PointerEventData(EventSystem.current);
        }

        public override void OnMoveOver(Controller controller)
        {
            // handle enter and exit events (highlight)
            GameObject new_target = null;
            if (UpdateCurrentPoint(controller))
                new_target = pevent.pointerCurrentRaycast.gameObject;

            UpdateHoveringTarget(new_target);

            float distance_to_plane = transform.InverseTransformPoint(controller.position).z * -0.06f;
            if (distance_to_plane < 1)
                distance_to_plane = 1;
            GameObject go = controller.SetPointer("Cursor");  /* XXX */
            go.transform.rotation = transform.rotation;
            go.transform.localScale = new Vector3(1, 1, distance_to_plane);
        }

        bool UpdateCurrentPoint(Controller controller, bool allow_out_of_bounds = false)
        {
            BaroqueUI.MoveControllerCamera(controller.position, transform.forward, pevent);

            List<RaycastResult> results = new List<RaycastResult>();
            foreach (var raycaster in GetComponentsInChildren<GraphicRaycaster>())
                raycaster.Raycast(pevent, results);

            RaycastResult rr;
            if (!BestRaycastResult(results, out rr))
            {
                if (allow_out_of_bounds)
                {
                    /* return a "raycast" result that simply projects on the canvas plane Z=0 */
                    /* (xxx could use Vector3.ProjectOnPlane(); this version is more flexible in case
                       we want again a ray that is not perpendicular) */
                    Plane plane = new Plane(transform.forward, transform.position);
                    Ray ray = new Ray(controller.position, transform.forward);
                    float enter;
                    if (plane.Raycast(ray, out enter))
                    {
                        pevent.pointerCurrentRaycast = new RaycastResult
                        {
                            depth = -1,
                            distance = enter,
                            worldNormal = transform.forward,
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


        void UpdateHoveringTarget(GameObject new_target)
        {
            if (new_target == pevent.pointerEnter)
                return;    /* already up-to-date */

            /* pop off any hovered objects from the stack, as long as they are not parents of 'new_target' */
            while (pevent.hovered.Count > 0)
            {
                GameObject h = pevent.hovered[pevent.hovered.Count - 1];
                if (!h)
                {
                    pevent.hovered.RemoveAt(pevent.hovered.Count - 1);
                    continue;
                }
                if (new_target != null && new_target.transform.IsChildOf(h.transform))
                    break;
                pevent.hovered.RemoveAt(pevent.hovered.Count - 1);
                ExecuteEvents.Execute(h, pevent, ExecuteEvents.pointerExitHandler);
            }

            /* enter and push any new object going to 'new_target', in order from outside to inside */
            pevent.pointerEnter = new_target;
            if (new_target != null)
                EnterAndPush(new_target.transform, pevent.hovered.Count == 0 ? transform :
                              pevent.hovered[pevent.hovered.Count - 1].transform);
        }

        void EnterAndPush(Transform new_target_transform, Transform limit)
        {
            if (new_target_transform != limit)
            {
                EnterAndPush(new_target_transform.parent, limit);
                ExecuteEvents.Execute(new_target_transform.gameObject, pevent, ExecuteEvents.pointerEnterHandler);
                pevent.hovered.Add(new_target_transform.gameObject);
            }
        }

        public override void OnLeave(Controller controller)
        {
            UpdateHoveringTarget(null);
            pevent = null;
            controller.SetPointer(null);
        }

        public override void OnTriggerDown(Controller controller)
        {
            if (UpdateCurrentPoint(controller))
            {
                pevent.pressPosition = pevent.position;
                pevent.pointerPressRaycast = pevent.pointerCurrentRaycast;
                pevent.pointerPress = null;

                GameObject target = pevent.pointerPressRaycast.gameObject;
                current_pressed = ExecuteEvents.ExecuteHierarchy(target, pevent, ExecuteEvents.pointerDownHandler);
                
                if (current_pressed != null)
                {
                    ExecuteEvents.Execute(current_pressed, pevent, ExecuteEvents.beginDragHandler);
                    pevent.pointerDrag = current_pressed;
                }
                else
                {
                    /* some objects have only a pointerClickHandler */
                    current_pressed = target;
                    pevent.pointerDrag = null;
                }
            }
        }

        public override void OnTriggerDrag(Controller controller)
        {
            if (UpdateCurrentPoint(controller, allow_out_of_bounds: true))
            {
                if (pevent.pointerDrag != null)
                    ExecuteEvents.Execute(pevent.pointerDrag, pevent, ExecuteEvents.dragHandler);
            }
        }

        public override void OnTriggerUp(Controller controller)
        {
            bool in_bounds = UpdateCurrentPoint(controller);

            if (pevent.pointerDrag != null)
            {
                ExecuteEvents.Execute(pevent.pointerDrag, pevent, ExecuteEvents.endDragHandler);
                if (in_bounds)
                {
                    ExecuteEvents.ExecuteHierarchy(pevent.pointerDrag, pevent, ExecuteEvents.dropHandler);
                }
                ExecuteEvents.Execute(pevent.pointerDrag, pevent, ExecuteEvents.pointerUpHandler);
                pevent.pointerDrag = null;
            }

            if (in_bounds)
                ExecuteEvents.Execute(current_pressed, pevent, ExecuteEvents.pointerClickHandler);
        }

        public override bool CanStartTeleportAction(Controller controller)
        {
            return false;
        }

        public override float GetPriority(Controller controller)
        {
            Plane plane = new Plane(transform.forward, transform.position);
            Ray ray = new Ray(controller.position, transform.forward);
            float enter;
            if (!plane.Raycast(ray, out enter))
                enter = 0;
            return 100 + enter;
        }
    }
}