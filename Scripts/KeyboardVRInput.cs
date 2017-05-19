using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


namespace BaroqueUI
{
    [Serializable]
    public class KeyboardVREnterEvent : UnityEvent<string>
    {
    }


    internal class KeyboardVRInput : MonoBehaviour, ISelectHandler
    {
        /* This component is automatically added to InputFields by the code in Dialog. 
         * The Dialog displays a VR keyboard and passes the key presses to the selected
         * InputField.
         * 
         * This component can also be manually added to an InputField.  In that case,
         * the 'KeyboardTyping' method here can be added manually to the keyboard's
         * onKeyboardTyping event.  In return, you get 'On Enter' and 'On Esc' events.
         */
        public KeyboardVREnterEvent onEnter;
        public UnityEvent onEsc;

        string original_text;

        InputField GetInputField()
        {
            var inputField = GetComponent<InputField>();
            Debug.Assert(inputField != null, "'KeyboardVRInput' must be put on an object with an 'InputField' component");
            return inputField;
        }

        static void GetBounds(InputField inputField, out int start, out int stop)
        {
            /* mess mess, gives a simpler interface to get the selection */
            int length = inputField.text.Length;
            int p1 = inputField.selectionAnchorPosition;
            int p2 = inputField.selectionFocusPosition;

            if (p1 == p2)
            {
                start = stop = inputField.caretPosition;
            }
            else if (p1 < p2)
            {
                start = p1; stop = p2;
            }
            else
            {
                start = p2; stop = p1;
            }

            if (start < 0) start = 0;
            if (stop < 0) stop = 0;
            if (stop > length) stop = length;
            if (start > stop) start = stop;
        }

        static void SetBounds(InputField inputField, int start, int stop)
        {
            /* mess mess, gives a simpler interface to set the selection */
            inputField.caretPosition = stop;
            inputField.selectionAnchorPosition = start;
            inputField.selectionFocusPosition = stop;
            inputField.ForceLabelUpdate();
        }

        public void OnSelect(BaseEventData eventData)
        {
            original_text = GetInputField().text;
        }

        void PreviewKey(InputField inputField, string add)
        {
            State();
            string s = inputField.text;
            int start, stop;
            GetBounds(inputField, out start, out stop);

            if (start < stop)
                s = s.Remove(start, stop - start);

            inputField.text = s.Insert(start, add);
            SetBounds(inputField, start, start + add.Length);
            State();
        }

        void State()
        {
#if false
            /* debugging only */
            InputField inputField = GetInputField();
            Debug.Log("caretPosition: " + inputField.caretPosition + "    sel: " + inputField.selectionAnchorPosition + " - " + inputField.selectionFocusPosition);
#endif
        }

        void ConfirmKey(InputField inputField)
        {
            State();
            int start, stop;
            GetBounds(inputField, out start, out stop);
            SetBounds(inputField, stop, stop);
            State();
        }

        public void KeyboardTyping(KeyboardClicker.EKeyState state, string key)
        {
            InputField inputField = GetInputField();
            inputField.ActivateInputField();

            switch (state)
            {
                case KeyboardClicker.EKeyState.Preview:
                    PreviewKey(inputField, key);
                    break;

                case KeyboardClicker.EKeyState.Confirm:
                    ConfirmKey(inputField);
                    break;

                case KeyboardClicker.EKeyState.Special_Enter:
                    if (inputField.multiLine)
                    {
                        PreviewKey(inputField, "\n");
                        ConfirmKey(inputField);
                    }
                    else
                    {
                        original_text = inputField.text;
                        SetBounds(inputField, 0, original_text.Length);

                        inputField.DeactivateInputField();    /* in a Dialog, this sends the OnChange event */
                        inputField.ActivateInputField();
                    }
                    if (onEnter != null)
                        onEnter.Invoke(inputField.text);
                    break;

                case KeyboardClicker.EKeyState.Special_Esc:
                    inputField.text = original_text;
                    SetBounds(inputField, 0, original_text.Length);
                    if (onEsc != null)
                        onEsc.Invoke();
                    break;

                case KeyboardClicker.EKeyState.Special_Backspace:
                    string s = inputField.text;
                    int start, stop;
                    GetBounds(inputField, out start, out stop);
                    if (start == stop)
                    {
                        start -= 1;
                        if (start < 0)
                            break;
                    }
                    inputField.text = s.Remove(start, stop - start);
                    SetBounds(inputField, start, start);
                    break;
            }
        }
    }
}