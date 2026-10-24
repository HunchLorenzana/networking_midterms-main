using System.Collections.Generic;
using UnityEngine;

public class PassiveRuntime
{
    private PassiveData secondWind;
    private PassiveData adrenalSurge;
    private PassiveData thickSkin;
    private float lastDamageTime;
    private float nextRegenTime;
    private float surgeEndTime;
    private float nextSurgeTime;
    private bool isThickSkinActive;

    public PassiveData Ricochet { get; private set; }
    public PassiveData Lifesteal { get; private set; }
    public PassiveData Weaken { get; private set; }
    public PassiveData Scavenger { get; private set; }

    public void SetEquipped(IReadOnlyList<PassiveData> equipped, float now)
    {
        Ricochet = null;
        Lifesteal = null;
        Weaken = null;
        Scavenger = null;
        secondWind = null;
        adrenalSurge = null;
        thickSkin = null;

        foreach (PassiveData passive in equipped)
        {
            if (passive == null) continue;

            switch (passive.Effect)
            {
                case PassiveEffect.Ricochet:
                    Ricochet = passive;
                    break;
                case PassiveEffect.Lifesteal:
                    Lifesteal = passive;
                    break;
                case PassiveEffect.Weaken:
                    Weaken = passive;
                    break;
                case PassiveEffect.Scavenger:
                    Scavenger = passive;
                    break;
                case PassiveEffect.SecondWind:
                    secondWind = passive;
                    break;
                case PassiveEffect.AdrenalSurge:
                    adrenalSurge = passive;
                    break;
                case PassiveEffect.ThickSkin:
                    thickSkin = passive;
                    break;
            }
        }

        Reset(now);
    }

    public void Reset(float now)
    {
        lastDamageTime = now;
        nextRegenTime = 0f;
        surgeEndTime = 0f;
        nextSurgeTime = 0f;
        isThickSkinActive = thickSkin != null;
    }

    public void NotifyDamaged(float now)
    {
        lastDamageTime = now;
        isThickSkinActive = false;

        if (adrenalSurge == null || now < nextSurgeTime) return;

        surgeEndTime = now + adrenalSurge.Duration;
        nextSurgeTime = now + adrenalSurge.Cooldown;
    }

    public float GetMoveSpeedFactor(float now)
    {
        return adrenalSurge != null && now < surgeEndTime ? 1f + adrenalSurge.Magnitude : 1f;
    }

    public float GetDamageTakenFactor()
    {
        return isThickSkinActive ? 1f - thickSkin.Magnitude : 1f;
    }

    public int GetRegenAmount(float now, int maxHealth)
    {
        if (secondWind == null || now - lastDamageTime < secondWind.Duration || now < nextRegenTime) return 0;

        nextRegenTime = now + Mathf.Max(0.1f, secondWind.SecondaryMagnitude);

        return Mathf.Max(1, Mathf.RoundToInt(maxHealth * secondWind.Magnitude));
    }
}
