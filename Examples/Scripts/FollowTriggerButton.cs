using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class FollowTriggerButton : MonoBehaviour
{
	void Update()
    {
        var ctrl0 = Baroque.GetControllers()[0];
        var ctrl1 = Baroque.GetControllers()[1];
        transform.rotation = Quaternion.Euler(ctrl0.triggerVariablePressure * 90, 0, ctrl1.triggerVariablePressure * 90);

        var rend = GetComponent<Renderer>();
        rend.material.color = new Color(ctrl0.triggerPressed ? 1f : 0f, ctrl1.triggerPressed ? 1f : 0f, 0);
	}
}
