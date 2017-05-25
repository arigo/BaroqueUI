using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class ColorMe : MonoBehaviour
{
    void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
    {
        /* the Menu may also be created in advance and reused */
        Material mat = GetComponent<Renderer>().material;
        var menu = new Menu {
            { "Red", () => mat.color = Color.red},
            { "Green", () => mat.color = Color.green},
            { "Blue", () => mat.color = new Color(0.25f, 0.35f, 1)},
            { "White", () => mat.color = Color.white},
        };

        menu.MakePopup(controller, gameObject);
    }
}