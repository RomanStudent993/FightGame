using System;
using UnityEngine;

/// <summary>Зелье регенерации (Q): полное восстановление HP.</summary>
[RequireComponent(typeof(SimpleHealth))]
public class PlayerRegenerationPotion : MonoBehaviour
{
    public static event Action Used;

    [Tooltip("Временно: зелье можно пить сколько угодно раз за уровень.")]
    [SerializeField] bool unlimitedUses = true;

    bool _usedThisLevel;

    public bool IsUsed => !unlimitedUses && _usedThisLevel;
    public bool CanUse => unlimitedUses || !_usedThisLevel;

    public bool TryBeginHeal()
    {
        return CanUse;
    }

    public void ApplyHeal(SimpleHealth health)
    {
        if ((!unlimitedUses && _usedThisLevel) || health == null || health.IsDead)
            return;

        int healedAmount = Mathf.Max(0, health.MaxHp - health.CurrentHp);
        health.RestoreFullHp();
        if (!unlimitedUses)
            _usedThisLevel = true;

        int popupAmount = healedAmount > 0 ? healedAmount : health.MaxHp;
        DamagePopup.ShowHeal(GetPopupPosition(), popupAmount);

        Used?.Invoke();
    }

    Vector3 GetPopupPosition()
    {
        Vector3 p = transform.position;
        Collider2D bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null)
            p = bodyCollider.bounds.center + Vector3.up * (bodyCollider.bounds.extents.y + 0.15f);
        else
            p += Vector3.up * 0.65f;
        return p;
    }
}
