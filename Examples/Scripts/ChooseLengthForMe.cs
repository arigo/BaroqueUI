using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class ChooseLengthForMe : MonoBehaviour
{
    public Dialog dialogChooseLength;

    void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
    {
        var popup = dialogChooseLength.MakePopup(controller, gameObject);
        if (popup == null)
            return;

        popup.SetChoices("DynDropdown", new List<string> {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"
        });
        popup.Set("DynDropdown", (int)(transform.localScale.y * 25f + 0.5f) - 1, value =>
        {
            Vector3 s = transform.localScale;
            s.y = (value + 1) / 25f;
            transform.localScale = s;
        });
    }
}