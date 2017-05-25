using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class ResizeMe : MonoBehaviour
{
    public Dialog dialogActionButtons;

    void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
    {
        var popup = dialogActionButtons.MakePopup(controller, gameObject);
        if (popup == null)
            return;

        popup.SetClick("Longer", () =>
        {
            Vector3 s = transform.localScale;
            s.y += 0.03f;
            transform.localScale = s;
        });
        popup.SetClick("Shorter", () =>
        {
            Vector3 s = transform.localScale;
            s.y -= 0.03f;
            transform.localScale = s;
        });

        Material mat = GetComponent<Renderer>().material;
        popup.Set("Red", mat.color == Color.red, (toggle) =>
        {
            if (toggle) mat.color = Color.red;
            else mat.color = Color.white;
        });
    }
}