using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class RotateMe : ControllerTracker
{
    public override void OnTriggerDown(Controller controller)
    {
        Debug.Log("RotateMe::OnTriggerDown: " + controller);
    }
#if false
    public Dialog dialogSetAngle;

    private void Start()
    {
        SceneAction.Register(sceneActionName, gameObject, buttonDown: OnButtonDown);
    }

    private void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
    {
        var popup = new Popup(dialogSetAngle);
        popup.Set("Slider", transform.rotation.eulerAngles.y, onChange: value =>
        {
            Vector3 v = transform.rotation.eulerAngles;
            v.y = value;
            transform.rotation = Quaternion.Euler(v);
        });
        popup.ShowPopup(action, snapshot);
    }

    public void HandleRotation(float angle)
    {
        Vector3 v = transform.rotation.eulerAngles;
        v.y = angle;
        transform.rotation = Quaternion.Euler(v);
    }
#endif
    }
