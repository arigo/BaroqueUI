using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class ColorMe : MonoBehaviour
{
    public string sceneActionName = "Default";

    private void Start()
    {
        SceneAction.Register(sceneActionName, gameObject, buttonDown: OnButtonDown);
    }

    private void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
    {
        Material mat = GetComponent<Renderer>().material;

        var menu = new Menu {
            { "Red", () => mat.color = Color.red},
            { "Green", () => mat.color = Color.green},
            { "Blue", () => mat.color = Color.blue},
            { "White", () => mat.color = Color.white},
        };
        menu.ShowPopup(action, snapshot);
    }
}
