using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HeroDisplay : MonoBehaviour
{
    [SerializeField] private Transform turntable;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float idleTurnSpeed = 15f;
    [SerializeField] private float dragTurnSpeed = 0.4f;

    private readonly List<GameObject> gunModels = new List<GameObject>();

    private WeaponData shownWeapon;
    private bool isDragging;

    void Update()
    {
        Pointer pointer = Pointer.current;

        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            isDragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        if (pointer == null || !pointer.press.isPressed)
        {
            isDragging = false;
        }

        float turn = isDragging ? -pointer.delta.ReadValue().x * dragTurnSpeed : idleTurnSpeed * Time.deltaTime;

        turntable.Rotate(0f, turn, 0f, Space.World);
    }

    public void ShowWeapon(WeaponData weapon)
    {
        if (weapon == shownWeapon) return;

        shownWeapon = weapon;

        foreach (GameObject model in gunModels)
        {
            Destroy(model);
        }

        gunModels.Clear();

        if (weapon == null) return;

        for (int gun = 0; gun < weapon.GunCount; gun++)
        {
            GameObject prefab = weapon.GetGunModel(gun);

            if (prefab == null) continue;

            GameObject model = Instantiate(prefab, holdPoint);
            Transform prefabTransform = prefab.transform;
            Vector3 scale = prefabTransform.localScale * weapon.GetGunScale(gun);

            if (weapon.GetGunMirror(gun))
            {
                scale.x = -scale.x;
            }

            model.transform.localPosition = weapon.GetGunOffset(gun);
            model.transform.localRotation = Quaternion.Euler(weapon.GetGunEuler(gun)) * prefabTransform.localRotation;
            model.transform.localScale = scale;
            gunModels.Add(model);
        }
    }
}
