using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaroqueUI;


public class EnterDemo2 : MonoBehaviour
{
    public InputField inputField;

    string original_text;

    void Start()
    {
        original_text = inputField.text;
        inputField.ActivateInputField();
    }

    void PreviewKey(string add)
    {
        string s = inputField.text;
        int pos = inputField.caretPosition;

        if (inputField.selectionAnchorPosition != inputField.selectionFocusPosition)
        {
            int i1 = inputField.selectionAnchorPosition;
            int i2 = inputField.selectionFocusPosition;
            if (i1 > i2) { int i3 = i1; i1 = i2; i2 = i3; }
            if (0 <= i1 && i2 <= s.Length)
            {
                s = s.Remove(i1, i2 - i1);
                pos = i1;
            }
        }

        if (pos < 0) pos = 0;
        if (pos > s.Length) pos = s.Length;

        inputField.text = s.Insert(pos, add);
        inputField.caretPosition = pos + add.Length;
        inputField.selectionAnchorPosition = pos;
        inputField.selectionFocusPosition = pos + add.Length;
    }

    void ConfirmKey()
    {
        inputField.selectionAnchorPosition = inputField.selectionFocusPosition = inputField.caretPosition;
        inputField.ForceLabelUpdate();
    }

    public void TypeKey(KeyboardClicker.TypeKey tkey)
    {
        switch (tkey.state)
        {
            case KeyboardClicker.EKeyState.Preview:
                PreviewKey(tkey.key);
                break;

            case KeyboardClicker.EKeyState.Confirm:
                ConfirmKey();
                break;

            case KeyboardClicker.EKeyState.Special_Enter:
                if (inputField.multiLine)
                {
                    PreviewKey("\n");
                    ConfirmKey();
                }
                else
                {
                    original_text = inputField.text;
                    inputField.caretPosition = original_text.Length;
                    inputField.selectionAnchorPosition = 0;
                    inputField.selectionFocusPosition = original_text.Length;
                }
                break;

            case KeyboardClicker.EKeyState.Special_Esc:
                inputField.text = original_text;
                inputField.caretPosition = original_text.Length;
                inputField.selectionAnchorPosition = 0;
                inputField.selectionFocusPosition = original_text.Length;
                break;

            case KeyboardClicker.EKeyState.Special_Backspace:
                string s = inputField.text;
                int stop = inputField.caretPosition;
                int start = stop - 1;

                if (inputField.selectionAnchorPosition != inputField.selectionFocusPosition)
                {
                    start = inputField.selectionAnchorPosition;
                    stop = inputField.selectionFocusPosition;
                    if (start > stop) { int tmp = start; start = stop; stop = tmp; }
                }

                if (start < 0) start = 0;
                if (stop > s.Length) stop = s.Length;
                if (start < stop)
                {
                    inputField.text = s.Remove(start, stop - start);
                    inputField.caretPosition = inputField.selectionAnchorPosition = inputField.selectionFocusPosition = start;
                }
                break;
        }
    }
}