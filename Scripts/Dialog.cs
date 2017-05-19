using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;

namespace BaroqueUI
{
    public class Dialog : ControllerTracker
    {
        [Tooltip("If checked, the dialog box is already placed in world space.  " +
                 "If left unchecked, the dialog box is hidden in a different layer " +
                 "(it can be either in the scene or a prefab) and is used with MakePopup().")]
        public bool alreadyPositioned = false;

        [Tooltip("If checked, the dialog box automatically shows and hides a keyboard (if it has got any InputField).  " +
                 "Otherwise, we assume a keyboard is already connected.")]
        public bool automaticKeyboard = true;

        [Tooltip("For pop-ups, the scale of the dialog box is corrected to this number of units per world space 'meter'.")]
        public float unitsPerMeter = 400;


        public void Set<T>(string widget_name, T value, UnityAction<T> onChange = null)
        {
            _Set(widget_name, value, onChange);
        }

        public T Get<T>(string widget_name)
        {
            return (T)_Get(widget_name);
        }

        public void SetClick(string clickable_widget_name, UnityAction onClick)
        {
            _Set(clickable_widget_name, null, onClick);
        }

        public void SetChoices(string choice_widget_name, List<string> choices)
        {
            var descr = FindWidget(choice_widget_name);
            descr.setter_extra(choices);
        }

        public Dialog MakePopup(Controller controller, GameObject requester = null)
        {
            return ShouldShowPopup(this, requester) ? Instantiate<Dialog>(this).DoShowPopup(controller) : null;
        }


        /*****************************************************************************************/

        static Dialog popupShown;   /* globally, a single one for now */
        static object popupRequester;

        static internal bool ShouldShowPopup(object model, GameObject requester)
        {
            /* If 'requester' is not null, use that to identify the owner of the dialog box
             * and hide if called a second time.  If it is null, then use instead 'model'
             * (the dialog template or the Menu instance). */
            object new_requester = (requester != null ? requester : model);
            bool should_hide = false;
            if (popupShown != null && popupShown)
            {
                should_hide = (popupRequester == new_requester);
                if (should_hide)
                    new_requester = null;
                Destroy(popupShown.gameObject);
            }
            popupShown = null;
            popupRequester = new_requester;
            return !should_hide;
        }

        internal Dialog DoShowPopup(Controller controller)
        {
            Vector3 head_forward = controller.position - BaroqueUI.GetHeadTransform().position;
            Vector3 fw = controller.forward + head_forward.normalized;
            fw.y = 0;
            transform.forward = fw;
            transform.position = controller.position + 0.15f * transform.forward;

            DisplayDialog();
            popupShown = this;
            return this;
        }


        /*****************************************************************************************/


        /* XXX internals are very Javascript-ish, full of multi-level callbacks.
         * It could also be done using a class hierarchy but it feels like it would be pages longer. */
        delegate object getter_fn();
        delegate void setter_fn(object value);
        delegate void deleter_fn();

        struct WidgetDescr {
            internal getter_fn getter;
            internal setter_fn setter;
            internal deleter_fn detach;
            internal setter_fn attach;
            internal setter_fn setter_extra;
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

            Button button = tr.GetComponent<Button>();
            if (button != null)
                return new WidgetDescr() {
                    getter = () => null,
                    setter = (ignored) => { },
                    detach = () => button.onClick.RemoveAllListeners(),
                    attach = (callback) => button.onClick.AddListener((UnityAction)callback),
                };

            Toggle toggle = tr.GetComponent<Toggle>();
            if (toggle != null)
                return new WidgetDescr()
                {
                    getter = () => toggle.isOn,
                    setter = (value) => toggle.isOn = (bool)value,
                    detach = () => toggle.onValueChanged.RemoveAllListeners(),
                    attach = (callback) => toggle.onValueChanged.AddListener((UnityAction<bool>)callback),
                };

