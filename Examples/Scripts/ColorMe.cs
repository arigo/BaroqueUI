using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class ColorMe : ControllerTracker
{
    public override void OnTriggerDown(Controller controller)
    {
#if false
        Material mat = GetComponent<Renderer>().material;

        var menu = new Menu {
            { "Red", () => mat.color = Color.red},
            { "Green", () => mat.color = Color.green},
            { "Blue", () => mat.color = Color.blue},
            { "White", () => mat.color = Color.white},
        };
        menu.ShowPopup(controller);
#endif
    }
}
