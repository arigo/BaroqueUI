using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class RotateMe : MonoBehaviour
{
    public string sceneActionName = "Default";
    public BaroqueUI_Dialog dialogSetAngle;

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
}