            Dropdown dropdown = tr.GetComponent<Dropdown>();
            if (dropdown != null)
                return new WidgetDescr()
                {
                    getter = () => dropdown.value,
                    setter = (value) => dropdown.value = (int)value,
                    setter_extra = (value) => { dropdown.ClearOptions(); dropdown.AddOptions(value as List<string>); },
                    detach = () => dropdown.onValueChanged.RemoveAllListeners(),
                    attach = (callback) => dropdown.onValueChanged.AddListener((UnityAction<int>)callback),
                };

            throw new NotImplementedException(widget_name);
        }

        void _Set(string widget_name, object value, object onChange = null)
        {
            var descr = FindWidget(widget_name);
            if (onChange != null)
                descr.detach();
            descr.setter(value);
            if (onChange != null)
                descr.attach(onChange);
        }

        object _Get(string widget_name)
        {
            var descr = FindWidget(widget_name);
            return descr.getter();
        }


        /*******************************************************************************************/

        static internal int UI_layer = -1;

        static void CreateLayer()
        {
            if (UI_layer >= 0)
                return;

            /* picking two unused consecutive layer numbers...  This is based on
               https://forum.unity3d.com/threads/adding-layer-by-script.41970/reply?quote=2274824 */
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 29; i >= 1; i--)
            {
                SerializedProperty layerSP0 = layers.GetArrayElementAtIndex(i);
                SerializedProperty layerSP1 = layers.GetArrayElementAtIndex(i + 1);
                if (layerSP0.stringValue == "" && layerSP1.stringValue == "")
                {
                    layerSP0.stringValue = "BaroqueUI dialog";
                    layerSP1.stringValue = "BaroqueUI dialog rendering";
                    tagManager.ApplyModifiedProperties();
                    UI_layer = i;
                    BaroqueUI.GetHeadTransform().GetComponent<Camera>().cullingMask &= ~(3 << UI_layer);
                    return;
                }
            }
            throw new Exception("Couldn't find two consecutive unused layers");
        }

        public void DisplayDialog()
        {
            transform.localScale = Vector3.one / unitsPerMeter;
            alreadyPositioned = true;
            gameObject.SetActive(true);
            StartCoroutine(CoSetInitialSelection());
        }

        void OnDestroy()
        {
            StopAutomaticKeyboard();
        }

        void RecSetLayer(int layer, bool includeInactive = false)
        {
            foreach (var child in GetComponentsInChildren<Canvas>(includeInactive))
                child.gameObject.layer = layer;
            foreach (var child in GetComponentsInChildren<CanvasRenderer>(includeInactive))
                child.gameObject.layer = layer;
        }

