using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialBlink : MonoBehaviour {

    public float minAlpha = 0.5f;
    public float maxAlpha = 1f;
    public float blinkTime = 1f;

    Material material;

	void Start()
    {
        material = GetComponent<Renderer>().material;
	}
	
	void Update()
    {
        Color col = material.color;
        col.a = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Sin((Time.time / blinkTime) * 2 * Mathf.PI) * 0.5f + 0.5f);
        material.color = col;
	}
}
