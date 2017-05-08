using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class RotateMe : ControllerTracker
{
    public Dialog dialogSetAngle;

    GameObject shown;

    public override void OnTriggerDown(Controller controller)
    {
        var popup = new Popup(dialogSetAngle);
        popup.Set("Slider", transform.rotation.eulerAngles.y, onChange: value =>
        {
            Vector3 v = transform.rotation.eulerAngles;
            v.y = value;
            transform.rotation = Quaternion.Euler(v);
        });
        popup.ShowPopup(controller, ref shown);
    }

    public void HandleRotation(float angle)
    {
        Vector3 v = transform.rotation.eulerAngles;
        v.y = angle;
        transform.rotation = Quaternion.Euler(v);
    }
}