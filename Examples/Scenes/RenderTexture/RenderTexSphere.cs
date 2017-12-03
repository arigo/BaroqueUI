using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BaroqueUI;


public class RenderTexSphere : MonoBehaviour
{
    public Dialog canvas;
    public Text text;

    Color default_color;

    private void Start()
    {
        default_color = canvas.backgroundColor;
        var ht = Controller.HoverTracker(this);
        ht.onEnter += Ht_onEnter;
        ht.onLeave += Ht_onLeave;
    }

    private void Ht_onEnter(Controller controller)
    {
        canvas.backgroundColor = Color.red;
    }

    private void Ht_onLeave(Controller controller)
    {
        canvas.backgroundColor = default_color;
    }

    private void FixedUpdate()
    {
        text.text = string.Format("{0:F1}", Time.time);
    }
}
