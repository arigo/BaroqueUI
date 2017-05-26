using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class TurnOff : MonoBehaviour
{
    public GameObject turnOffAndOn;

    private void Start()
    {
        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;
    }

    void OnTriggerDown(Controller controller)
    {
        turnOffAndOn.SetActive(!turnOffAndOn.activeSelf);
        GetComponent<Renderer>().material.color = (
            turnOffAndOn.activeSelf ? Color.white : Color.gray);
    }
}
