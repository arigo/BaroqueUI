using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeText : MonoBehaviour
{
    public void EnteredText(string text)
    {
        GetComponent<TextMesh>().text = text;
    }
}
