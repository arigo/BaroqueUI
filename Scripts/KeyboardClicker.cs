using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;


namespace BaroqueUI
{
    [Serializable]
    public class KeyboardTypingEvent : UnityEvent<KeyboardClicker.EKeyState, string>
    {
        /* KeyboardClicker sends a KeyboardTypingEvent to report actions about the VR keyboard keys.

           Apart from the special keys (enter, esc, tab, backspace), all regular keys are sent in the
           following sequence:
           - Event(Preview);
           - optionally more Event(Preview), each replacing the current key;
           - one final Event(Confirm), always with the same key as the most recent Event(Preview).

           The special keys cannot occur between a Event(Preview) and a Event(Confirm).
           This script will always confirm a previewed key before that.  In one case this script can
           cancel a previewed key by emitting Event(Preview, "") and Event(Confirm, "").

           Dead keys are sent as, say, Event(Preview, "^").  Then it might be replaced with a
           Event(Preview, "â") followed by Event(Confirm, "â").  Or the dead key alone can be
           confirmed, e.g. if we press the space bar or a letter that can't combine with the dead key.

           There are two different ways to use this:

           * If you can easily accept corrections in output, e.g. if it is sent to an inputField,
             then write the Previewed key immediately but replace them with further inputs until
             you get the Confirm.  The Previewed key can be selected; then it is natural that they
             are replaced when different keys are Previewed, and gives some color feedback.

           * If you can't correct the output, then ignore Preview and only emit keys when you get
             the Confirm.
         */
    }


    public class KeyboardClicker : MonoBehaviour
    {
        public enum EKeyState { Preview, Confirm, Special_Backspace, Special_Tab, Special_Enter, Special_Esc };

        public KeyboardTypingEvent onKeyboardTyping;
        public bool enableTabKey = true;
        public bool enableEnterKey = true;
        public bool enableEscKey = false;
        public float controllerPriority = 200;


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

        const int SCAN_UNKNOWN = -1;
        const int SCAN_ESC = 1;
        const int SCAN_BACKSPACE = 14;
        const int SCAN_TAB = 15;
        const int SCAN_ENTER = 28;
        const int SCAN_BACKSLASH = 43;
        const int SCAN_ALTGR = 56;
        const int SCAN_SPACE = 57;
        const int SCAN_EXTRA = 86;   /* many European keyboards have an extra key here; US keyboards report the same as SCAN_BACKSLASH */

        const string DEAD_KEY = "[DEAD_KEY]";


