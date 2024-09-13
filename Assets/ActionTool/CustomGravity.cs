using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), ExecuteAlways]
public class CustomGravity : MonoBehaviour
{
    public Vector3 gravityDirection = Vector3.down;
    public float gravityStrength = 9.81f;

    private Rigidbody _rigidbody;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
    }

    public void FixedUpdate()
    {
        _rigidbody.AddForce(gravityDirection * gravityStrength, ForceMode.Acceleration);
    }
}
