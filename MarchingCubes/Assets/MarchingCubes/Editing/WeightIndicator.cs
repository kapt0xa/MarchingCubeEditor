using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeightIndicator : MonoBehaviour
{
    [HideInInspector] public float weight { get; private set; } = 1;
    [SerializeField] Material white;
    [SerializeField] Material black;
    MeshRenderer indicator;
    [SerializeField] float default_scale = 0.3f;
    [HideInInspector] public bool is_updated = true;
    [HideInInspector] public bool is_sign_changed = true;
    public bool is_sign_change_alowed = true;

    // Start is called before the first frame update
    void Start()
    {
        indicator = GetComponent<MeshRenderer>();
        is_sign_changed = is_sign_change_alowed;
        OnMouseOver();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void SetWeight(float weight_val)
    {
        weight = weight_val;
        is_updated = true;
        is_sign_changed = is_sign_change_alowed;
        OnMouseOver();
    }

    private void OnMouseOver()
    {
        bool sign_was = CubeTable.IsPositive(weight);
        float weight_was = weight;
        weight += Input.mouseScrollDelta.y * 0.1f;
        if (Input.GetKey(KeyCode.Keypad0))
        {
            weight = 0;
        }
        if (Input.GetKey(KeyCode.Minus))
        {
            weight = -1;
        }
        if (Input.GetKey(KeyCode.Equals))
        {
            weight = 1;
        }
        if (weight > 1) { weight = 1; }
        if (weight < -1) { weight = -1; }
        if (weight_was != weight)
        {
            is_updated = true;
        }
        if (CubeTable.IsPositive(weight) != sign_was)
        {
            if (is_sign_change_alowed)
            {
                is_sign_changed = true;
            }
            else
            {
                weight = weight_was;
            }
        }

        if (is_updated)
        {
            float size_abs = Math.Abs(weight);
            if (size_abs < 0.1f)
            {
                size_abs = 0.1f;
            }
            if (weight > 0)
            {
                indicator.material = white;
            }
            else
            {
                indicator.material = black;
            }

            transform.localScale = default_scale * new Vector3(size_abs, size_abs, size_abs);

        }
    }
}