        static string GetCharsFromKeys(int scancode, bool shift, bool altGr, int next_scancode = 0, bool next_shift = false)
        {
            uint key = MapVirtualKey((uint)scancode, MAPVK_VSC_TO_VK);
            uint next_key = next_scancode > 0 ? MapVirtualKey((uint)next_scancode, MAPVK_VSC_TO_VK) : key;

            var buf = new System.Text.StringBuilder(128);
            var keyboardState = new byte[256];
            if (shift)
                keyboardState[VK_SHIFT] = 0xff;
            if (altGr)
            {
                keyboardState[VK_CONTROL] = 0xff;
                keyboardState[VK_MENU] = 0xff;
            }
            int result = ToUnicode(key, (uint)scancode, keyboardState, buf, 128, 0);
            if (result == -1)
            {
                /* dead keys seem to be stored inside Windows somewhere, so we need to clear
                 * it out in all cases.  That's why we send by default the dead key twice. */
                if (next_scancode == 0)
                {
                    ToUnicode(key, (uint)scancode, keyboardState, buf, 128, 0);
                    return DEAD_KEY + buf.ToString(0, 1);
                }
                else
                {
                    keyboardState[VK_SHIFT] = (byte)(next_shift ? 0xff : 0);
                    keyboardState[VK_CONTROL] = 0;
                    keyboardState[VK_MENU] = 0;
                    result = ToUnicode(next_key, (uint)next_scancode, keyboardState, buf, 128, 0);
                }
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

            internal const float TOTAL_KEY_TIME = 0.3f;

            internal bool Update(bool fallback = false)
            {
                string display_text = current_text.StartsWith(DEAD_KEY) ? current_text.Substring(DEAD_KEY.Length) : current_text;
                bool update = text.text != display_text;
                if (update)
                    text.text = display_text;

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
            Dictionary<int, string[]> all_regular_scancodes = new Dictionary<int, string[]>();

            foreach (var btn in GetComponentsInChildren<Button>())
            {
                string name = btn.gameObject.name;
                int scancode;
                string text0, text1, text2;

                if (name.StartsWith("S") && Int32.TryParse(name.Substring(1), out scancode))
                {
                    if (scancode == SCAN_BACKSPACE || scancode == SCAN_TAB || scancode == SCAN_ENTER ||
                        scancode == SCAN_ALTGR || scancode == SCAN_ESC)
                    {
                        if (scancode == SCAN_TAB && !enableTabKey ||
                            scancode == SCAN_ENTER && !enableEnterKey ||
                            scancode == SCAN_ESC && !enableEscKey)
                        {
                            Destroy(btn.gameObject);
                            continue;
                        }
                        text0 = text1 = text2 = btn.GetComponentInChildren<Text>().text;
                        if (scancode == SCAN_ALTGR)
                            key_altgr = btn;
                        if (scancode == SCAN_ENTER)
                            text2 = "";
                    }
                    else if (scancode == SCAN_SPACE)
                    {
                        text0 = text1 = text2 = " ";
                        all_regular_scancodes.Add(scancode, new string[] { text0, text1 });
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
                        all_regular_scancodes.Add(scancode, new string[] { text0, text1 });
                    }
                }
                else if (name.StartsWith("K-") && name.Length == 4)
                {
                    scancode = SCAN_UNKNOWN;
                    text0 = name[2].ToString();
                    text1 = name[3].ToString();
                    text2 = "";
                }
                else
                    continue;

                var info = new KeyInfo();
                info.scan_code = scancode;
                info.current_text = text0;
                info.texts = new string[] { text0, text1, text2 };
                info.image = btn.GetComponent<Image>();
                info.text = btn.GetComponentInChildren<Text>();
                info.Update();
                key_infos[btn] = info;
            }
            if (!use_ctrl_alt && key_altgr != null)
            {
                key_infos.Remove(key_altgr);
                Destroy(key_altgr.gameObject);
            }
            dead_keys_combinations = new Dictionary<string, Dictionary<string, string>>();
            foreach (var info in key_infos.Values)
            {
                AddDeadKeyCombination(all_regular_scancodes, info.texts[0], info.scan_code, false, false);
                AddDeadKeyCombination(all_regular_scancodes, info.texts[1], info.scan_code, true, false);
                AddDeadKeyCombination(all_regular_scancodes, info.texts[2], info.scan_code, false, true);
            }

            foreach (Canvas canvas in GetComponentsInChildren<Canvas>())
                canvas.worldCamera = GetControllerCamera();

            locals = new List<Local>();

            var ct = Controller.HoverTracker(this);
            ct.SetPriorityFromDistance(controllerPriority);
            ct.isConcurrent = true;
            ct.onEnter += OnEnter;
            ct.onControllersUpdate += OnControllersUpdate;
            ct.onLeave += OnLeave;
            ct.onTouchDown += (ctrl) => { };   /* needed to grab interest in the touchpad */
        }

        void AddDeadKeyCombination(Dictionary<int, string[]> all_regular_scancodes, string key_text,
                                   int scan_code, bool with_shift, bool with_altgr)
        {
            if (!key_text.StartsWith(DEAD_KEY))
                return;

            var k = key_text.Substring(DEAD_KEY.Length);
            dead_keys_combinations[k] = new Dictionary<string, string>();
            foreach (var kv in all_regular_scancodes)
            {
                for (int i = 1; i >= 0; i--)
                {
                    if (kv.Value[i].StartsWith(DEAD_KEY))
                        continue;
                    string combined = GetCharsFromKeys(scan_code, with_shift, with_altgr, next_scancode: kv.Key, next_shift: i == 1);
                    if (combined != k + kv.Value[i])
                        dead_keys_combinations[k][kv.Value[i]] = combined;
                }
            }
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

            var pevent = MoveControllerCamera(transform.TransformPoint(position), transform.forward);

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


        /*********************************************************************************************/

        static Camera controllerCamera;   /* See Dialog.ortho_camera for comments about this */

        static Camera GetControllerCamera()
        {
            if (controllerCamera == null)   // includes 'has been destroyed'
            {
                controllerCamera = new GameObject("Controller Camera").AddComponent<Camera>();
                controllerCamera.clearFlags = CameraClearFlags.Nothing;
                controllerCamera.cullingMask = 0;
                controllerCamera.pixelRect = new Rect { x = 0, y = 0, width = 10, height = 10 };
                controllerCamera.nearClipPlane = 0.001f;
            }
            return controllerCamera;
        }

        static PointerEventData MoveControllerCamera(Vector3 position, Vector3 forward)
        {
            PointerEventData pevent = new PointerEventData(EventSystem.current);
            Camera camera = GetControllerCamera();
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(forward);
            pevent.position = new Vector2(5, 5);   /* at the center of the 10x10-pixels "camera" */
            return pevent;
        }


        class Local
        {
            internal Controller ctrl;
            internal int touchpad_down;        /* 0: no, 1: touched, 2: pressed */
            internal KeyInfo altgr_touched;    /* set to the KeyInfo for the alt key when we touch it, until we release the touch */
            internal KeyInfo just_touched;     /* set to the KeyInfo for other keys when we touch, until we either release or press the touchpad */
            internal bool shift_outside;       /* set to true if we touch the touchpad outside of any key */
            internal bool dead_key_touchpad_not_released;    /* set to false whenever the touchpad is released; used for dead keys */
        }
        List<Local> locals;
        int is_active;       /* set to zero when both controllers are away from the keyboard and the key blinks is done */
        int keys_displayed;    /* mode currently displayed for all keys [0 - 2] */
        string combine_keys_displayed;    /* with 'keys_displayed', if non-null, show potential combinations from dead key */
        KeyInfo shifting_single_key;

        /* dead keys: built inside a dict mapping strings like "^" to dicts mapping "i" to "î" 
         */
        Dictionary<string, Dictionary<string, string>> dead_keys_combinations;
        string dead_key_last;    /* the key last typed that is a dead key, e.g. "^" on French keyboards. */
        int dead_key_status;    /* 0: no dead key; 1: pressing/pressed the dead key; 2: pressing/pressed the follow-up key */

        string unconfirmed_key;
        Local unconfirmed_key_local;


        protected virtual void OnEnter(Controller controller)
        {
            locals.Add(new Local
            {
                ctrl = controller,
                touchpad_down = controller.touchpadPressed ? 2 : controller.touchpadTouched ? 1 : 0
            });
            controller.SetPointer("Red Ball");
            is_active = 3;
        }

        void SendKey(EKeyState state, string key)
        {
            onKeyboardTyping.Invoke(state, key);
        }

        void SpecialKey(EKeyState state, string key)
        {
            dead_key_status = 0;
            if (unconfirmed_key != null)
            {
                if (state == EKeyState.Special_Backspace)
                {
                    /* internally handle Backspace as a cancel if there is an unconfirmed key */
                    SendKey(EKeyState.Preview, "");
                    SendKey(EKeyState.Confirm, "");
                    unconfirmed_key = null;
                    return;
                }
                ConfirmKey();
            }
            SendKey(state, key);
        }

        void KeyCombine(Local local, string k, bool replacement)
        {
            if (k.StartsWith(DEAD_KEY))
            {
                dead_key_status = (dead_key_status == 1 && !replacement) ? 0 : 1;
                dead_key_last = k.Substring(DEAD_KEY.Length);
                k = dead_key_last;
                if (!replacement && dead_key_status == 1)
                    local.dead_key_touchpad_not_released = true;
            }
            else if (dead_key_status >= 1)
            {
                if (dead_key_status == 2 && !replacement)
                {
                    dead_key_status = 0;    /* already done, we're typing the following key now */
                }
                else if (dead_key_status == 1 && replacement)
                {
                    dead_key_status = 0;    /* we're replacing a dead key with a non-dead shifted key */
                }
                else
                {
                    string k2;
                    if (dead_keys_combinations[dead_key_last].TryGetValue(k, out k2))
                    {
                        /* replace (below) the dead key with k2, which is the combined character */
                    }
                    else
                    {
                        k2 = dead_key_last + k;   /* add the non-combining letter (but it can still change later) */
                    }
                    SendKey(EKeyState.Preview, k2);
                    unconfirmed_key = k2;
                    unconfirmed_key_local = local;
                    dead_key_status = 2;
                    return;
                }
            }
            if (!replacement)
                ConfirmKey();
            SendKey(EKeyState.Preview, k);
            unconfirmed_key = k;
            unconfirmed_key_local = local;
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
                case SCAN_BACKSPACE: SpecialKey(EKeyState.Special_Backspace, ""); break;
                case SCAN_TAB: SpecialKey(EKeyState.Special_Tab, "\t"); break;
                case SCAN_ENTER: SpecialKey(EKeyState.Special_Enter, "\n"); break;
                case SCAN_ESC: SpecialKey(EKeyState.Special_Esc, ""); break;

                case SCAN_ALTGR:
                    return;     /* no haptic pulse */

                default:
                    KeyCombine(local, key.current_text, replacement: false);
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
                case SCAN_ESC:
                case SCAN_ALTGR:
                    return;

                default:
                    if (keys_displayed < 2)
                    {
                        key.current_text = key.texts[1];
                        shifting_single_key = key;
                    }

                    if (key.current_text == "")    /* typical if keys_displayed == 2 */
                        return;

                    KeyCombine(local, key.current_text, replacement: true);
                    break;
            }
            key.SetBlink(0.15f);
            local.ctrl.HapticPulse(900);
        }

        void ConfirmKey()
        {
            if (unconfirmed_key != null)
            {
                SendKey(EKeyState.Confirm, unconfirmed_key);
                unconfirmed_key = null;
            }
            unconfirmed_key_local = null;
        }

        void ConfirmKeyLocal(Local local)
        {
            if (unconfirmed_key_local == local && dead_key_status != 1)
                ConfirmKey();
        }

        protected virtual void OnControllersUpdate(Controller[] controllers)
        {
            foreach (var local in locals)
            {
                KeyInfo key = FindKey(local.ctrl.position);
                if (key != null)
                    key.SetBlink(0.91f);

                if (!local.ctrl.touchpadTouched)
                {
                    /* Touchpad is not touched.  Confirm the key and cancel all other state */
                    ConfirmKeyLocal(local);
                    local.touchpad_down = 0;
                    local.shift_outside = false;
                    if (local.dead_key_touchpad_not_released)
                        local.dead_key_touchpad_not_released = false;
                    if (local.altgr_touched != null)
                    {
                        local.altgr_touched = null;
                        local.ctrl.GrabHover(false);
                    }
                    continue;
                }

                /* Touchpad is touched. */
                switch (local.touchpad_down)
                {
                    case 0:    /* touchpad was not touched previously */
                        local.just_touched = null;
                        if (key != null)
                        {
                            if (key.scan_code == SCAN_ALTGR)
                            {
                                local.altgr_touched = key;
                                local.ctrl.GrabHover(true);
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
                        if (local.just_touched != key)
                        {
                            /* moving the controller away from the 'just_touched' key: confirm the key, if
                             * it was touched by this controller */
                            local.just_touched = null;
                            ConfirmKeyLocal(local);
                        }
                        if (local.ctrl.touchpadPressed)
                        {
                            if (key != null)
                            {
                                if (local.just_touched == key)
                                    KeyShiftingPress(local, key);
                                else
                                    KeyTouch(local, key, shift: !(dead_key_status == 1 && local.dead_key_touchpad_not_released));
                                ConfirmKeyLocal(local);
                            }
                            local.touchpad_down = 2;
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

            string combine = null;
            if (dead_key_status == 1 && dead_keys_combinations.ContainsKey(dead_key_last))
                combine = dead_key_last;

            if (mode != keys_displayed || combine != combine_keys_displayed)
            {
                foreach (var info in key_infos.Values)
                {
                    if (info == shifting_single_key)
                        continue;
                    string k = info.texts[mode];
                    if (combine != null && dead_keys_combinations[combine].ContainsKey(k))
                    {
                        string k2 = dead_keys_combinations[combine][k];
                        if (!k2.EndsWith(k))
                            k = k2;
                    }
                    info.current_text = k;
                }
                keys_displayed = mode;
                combine_keys_displayed = combine;
            }
            shifting_single_key = null;
        }


        /*float last_update = 0;*/

        void Update()
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

        protected virtual void OnLeave(Controller controller)
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
            controller.SetPointer("");
            if (locals.Count == 0)
                is_active = 1;
        }
    }
}