using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaroqueUI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;

public class KeyboardClicker : ConcurrentControllerTracker
{
    public InputField inputField;

    static Camera controllerCamera;

    GameObject[] pointers;


    [DllImport("user32.dll")]
    public static extern int ToUnicode(uint virtualKeyCode, uint scanCode, byte[] keyboardState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
            System.Text.StringBuilder receivingBuffer, int bufferSize, uint flags);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);
    const uint MAPVK_VSC_TO_VK = 1;

    const int VK_SHIFT = 0x10;
    const int VK_CONTROL = 0x11;
    const int VK_MENU = 0x12;
    const int VK_SPACE = 0x20;

    const int SCAN_BACKSPACE = 14;
    const int SCAN_TAB = 15;
    const int SCAN_ENTER = 28;
    const int SCAN_BACKSLASH = 43;
    const int SCAN_EXTRA = 86;   /* non-US keyboards have an extra key here; US keyboard report the same as SCAN_BACKSLASH */
    const int SCAN_TOTAL = 87;


    static string GetCharsFromKeys(int scancode, bool shift, bool altGr)
    {
        uint keys = MapVirtualKey((uint)scancode, MAPVK_VSC_TO_VK);

        var buf = new System.Text.StringBuilder(128);
        var keyboardState = new byte[256];
        if (shift)
            keyboardState[VK_SHIFT] = 0xff;
        if (altGr)
        {
            keyboardState[VK_CONTROL] = 0xff;
            keyboardState[VK_MENU] = 0xff;
        }
        int result = ToUnicode(keys, (uint)scancode, keyboardState, buf, 128, 0);
        if (result == -1)
        {
            /* dead keys seem to be stored inside Windows somewhere, so we need to clear 
             * it out e.g. by sending a ToUnicode(VK_SPACE) */
            keyboardState[VK_SHIFT] = 0;
            keyboardState[VK_CONTROL] = 0;
            keyboardState[VK_MENU] = 0;
            result = ToUnicode(VK_SPACE, 0, keyboardState, buf, 128, 0);
        }
        return buf.ToString(0, result);
    }


    class KeyInfo
    {
        internal int scan_code;
        internal int mode;
        internal string[] texts;
        internal Image image;
        internal Text text;
        internal float blink_end;

        const float TOTAL_KEY_TIME = 0.5f;

        internal bool Update()
        {
            if (text.text != texts[mode])
                text.text = texts[mode];

            if (blink_end > 0)
            {
                float done_fraction = 1 - (blink_end - Time.time) / TOTAL_KEY_TIME;
                Color col = Color.red;
                switch (mode)
                {
                    case 1: col = new Color(1, 0.898f, 0); break;
                    case 2: col = new Color(0, 0.710f, 1); break;
                }
                image.color = Color.Lerp(col, Color.white, done_fraction);
                if (done_fraction >= 1)
                {
                    blink_end = 0;
                    mode = 0;
                }
                return true;
            }
            else
                return false;
        }

        internal void SetBlink(float white_fraction)
        {
            float end = Time.time + (1 - white_fraction) * TOTAL_KEY_TIME;
            if (end > blink_end)
                blink_end = end;
        }
    }

    Dictionary<Button, KeyInfo> key_infos;
    bool use_ctrl_alt;


    void Start()
    {
        key_infos = new Dictionary<Button, KeyInfo>();

        foreach (var btn in GetComponentsInChildren<Button>())
        {
            string name = btn.gameObject.name;
            int scancode;
            if (name.StartsWith("S") && Int32.TryParse(name.Substring(1), out scancode))
            {
                string text0, text1, text2;

                if (scancode == SCAN_BACKSPACE || scancode == SCAN_TAB || scancode == SCAN_ENTER)
                {
                    text0 = text1 = text2 = btn.GetComponentInChildren<Text>().text;
                }
                else
                {
                    text0 = GetCharsFromKeys(scancode, false, false);
                    text1 = GetCharsFromKeys(scancode, true, false);
                    text2 = GetCharsFromKeys(scancode, false, true);

                    if (scancode == SCAN_EXTRA && text0 == GetCharsFromKeys(SCAN_BACKSLASH, false, false)
                                               && text1 == GetCharsFromKeys(SCAN_BACKSLASH, true, false)
                                               && text2 == GetCharsFromKeys(SCAN_BACKSLASH, false, true))
                        text0 = "";    /* key SCAN_EXTRA is completely equivalent to SCAN_BACKSLASH, hide it */

                    if (text0 == "")
                    {
                        Destroy(btn.gameObject);
                        continue;
                    }
                    use_ctrl_alt |= (text2 != "");
                }
                var info = new KeyInfo();
                info.scan_code = scancode;
                info.texts = new string[] { text0, text1, text2 };
                info.image = btn.GetComponent<Image>();
                info.text = btn.GetComponentInChildren<Text>();
                info.Update();
                key_infos[btn] = info;
            }
        }

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

        pointers = new GameObject[]
        {
            BaroqueUI.BaroqueUI.GetPointerObject("Red Ball"),
            BaroqueUI.BaroqueUI.GetPointerObject("Yellow Ball"),
            BaroqueUI.BaroqueUI.GetPointerObject("Cyan Ball"),
        };
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

    KeyInfo FindKey(Vector3 position)
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
        KeyInfo result;
        return key_infos.TryGetValue(btn, out result) ? result : null;
    }


    class Local
    {
        internal Controller ctrl;
        internal KeyInfo highlight;
        internal bool touchpad_down;
        internal int mode;
        internal bool show_all_keys;
    }
    List<Local> locals;
    int is_active;

    public override void OnEnter(Controller controller)
    {
        locals.Add(new Local {
            ctrl = controller,
            highlight = null,
            touchpad_down = controller.touchpadTouched
        });
        controller.SetPointerPrefab(pointers[0]);
        is_active = 3;
    }

    public override void OnMove(Controller[] controllers)
    {
        UpdateKeys();

        foreach (var local in locals)
        {
            KeyInfo key = local.highlight;
            int old_mode = local.mode;

            if (local.ctrl.touchpadTouched)
            {
                Vector2 xy = local.ctrl.touchpadPosition;
                if (xy.x > 0.7f && use_ctrl_alt)
                    local.mode = 2;
                else if (xy.y > 0.7f)
                    local.mode = 1;
                else
                    local.mode = 0;

                if (key != null)
                    key.mode = local.mode;

                if (!local.touchpad_down)
                {
                    if (key != null)
                    {
                        switch (key.scan_code)
                        {
                            case SCAN_BACKSPACE:
                                int count = inputField.text.Length;
                                if (count > 0)
                                    inputField.text = inputField.text.Substring(0, count - 1);
                                break;

                            case SCAN_TAB:
                                inputField.text += "    ";   /* XXX */
                                break;

                            case SCAN_ENTER:
                                inputField.text = "";   /* XXX */
                                break;

                            default:
                                inputField.text += key.texts[key.mode];
                                break;
                        }
                        key.SetBlink(0.2f);
                        local.ctrl.HapticPulse();
                    }
                    else
                        local.show_all_keys = true;
                }
                if (local.show_all_keys)
                {
                    foreach (var info in key_infos.Values)
                    {
                        info.mode = local.mode;
                        info.SetBlink(0.93f);
                    }
                }
            }
            else
            {
                local.show_all_keys = false;
                if (key == null || key.mode != local.mode)
                    local.mode = 0;
            }

            local.touchpad_down = local.ctrl.touchpadTouched;
            if (local.mode != old_mode)
                local.ctrl.SetPointerPrefab(pointers[local.mode]);
        }
    }

    void UpdateKeys(Controller removing = null)
    {
        foreach (var local in locals)
        {
            KeyInfo key = (removing == local.ctrl) ? null : FindKey(local.ctrl.position);
            local.highlight = key;

            if (key != null)
                key.SetBlink(0.9f);
        }
    }

    private void Update()
    {
        if (is_active != 0)
        {
            bool any_update = false;
            foreach (var info in key_infos.Values)
                any_update |= info.Update();
            if (!any_update)
                is_active &= ~1;
            else
                is_active |= 1;
        }
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
        is_active = 1;
    }

    public override bool CanStartTeleportAction(Controller controller)
    {
        return false;
    }
}
