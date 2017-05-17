using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class RotateMe : ControllerTracker
{
    public Dialog dialogSetAngle;

    public override void OnTriggerDown(Controller controller)
    {
        var popup = dialogSetAngle.MakePopup(controller, gameObject);
        if (popup == null)
            return;

        popup.Set("Slider", transform.rotation.eulerAngles.y, onChange: value =>
        {
            Vector3 v = transform.rotation.eulerAngles;
            v.y = value;
            transform.rotation = Quaternion.Euler(v);
        });
    }

    public void HandleRotation(float angle)
    {
        Vector3 v = transform.rotation.eulerAngles;
        v.y = angle;
        transform.rotation = Quaternion.Euler(v);
    }
}