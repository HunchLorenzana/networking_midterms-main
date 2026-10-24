using System;
using UnityEngine;

[Serializable]
public class FirePointData
{
    [Tooltip("Index of the held gun this point belongs to (0 is the first gun)")]
    [SerializeField] private int gun;
    [Tooltip("Fire point position in that gun's model space")]
    [SerializeField] private Vector3 offset;
    [Tooltip("Extra yaw in degrees added to the aim direction for shots from this point")]
    [SerializeField] private float angle;

    public int Gun => gun;
    public Vector3 Offset => offset;
    public float Angle => angle;
}
