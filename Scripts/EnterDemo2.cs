using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaroqueUI;


public class EnterDemo2 : MonoBehaviour
{
    public InputField inputField;
    string last_typed;
    int last_typed_pos;

    void Start()
    {
        inputField.ActivateInputField();
    }

    void EnterText(string add, bool remove = false)
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
                remove = false;
            }
        }
        
        if (pos < 0) pos = 0;
        if (pos > s.Length) pos = s.Length;

        if (remove && last_typed != null && last_typed_pos == pos - last_typed.Length &&
                last_typed_pos + last_typed.Length <= s.Length &&
                s.Substring(last_typed_pos, last_typed.Length) == last_typed)
        {
            s = s.Remove(last_typed_pos, last_typed.Length);
            pos = last_typed_pos;
        }
        inputField.text = s.Insert(pos, add);
        inputField.caretPosition = inputField.selectionAnchorPosition = inputField.selectionFocusPosition = pos + add.Length;
        last_typed = add.Length > 0 ? add : null;
        last_typed_pos = pos;
    }

    public void TypeKey(string key)
    {
        EnterText(key);
    }

    public void TypeKeyReplacement(string newkey)
    {
        EnterText(newkey, remove: true);
    }

    public void TypeBackspace()
    {
        string s = inputField.text;
        int pos = inputField.caretPosition - 1;
        int length = 1;

        if (inputField.selectionAnchorPosition != inputField.selectionFocusPosition)
        {
            int i1 = inputField.selectionAnchorPosition;
            int i2 = inputField.selectionFocusPosition;
            if (i1 > i2) { int i3 = i1; i1 = i2; i2 = i3; }
            if (0 <= i1 && i2 <= s.Length)
            {
                pos = i1;
                length = i2 - i1;
            }
        }

        if (pos < 0) return;
        if (pos + length > s.Length) return;

        inputField.text = s.Remove(pos, length);
        inputField.caretPosition = inputField.selectionAnchorPosition = inputField.selectionFocusPosition = pos;
        last_typed = null;
    }
}