using DG.Tweening;
using UnityEngine;

public class HeldWeaponVisual : MonoBehaviour
{
    private const int SLOT_COUNT = 2;
    private const int MAX_GUNS = 4;
    private const float FLICK_SECONDS = 0.24f;
    private const float SPIN_SECONDS = 0.42f;
    private const float RAISE_SECONDS = 0.3f;
    private const float RAISE_SETTLE_SECONDS = 0.12f;
    private const float RAISE_START_ANGLE = -80f;
    private const float RAISE_OVERSHOOT_ANGLE = 10f;
    private const float SPIN_START_SCALE = 0.5f;

    [SerializeField] private Transform mount;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private BotBrain botBrain;

    private readonly GameObject[] models = new GameObject[SLOT_COUNT * MAX_GUNS];
    private readonly WeaponData[] shownWeapons = new WeaponData[SLOT_COUNT];
    private readonly Vector3[] basePositions = new Vector3[SLOT_COUNT * MAX_GUNS];
    private readonly Quaternion[] baseRotations = new Quaternion[SLOT_COUNT * MAX_GUNS];
    private readonly Vector3[] baseScales = new Vector3[SLOT_COUNT * MAX_GUNS];

    private int shownSlot = -1;

    void LateUpdate()
    {
        int activeSlot = playerWeapon != null ? playerWeapon.ActiveSlot : botBrain.ActiveSlot;

        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            WeaponData weapon = playerWeapon != null ? playerWeapon.GetWeapon(slot) : botBrain.GetWeapon(slot);

            if (weapon != shownWeapons[slot])
            {
                shownWeapons[slot] = weapon;
                Rebuild(slot, weapon);
                shownSlot = -1;
            }
        }

        if (activeSlot == shownSlot) return;

        bool isFirstShow = shownSlot < 0;

        shownSlot = activeSlot;

        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            for (int gun = 0; gun < MAX_GUNS; gun++)
            {
                GameObject model = models[slot * MAX_GUNS + gun];

                if (model != null)
                {
                    model.SetActive(slot == activeSlot);
                }
            }
        }

        if (!isFirstShow)
        {
            PlaySwitchAnimation(activeSlot);
        }
    }

    public bool TryGetMuzzle(int slot, out Vector3 worldPosition)
    {
        return TryGetMuzzle(slot, 0, out worldPosition);
    }

    public bool TryGetMuzzle(int slot, int pointIndex, out Vector3 worldPosition)
    {
        WeaponData weapon = shownWeapons[slot];
        GameObject model = weapon == null ? null : models[slot * MAX_GUNS + Mathf.Min(weapon.GetFirePointGun(pointIndex), MAX_GUNS - 1)];

        if (model == null)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = model.transform.TransformPoint(weapon.GetFirePointOffset(pointIndex));

        return true;
    }

    private void Rebuild(int slot, WeaponData weapon)
    {
        for (int gun = 0; gun < MAX_GUNS; gun++)
        {
            int index = slot * MAX_GUNS + gun;

            if (models[index] == null) continue;

            models[index].transform.DOKill();
            Destroy(models[index]);
            models[index] = null;
        }

        if (weapon == null) return;

        int gunCount = Mathf.Min(weapon.GunCount, MAX_GUNS);

        for (int gun = 0; gun < gunCount; gun++)
        {
            GameObject prefab = weapon.GetGunModel(gun);

            if (prefab == null) continue;

            int index = slot * MAX_GUNS + gun;
            GameObject model = Instantiate(prefab, mount);
            Transform prefabTransform = prefab.transform;
            Vector3 scale = prefabTransform.localScale * weapon.GetGunScale(gun);

            if (weapon.GetGunMirror(gun))
            {
                scale.x = -scale.x;
            }

            model.transform.localPosition = weapon.GetGunOffset(gun);
            model.transform.localRotation = Quaternion.Euler(weapon.GetGunEuler(gun)) * prefabTransform.localRotation;
            model.transform.localScale = scale;
            basePositions[index] = model.transform.localPosition;
            baseRotations[index] = model.transform.localRotation;
            baseScales[index] = model.transform.localScale;
            models[index] = model;
        }
    }

    private void PlaySwitchAnimation(int slot)
    {
        for (int gun = 0; gun < MAX_GUNS; gun++)
        {
            int index = slot * MAX_GUNS + gun;

            if (models[index] == null) continue;

            Transform target = models[index].transform;

            target.DOKill();
            target.localPosition = basePositions[index];
            target.localRotation = baseRotations[index];
            target.localScale = baseScales[index];

            switch (shownWeapons[slot].SwitchAnimation)
            {
                case WeaponSwitchAnimation.Flick:
                    PlayFlick(target, index);
                    break;
                case WeaponSwitchAnimation.Spin:
                    PlaySpin(target, index);
                    break;
                case WeaponSwitchAnimation.Raise:
                    PlayRaise(target, index);
                    break;
            }
        }
    }

    private void PlayFlick(Transform target, int index)
    {
        target.localRotation = Quaternion.Euler(60f, 0f, 0f) * baseRotations[index];
        target.localPosition = basePositions[index] + new Vector3(0f, -0.3f, -0.15f);
        target.localScale = baseScales[index] * 0.8f;

        target.DOLocalRotateQuaternion(baseRotations[index], FLICK_SECONDS).SetEase(Ease.OutBack).SetLink(target.gameObject);
        target.DOLocalMove(basePositions[index], FLICK_SECONDS).SetEase(Ease.OutBack).SetLink(target.gameObject);
        target.DOScale(baseScales[index], FLICK_SECONDS * 0.8f).SetEase(Ease.OutQuad).SetLink(target.gameObject);
    }

    private void PlaySpin(Transform target, int index)
    {
        ApplyPivotPitch(target, index, 360f);
        target.localScale = baseScales[index] * SPIN_START_SCALE;

        DOVirtual.Float(0f, 1f, SPIN_SECONDS, progress =>
        {
            ApplyPivotPitch(target, index, 360f * (1f - progress));
            target.localScale = baseScales[index] * Mathf.Lerp(SPIN_START_SCALE, 1f, Mathf.Clamp01(progress / 0.7f));
        }).SetEase(Ease.OutCubic).SetTarget(target).SetLink(target.gameObject);
    }

    private void PlayRaise(Transform target, int index)
    {
        ApplyPivotPitch(target, index, RAISE_START_ANGLE);

        Sequence sequence = DOTween.Sequence().SetTarget(target).SetLink(target.gameObject);

        sequence.Append(DOVirtual.Float(RAISE_START_ANGLE, RAISE_OVERSHOOT_ANGLE, RAISE_SECONDS, angle => ApplyPivotPitch(target, index, angle)).SetEase(Ease.OutCubic));
        sequence.Append(DOVirtual.Float(RAISE_OVERSHOOT_ANGLE, 0f, RAISE_SETTLE_SECONDS, angle => ApplyPivotPitch(target, index, angle)).SetEase(Ease.OutQuad));
    }

    private void ApplyPivotPitch(Transform target, int index, float angle)
    {
        Quaternion delta = Quaternion.AngleAxis(angle, Vector3.right);

        target.localPosition = delta * basePositions[index];
        target.localRotation = delta * baseRotations[index];
    }
}
