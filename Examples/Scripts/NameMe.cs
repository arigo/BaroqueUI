using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class NameMe : ControllerTracker
{
    public Dialog dialogSetName;

    GameObject shown;

    public override void OnTriggerDown(Controller controller)
    {
        var popup = new Popup(dialogSetName);
        popup.Set("InputField", name, onChange: value => 
        {
            name = value;
            Debug.Log("Renamed to: " + name);
        });
        popup.ShowPopup(controller, ref shown);
    }
}
