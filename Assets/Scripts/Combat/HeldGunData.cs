using System;
using UnityEngine;

[Serializable]
public class HeldGunData
{
    [SerializeField] private HoldHand hand;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Vector3 modelOffset;
    [SerializeField] private Vector3 modelEuler;
    [SerializeField] private float modelScale = 1f;
    [Tooltip("Flips the model sideways, useful for a left-hand copy of a right-hand gun")]
    [SerializeField] private bool mirrorModel;
    [Tooltip("Muzzle position in the model's own space. This gun adds a fire point here")]
    [SerializeField] private Vector3 muzzleOffset;

    public HoldHand Hand => hand;
    public GameObject ModelPrefab => modelPrefab;
    public Vector3 ModelOffset => modelOffset;
    public Vector3 ModelEuler => modelEuler;
    public float ModelScale => modelScale;
    public bool MirrorModel => mirrorModel;
    public Vector3 MuzzleOffset => muzzleOffset;
}
