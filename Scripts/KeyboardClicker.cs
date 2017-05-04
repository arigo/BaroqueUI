using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaroqueUI;
using UnityEngine.EventSystems;

public class KeyboardClicker : ConcurrentControllerTracker
{
    public InputField inputField;

    static Camera controllerCamera;


    void Start()
    {
        if (controllerCamera == null || !controllerCamera.gameObject.activeInHierarchy)
        {
            controllerCamera = new GameObject("Controller Keyboard Camera").AddComponent<Camera>();
            controllerCamera.clearFlags = CameraClearFlags.Nothing;
            controllerCamera.cullingMask = 0;
            controllerCamera.pixelRect = new Rect { x = 0, y = 0, width = 10, height = 10 };
            controllerCamera.nearClipPlane = 0.001f;
        }
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>())
            canvas.worldCamera = controllerCamera;

        if (locals == null)
            locals = new List<Local>();
        if (key_pressed == null)
            key_pressed = new Dictionary<Image, float>();
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

    Button FindKey(Vector3 position)
    {
        position = transform.InverseTransformPoint(position);
        position.z = -20;
        controllerCamera.transform.position = transform.TransformPoint(position);
        controllerCamera.transform.rotation = transform.rotation;

        PointerEventData pevent = new PointerEventData(EventSystem.current);
        pevent.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */

        List<RaycastResult> results = new List<RaycastResult>();
        foreach (var raycaster in GetComponentsInChildren<GraphicRaycaster>())
            raycaster.Raycast(pevent, results);

        RaycastResult rr;
        if (!BestRaycastResult(results, out rr))
            return null;
        Button btn = rr.gameObject.GetComponentInParent<Button>();
        if (btn == null)
            return null;
        Text txt = btn.GetComponentInChildren<Text>();
        if (txt == null)
            return null;
        return btn;
    }


    class Local
    {
        internal Controller ctrl;
        internal Button highlight;
        internal bool touchpad_down;
    }
    List<Local> locals;
    Dictionary<Image, float> key_pressed;

    public override void OnEnter(Controller controller)
    {
        locals.Add(new Local {
            ctrl = controller,
            highlight = null,
            touchpad_down = controller.touchpadTouched
        });
        controller.SetPointer("Red Ball");
    }

    const float TOTAL_KEY_TIME = 0.5f;

    void SetKeyColor(Button key, float white_fraction)
    {
        Image img = key.GetComponent<Image>();
        float end = Time.time + (1 - white_fraction) * TOTAL_KEY_TIME;
        float end1;
        if (key_pressed.TryGetValue(img, out end1))
            if (end1 > end)
                return;
        key_pressed[img] = end;
    }

    public override void OnMove(Controller[] controllers)
    {
        UpdateKeys();
    
        foreach (var local in locals)
        {
            if (local.ctrl.touchpadTouched && !local.touchpad_down && local.highlight != null)
            {
                string k = local.highlight.gameObject.name;
                if (k == "Backspace")
                {
                    int count = inputField.text.Length;
                    if (count > 0)
                        inputField.text = inputField.text.Substring(0, count - 1);
                }
                else
                {
                    if (k == "Space")
                        k = " ";
                    inputField.text += k;
                }
                SetKeyColor(local.highlight, 0.2f);
                local.ctrl.HapticPulse();
            }
            local.touchpad_down = local.ctrl.touchpadTouched;
        }
    }

    void UpdateKeys(Controller removing = null)
    {
        foreach (var local in locals)
        {
            Button key = (removing == local.ctrl) ? null : FindKey(local.ctrl.position);
            local.highlight = key;

            if (key != null)
                SetKeyColor(key, 0.9f);
        }
    }

    private void Update()
    {
        Image remove_me = null;
        float cur_time = Time.time;

        foreach (var kv in key_pressed)
        {
            float done_fraction;
            if (cur_time >= kv.Value)
            {
                remove_me = kv.Key;
                done_fraction = 1;
            }
            else
            {
                done_fraction = 1 - (kv.Value - cur_time) / TOTAL_KEY_TIME;
            }
            kv.Key.color = Color.Lerp(Color.red, Color.white, done_fraction);
        }
        if (remove_me != null)
            key_pressed.Remove(remove_me);
    }

    public override void OnLeave(Controller controller)
    {
        UpdateKeys(removing: controller);

        for (int i = 0; i < locals.Count; i++)
        {
            if (locals[i].ctrl == controller)
            {
                locals.RemoveAt(i);
                break;
            }
        }
        controller.SetPointer(null);
    }

    public override bool CanStartTeleportAction(Controller controller)
    {
        return false;
    }
}
