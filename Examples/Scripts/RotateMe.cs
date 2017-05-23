using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class RotateMe : MonoBehaviour
{
    private void Start()
    {
        Controller.Register(this);
    }

    void OnTriggerDown(Controller controller)
    {
        Debug.Log("OnTriggerDown: controller " + controller.index);
    }
    void OnTriggerUp(Controller controller)
    {
        Debug.Log("OnTriggerUp: controller " + controller.index);
    }

#if false
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
#endif
}