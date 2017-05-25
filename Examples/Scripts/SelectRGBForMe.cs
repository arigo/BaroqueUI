using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class SelectRGBForMe : MonoBehaviour
{
    public Dialog selectRGBDialog;

    void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
    {
        Material mat = GetComponent<Renderer>().material;

        var popup = selectRGBDialog.MakePopup(controller, gameObject);
        if (popup == null)
            return;

        Color col1 = mat.color;
        int red_component   = col1.r < 0.25f ? 0 : col1.r < 0.75f ? 1 : 2;
        int green_component = col1.g < 0.25f ? 0 : col1.g < 0.75f ? 1 : 2;
        int blue_component  = col1.b < 0.25f ? 0 : col1.b < 0.75f ? 1 : 2;

        popup.Set("Red Dropdown", red_component, onChange: value =>
        {
            Color ncol = mat.color;
            ncol.r = value * 0.5f;
            mat.color = ncol;
        });
        popup.Set("Green Dropdown", green_component, onChange: value =>
        {
            Color ncol = mat.color;
            ncol.g = value * 0.5f;
            mat.color = ncol;
        });
        popup.Set("Blue Dropdown", blue_component, onChange: value =>
        {
            Color ncol = mat.color;
            ncol.b = value * 0.5f;
            mat.color = ncol;
        });
    }
}