        IEnumerator UpdateRendering()
        {
            while (this && isActiveAndEnabled)
            {
                UpdateRenderingOnce();
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        void UpdateRenderingOnce(bool includeInactive = false)
        {
            RecSetLayer(UI_layer + 1);   /* this layer is visible for the camera */

            foreach (var canvas in GetComponentsInChildren<Canvas>())
            {
                var rend = canvas.GetComponent<DialogRenderer>();
                if (rend == null)
                {
                    rend = canvas.gameObject.AddComponent<DialogRenderer>();
                    float pixels_per_unit = GetComponent<CanvasScaler>().dynamicPixelsPerUnit;
                    rend.PrepareForRendering(pixels_per_unit);
                }
                rend.Render();
            }

            RecSetLayer(UI_layer, includeInactive);   /* this layer is not visible any more */
        }

        void Start()
        {
            if (!alreadyPositioned)
            {
                gameObject.SetActive(false);
                return;
            }

            CreateLayer();
            UpdateRenderingOnce(includeInactive: true);
            StartCoroutine(UpdateRendering());

            foreach (InputField inputField in GetComponentsInChildren<InputField>(includeInactive: true))
            {
                if (inputField.GetComponent<KeyboardVRInput>() == null)
                    inputField.gameObject.AddComponent<KeyboardVRInput>();
            }

            StartAutomaticKeyboard();
        }

        GameObject SetInitialSelection()
        {
            foreach (var selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(selectable.gameObject, null);
                    selectable.Select();
                    return selectable.gameObject;
                }
            }
            return null;
        }

        IEnumerator CoSetInitialSelection()
        {
            yield return new WaitForSecondsRealtime(0.1f);   /* doesn't work if done immediately :-( */
            SetInitialSelection();
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

        static bool BestRaycastResult(List<RaycastResult> lst, ref RaycastResult best_result)
        {
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
            Vector2 screen_point = GetComponent<DialogRenderer>().ScreenPoint(controller.position);
            pevent.position = screen_point;

            var results = new List<RaycastResult>();
            foreach (var rend in GetComponentsInChildren<DialogRenderer>())
                rend.CustomRaycast(controller.position, results);

            RaycastResult rr = new RaycastResult { depth = -1, screenPosition = screen_point };
            if (!BestRaycastResult(results, ref rr))
            {
                if (!allow_out_of_bounds)
                    return false;
            }
            rr.worldNormal = transform.forward;
            rr.worldPosition = Vector3.ProjectOnPlane(controller.position - transform.position, transform.forward) + transform.position;
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


        /****************************************************************************************/

        KeyboardClicker auto_keyboard;

        void StartAutomaticKeyboard()
        {
            if (automaticKeyboard && GetComponentInChildren<KeyboardVRInput>() != null && auto_keyboard == null)
            {
                GameObject keyboard_prefab = Resources.Load<GameObject>("BaroqueUI/Keyboard");
                RectTransform rtr = transform as RectTransform;
                Vector3 vcorr = rtr.TransformVector(new Vector3(0, rtr.rect.height * rtr.pivot.y, 0));

                Vector3 pos = transform.position - 0.08f * transform.forward - 0.09f * transform.up - vcorr;
                Quaternion rotation = Quaternion.LookRotation(transform.forward);
                rotation = Quaternion.Euler(41.6335f, rotation.eulerAngles.y, 0);
                // note: 41.6335 = atan(0.08/0.09)
                auto_keyboard = Instantiate(keyboard_prefab, pos, rotation).GetComponent<KeyboardClicker>();
                auto_keyboard.enableEnterKey = true;
                auto_keyboard.enableTabKey = false;
                auto_keyboard.enableEscKey = true;

                auto_keyboard.onKeyboardTyping.AddListener(KeyboardTyping);
            }
        }

        void StopAutomaticKeyboard()
        {
            if (auto_keyboard != null && auto_keyboard)
            {
                Destroy(auto_keyboard.gameObject);
            }
            auto_keyboard = null;
        }

        public void KeyboardTyping(KeyboardClicker.EKeyState state, string key)
        {
            GameObject gobj = EventSystem.current.currentSelectedGameObject;
            if (gobj != null && gobj && gobj.transform.IsChildOf(transform))
            {
                /* the selection is already inside this dialog */
            }
            else
            {
                gobj = SetInitialSelection();
                if (gobj == null)
                    return;
            }
            var keyboardVrInput = gobj.GetComponent<KeyboardVRInput>();
            if (keyboardVrInput != null)
                keyboardVrInput.KeyboardTyping(state, key);
        }
    }


    class DialogRenderer : MonoBehaviour
    {
        RenderTexture render_texture;
        Camera ortho_camera;
        Transform quad, back_quad;
        float pixels_per_unit;

        public void PrepareForRendering(float pixels_per_unit)
        {
            RectTransform rtr = transform as RectTransform;
            if (GetComponentInChildren<Collider>() == null)
            {
                Rect r = rtr.rect;
                float zscale = transform.InverseTransformVector(transform.forward * 0.108f).magnitude;

                BoxCollider coll = gameObject.AddComponent<BoxCollider>();
                coll.isTrigger = true;
                coll.size = new Vector3(r.width, r.height, zscale);
                coll.center = new Vector3(r.center.x, r.center.y, zscale * -0.3125f);
            }

            this.pixels_per_unit = pixels_per_unit;
            render_texture = new RenderTexture((int)(rtr.rect.width * pixels_per_unit + 0.5),
                                               (int)(rtr.rect.height * pixels_per_unit + 0.5), 32);
            Transform tr1 = transform.Find("Ortho Camera");
            if (tr1 != null)
                ortho_camera = tr1.GetComponent<Camera>();
            else
                ortho_camera = new GameObject("Ortho Camera").AddComponent<Camera>();
            ortho_camera.enabled = false;
            ortho_camera.transform.SetParent(transform);
            ortho_camera.transform.position = rtr.TransformPoint(
                rtr.rect.width * (0.5f - rtr.pivot.x),
                rtr.rect.height * (0.5f - rtr.pivot.y),
                0);
            ortho_camera.transform.rotation = rtr.rotation;
            ortho_camera.clearFlags = CameraClearFlags.SolidColor;
            ortho_camera.backgroundColor = new Color(1, 1, 1);
            ortho_camera.cullingMask = 2 << Dialog.UI_layer;
            ortho_camera.orthographic = true;
            ortho_camera.orthographicSize = rtr.TransformVector(0, rtr.rect.height * 0.5f, 0).magnitude;
            ortho_camera.nearClipPlane = -10;
            ortho_camera.farClipPlane = 10;
            ortho_camera.targetTexture = render_texture;

            quad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            quad.SetParent(transform);
            quad.position = ortho_camera.transform.position;
            quad.rotation = ortho_camera.transform.rotation;
            quad.localScale = new Vector3(rtr.rect.width, rtr.rect.height, 1);
            quad.gameObject.layer = 0;   /* default */
            DestroyImmediate(quad.GetComponent<Collider>());

            quad.GetComponent<MeshRenderer>().material = Resources.Load<Material>("BaroqueUI/Dialog Material");
            quad.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", render_texture);

            back_quad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            back_quad.SetParent(transform);
            back_quad.position = ortho_camera.transform.position;
            back_quad.rotation = ortho_camera.transform.rotation * Quaternion.LookRotation(new Vector3(0, 0, -1));
            back_quad.localScale = new Vector3(rtr.rect.width, rtr.rect.height, 1);
            back_quad.gameObject.layer = 0;   /* default */
            DestroyImmediate(back_quad.GetComponent<Collider>());

            GetComponent<Canvas>().worldCamera = ortho_camera;
        }

        private void OnDestroy()
        {
            render_texture.Release();
        }

        public void Render()
        {
            ortho_camera.Render();
        }

        public void CustomRaycast(Vector3 world_position, List<RaycastResult> results)
        {
            Vector2 screen_point = ScreenPoint(world_position);
            var canvas = GetComponent<Canvas>();
            var graphicsForCanvas = GraphicRegistry.GetGraphicsForCanvas(canvas);
            for (int i = 0; i < graphicsForCanvas.Count; i++)
            {
                Graphic graphic = graphicsForCanvas[i];
                if (graphic.canvasRenderer.cull)
                    continue;
                if (graphic.depth == -1)
                    continue;
                if (!graphic.raycastTarget)
                    continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screen_point, ortho_camera))
                    continue;
                if (!graphic.Raycast(screen_point, ortho_camera))
                    continue;

                results.Add(new RaycastResult {
                    gameObject = graphic.gameObject,
                    module = GetComponent<GraphicRaycaster>(),
                    index = results.Count,
                    depth = graphic.depth,
                    sortingLayer = canvas.sortingLayerID,
                    sortingOrder = canvas.sortingOrder,
                    screenPosition = screen_point,
                });
            }
        }

        public Vector2 ScreenPoint(Vector3 world_position)
        {
            RectTransform rtr = transform as RectTransform;
            Vector2 local_pos = transform.InverseTransformPoint(world_position);   /* drop the 'z' coordinate */
            local_pos.x += rtr.rect.width * rtr.pivot.x;
            local_pos.y += rtr.rect.height * rtr.pivot.y;
             /* Here, 'local_pos' is in coordinates that match the UI element coordinates.
              * To convert it to the 'screenspace' coordinates of a camera, we need to apply
              * a scaling factor of 'pixels_per_unit'. */
            return local_pos * pixels_per_unit;
        }
    }
}