using UnityEngine;

public interface IKnockbackable
{
    void ApplyKnockback(Vector3 direction, float distance, float slowMultiplier, float slowSeconds);
    void ApplySlow(float slowMultiplier, float slowSeconds);
}
