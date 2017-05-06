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
    const int SCAN_ALTGR = 56;
    const int SCAN_SPACE = 57;
    const int SCAN_EXTRA = 86;   /* non-US keyboards have an extra key here; US keyboard report the same as SCAN_BACKSLASH */


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
        /* these fields control the appearence of the key, not its behaviour */
        internal int scan_code;
        internal string current_text;      /* what to display on the key */
        internal string[] texts;           /* array of 3 strings [normal, shift, altgr] */
        internal Image image;
        internal Text text;
        internal float blink_end;

        internal const float TOTAL_KEY_TIME = 0.5f;

        internal bool Update(bool fallback = false)
        {
            bool update = text.text != current_text;
            if (update)
                text.text = current_text;

            update |= blink_end > 0;
            if (update)
            {
                float done_fraction = 1 - (blink_end - Time.time) / TOTAL_KEY_TIME;
                Color col1 = Color.red, col2 = Color.white;
                if (current_text == "")
                    col2 = new Color(0.9f, 0.9f, 0.9f);
                image.color = Color.Lerp(col1, col2, done_fraction);
                if (done_fraction >= 1)
                {
                    blink_end = 0;
                    if (fallback && current_text == texts[1])
                        current_text = texts[0];    /* automatic fall back */
                }
            }
            return update;
        }

        internal void SetBlink(float white_fraction)
        {
            float end = Time.time + (1 - white_fraction) * TOTAL_KEY_TIME;
            if (end > blink_end)
                blink_end = end;
        }
    }

    Dictionary<Button, KeyInfo> key_infos;


    void Start()
    {
        key_infos = new Dictionary<Button, KeyInfo>();
        Button key_altgr = null;
        bool use_ctrl_alt = false;

        foreach (var btn in GetComponentsInChildren<Button>())
        {
            string name = btn.gameObject.name;
            int scancode;
            if (name.StartsWith("S") && Int32.TryParse(name.Substring(1), out scancode))
            {
                string text0, text1, text2;

                if (scancode == SCAN_BACKSPACE || scancode == SCAN_TAB || scancode == SCAN_ENTER || scancode == SCAN_ALTGR)
                {
                    text0 = text1 = text2 = btn.GetComponentInChildren<Text>().text;
                    if (scancode == SCAN_ALTGR)
                        key_altgr = btn;
                    if (scancode == SCAN_ENTER)
                        text2 = "";
                }
                else if (scancode == SCAN_SPACE)
                {
                    text0 = text1 = text2 = " ";
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
                info.current_text = text0;
                info.texts = new string[] { text0, text1, text2 };
                info.image = btn.GetComponent<Image>();
                info.text = btn.GetComponentInChildren<Text>();
                info.Update();
                key_infos[btn] = info;
            }
        }
        if (!use_ctrl_alt)
        {
            key_infos.Remove(key_altgr);
            Destroy(key_altgr.gameObject);
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

    KeyInfo FindKey(Vector3 position, float dx = 0, float dy = 0)
    {
        position = transform.InverseTransformPoint(position);
        position.x += dx;
        position.y += dy;
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
        internal int touchpad_down;        /* 0: no, 1: touched, 2: pressed */
        internal KeyInfo altgr_touched;    /* set to the KeyInfo for the alt key when we touch it, until we release the touch */
        internal KeyInfo just_touched;     /* set to the KeyInfo for other keys when we touch, until we either release or press the touchpad */
        internal bool shift_outside;       /* set to true if we touch the touchpad outside of any key */
    }
    List<Local> locals;
    int is_active;       /* set to zero when both controllers are away from the keyboard and the key blinks is done */
    int keys_displayed;    /* mode currently displayed for all keys [0-2] */

    public override void OnEnter(Controller controller)
    {
        locals.Add(new Local {
            ctrl = controller,
            touchpad_down = controller.touchpadPressed ? 2 : controller.touchpadTouched ? 1 : 0
        });
        controller.SetPointer("Red Ball");
        is_active = 3;
    }

    void KeyTouch(Local local, KeyInfo key, bool shift)
    {
        if (keys_displayed < 2)
            key.current_text = key.texts[shift ? 1 : 0];

        if (key.current_text == "")    /* occurs if keys_displayed == 2 */
            return;

        key.SetBlink(0.2f);

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

            case SCAN_ALTGR:
                return;     /* no haptic pulse */

            default:
                inputField.text += key.current_text;
                break;
        }
        local.ctrl.HapticPulse(500);
    }

    void KeyShiftingPress(Local local, KeyInfo key)
    {
        switch (key.scan_code)
        {
            case SCAN_BACKSPACE:
            case SCAN_TAB:
            case SCAN_ENTER:
            case SCAN_ALTGR:
                return;

            default:
                if (keys_displayed < 2)
                    key.current_text = key.texts[1];

                if (key.current_text == "")    /* typical if keys_displayed == 2 */
                    return;

                int count = inputField.text.Length;
                if (count > 0)
                    inputField.text = inputField.text.Substring(0, count - 1);  /* xxx */
                inputField.text += key.current_text;
                break;
        }
        key.SetBlink(0.15f);
        local.ctrl.HapticPulse(900);
    }

    public override void OnMove(Controller[] controllers)
    {
        foreach (var local in locals)
        {
            KeyInfo key = FindKey(local.ctrl.position);
            if (key != null)
                key.SetBlink(0.91f);

            if (!local.ctrl.touchpadTouched)
            {
                /* Touchpad is not touched.  Cancel all state */
                local.touchpad_down = 0;
                local.shift_outside = false;
                if (local.altgr_touched != null)
                {
                    local.altgr_touched = null;
                    local.ctrl.GrabFromScript(false);
                }
                continue;
            }

            /* Touched is touched. */
            switch (local.touchpad_down)
            {
                case 0:    /* touchpad was not touched previously */
                    local.just_touched = null;
                    if (key != null)
                    {
                        if (key.scan_code == SCAN_ALTGR)
                        {
                            local.altgr_touched = key;
                            local.ctrl.GrabFromScript(true);
                        }
                        else
                            local.just_touched = key;

                        KeyTouch(local, key, shift: keys_displayed == 1);
                    }
                    else
                    {
                        /* only if pressing far enough from any key */
                        const float d = 9f;
                        if (FindKey(local.ctrl.position, +d, +d) == null &&
                            FindKey(local.ctrl.position, +d, -d) == null &&
                            FindKey(local.ctrl.position, -d, +d) == null &&
                            FindKey(local.ctrl.position, -d, -d) == null)
                            local.shift_outside = true;
                    }
                    local.touchpad_down = 1;
                    break;

                case 1:    /* touchpad was already touched (but not pressed) previously */
                    if (local.ctrl.touchpadPressed)
                    {
                        if (key != null)
                        {
                            if (local.just_touched == key)
                                KeyShiftingPress(local, key);
                            else
                                KeyTouch(local, key, shift: true);
                        }
                        local.touchpad_down = 2;
                    }
                    else if (local.just_touched != key)
                    {
                        local.just_touched = null;
                    }
                    break;

                case 2:     /* touchpad was pressed previously */
                    if (!local.ctrl.touchpadPressed)
                    {
                        local.just_touched = null;
                        local.touchpad_down = 1;
                    }
                    break;
            }
        }

        UpdateAltGr();
    }

    void UpdateAltGr()
    {
        int mode = 0;

        foreach (var local in locals)
        {
            if (local.altgr_touched != null)
            {
                local.altgr_touched.SetBlink(0.6f);
                mode = 2;
            }
            else if (mode == 0 && local.shift_outside)
                mode = 1;
        }

        if (mode != keys_displayed)
        {
            foreach (var info in key_infos.Values)
                info.current_text = info.texts[mode];
            keys_displayed = mode;
        }
    }


    /*float last_update = 0;*/

    private void Update()
    {
        /*if (Time.time >= last_update + 0.5f)
        {
            last_update = Time.time;
            string s = last_update + "     " + is_active + "  keys_displayed: " + keys_displayed;
            foreach (var local in locals)
                s += "  [" + local.touchpad_down + " " + (local.altgr_touched != null ? "altgr" : "") + " "
                    + (local.just_touched != null ? "just_touched:" + local.just_touched.texts[0] : "") + " "
                    + (local.shift_outside ? "shift_outside" : "") + "]";
            Debug.Log(s);
        }*/

        if (is_active != 0)
        {
            is_active &= ~1;
            foreach (var info in key_infos.Values)
                if (info.Update(fallback: keys_displayed == 0))
                    is_active |= 1;
        }
    }

    public override void OnLeave(Controller controller)
    {
        for (int i = 0; i < locals.Count; i++)
        {
            if (locals[i].ctrl == controller)
            {
                locals.RemoveAt(i);
                break;
            }
        }
        UpdateAltGr();
        controller.SetPointer(null);
        if (locals.Count == 0)
            is_active = 1;
    }

    public override bool CanStartTeleportAction(Controller controller)
    {
        return false;
    }
}
