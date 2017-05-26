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
        float angle = ctrl0.triggerVariablePressure - ctrl1.triggerVariablePressure;
        transform.rotation = Quaternion.Euler(0, 0, angle * 90);
	}
}
