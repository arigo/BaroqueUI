﻿#warning "FIX ME"
#if false
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class NameMe : ControllerTracker
{
    public Dialog dialogSetName;

    public override void OnTriggerDown(Controller controller)
    {
        var popup = dialogSetName.MakePopup(controller, gameObject);
        if (popup == null)
            return;

        popup.Set("InputField", name, onChange: value => 
        {
            name = value;
            Debug.Log("Renamed to: " + name);
        });
    }
}
#endif