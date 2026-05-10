using UnityEngine;
using System.Collections.Generic;

public class PlayerAttackDamage : MonoBehaviour
{
    public int damage = 1;
    public float attackRadius = 1.25f;
    public float attackCooldown = 0.35f;
    public bool damageOnlyEnemyTag = true;
    public LayerMask enemyLayer;
    [Tooltip("Запас по Y: удар не проходит, если коллайдер атакующего целиком выше/ниже цели (например стоишь на платформе над врагом).")]
    public float meleeVerticalOverlapPadding = 0.12f;

    private float nextAttackTime;
    private Transform selfRoot;
    private Collider2D attackerCollider;

    private static AudioClip _missClipCached;

    static void PlayMissSound()
    {
        if (_missClipCached == null)
            _missClipCached = Resources.Load<AudioClip>("Sounds/sound_miss");
        if (_missClipCached == null) return;
        Vector3 p = Vector3.zero;
        if (Camera.main != null) p = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_missClipCached, p);
    }

    void Awake()
    {
        selfRoot = transform.root;
        attackerCollider = GetComponent<Collider2D>();
        if (attackerCollider == null)
            attackerCollider = GetComponentInParent<Collider2D>();
    }

    bool CanMeleeHitColliderVertically(Collider2D victim)
    {
        if (victim == null) return false;
        if (attackerCollider == null) return true;
        Bounds pb = attackerCollider.bounds;
        Bounds vb = victim.bounds;
        float pad = meleeVerticalOverlapPadding;
        if (pb.max.y + pad < vb.min.y) return false;
        if (pb.min.y - pad > vb.max.y) return false;
        return true;
    }

    void Update()
    {
        if (Time.time < nextAttackTime) return;

        if (Input.GetMouseButtonDown(0))
        {
            nextAttackTime = Time.time + attackCooldown;
            NotifyEnemiesOfMeleeAttack();
            if (!DealDamage())
                PlayMissSound();
        }
    }

    private void NotifyEnemiesOfMeleeAttack()
    {
        Vector2 origin = transform.position;
        Collider2D[] inRange = enemyLayer.value == 0
            ? Physics2D.OverlapCircleAll(origin, attackRadius)
            : Physics2D.OverlapCircleAll(origin, attackRadius, enemyLayer);

        HashSet<EnemyAI> notified = new HashSet<EnemyAI>();
        for (int i = 0; i < inRange.Length; i++)
        {
            if (!CanMeleeHitColliderVertically(inRange[i])) continue;
            EnemyAI ai = inRange[i].GetComponentInParent<EnemyAI>();
            if (ai == null || notified.Contains(ai)) continue;
            SimpleHealth sh = ai.GetComponentInParent<SimpleHealth>();
            if (sh != null && sh.IsDead) continue;
            notified.Add(ai);
            ai.OnPlayerMeleeAttack(origin);
        }
    }

    /// <returns>true если хотя бы одному врагу нанесён урон</returns>
    private bool DealDamage()
    {
        Collider2D[] hits = enemyLayer.value == 0
            ? Physics2D.OverlapCircleAll(transform.position, attackRadius)
            : Physics2D.OverlapCircleAll(transform.position, attackRadius, enemyLayer);

        HashSet<Transform> damagedRoots = new HashSet<Transform>();
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitRoot = hits[i].transform.root;
            if (hitRoot == selfRoot) continue;
            if (damageOnlyEnemyTag && !hitRoot.CompareTag("Enemy")) continue;
            if (damagedRoots.Contains(hitRoot)) continue;
            if (!CanMeleeHitColliderVertically(hits[i])) continue;

            SimpleHealth health = hits[i].GetComponentInParent<SimpleHealth>();
            if (health != null && !health.IsDead && health.TakeDamage(damage))
                damagedRoots.Add(hitRoot);
        }

        return damagedRoots.Count > 0;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
