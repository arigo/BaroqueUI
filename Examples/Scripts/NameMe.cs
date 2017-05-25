using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class NameMe : MonoBehaviour
{
    public Dialog dialogSetName;

    void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
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