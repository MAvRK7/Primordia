using System;
using UnityEngine;

public enum DamageStatus
{
    None,
}

/// <summary>
/// The common damage payload used by guns and melee weapons.
/// </summary>
[Serializable]
public readonly struct CombatHit
{
    public CombatHit(
        float damage,
        Vector3 point,
        Vector3 normal,
        Transform attacker,
        DamageStatus status = DamageStatus.None)
    {
        Damage = Mathf.Max(0f, damage);
        Point = point;
        Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
        Attacker = attacker;
        Status = status;
    }

    public float Damage { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public Transform Attacker { get; }
    public DamageStatus Status { get; }
}

public interface IDamageable
{
    bool IsDead { get; }
    void ApplyHit(CombatHit hit);
}

public static class CombatDamageResolver
{
    public static bool TryGetDamageable(Collider collider, out IDamageable damageable)
    {
        damageable = collider != null
            ? collider.GetComponentInParent<IDamageable>()
            : null;

        return damageable != null && !damageable.IsDead;
    }

    public static bool TryApply(
        Collider collider,
        CombatHit hit,
        out IDamageable damageable)
    {
        if (!TryGetDamageable(collider, out damageable))
            return false;

        damageable.ApplyHit(hit);
        return true;
    }
}
