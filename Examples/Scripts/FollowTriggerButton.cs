using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class FollowTriggerButton : MonoBehaviour
{
    public int controllerIndex;

	void FixedUpdate()
    {
        var ctrl = Baroque.GetControllers()[controllerIndex];
        transform.rotation = Quaternion.Euler(0, 0, ctrl.triggerVariablePressure * 90);
	}
}
