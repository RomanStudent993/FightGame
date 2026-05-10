using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    public int damage = 10;
    public float damageCooldown = 0.8f;
    public bool triggerAttackAnimation = true;
    [Tooltip("Короткая пауза перед первым ударом после контакта. Не применяется, если игрок в этот момент реально блокирует удар щитом (см. PlayerShieldDefense).")]
    public float thinkTimeBeforeFirstHit = 0.05f;

    [Header("Откидывание игрока")]
    [Tooltip("Горизонтальная скорость при обычном уроне (без щита).")]
    public float hitKnockbackSpeed = 5.1f;
    [Tooltip("При пробитии щита.")]
    public float shieldBreakKnockbackSpeed = 6.75f;
    [Tooltip("Мгновенный сдвиг позиции при обычном уроне (тот же кадр, что и удар).")]
    public float hitKnockbackSlideDistance = 0.112f;
    [Tooltip("Сдвиг при пробитии щита.")]
    public float shieldBreakKnockbackSlideDistance = 0.165f;
    [Header("Удар по поднятому щиту (без потери HP)")]
    public float shieldBlockKnockbackSpeed = 6.75f;
    public float shieldBlockKnockbackSlideDistance = 0.42f;
    [Tooltip("Коротко глушит горизонтальный ввод героя после отбоя щитом, чтобы следующий FixedUpdate не перезаписал linearVelocity.")]
    public float shieldBlockInputLockDuration = 0.16f;
    [Range(0f, 1f)]
    [Tooltip("Сколько от Shield Block Slide Distance сдвигается за один кадр. Меньше — меньше «телепорт», то же суммарное расстояние размазывается по скорости за время Input Lock.")]
    public float shieldBlockInstantSlidePortion = 0.25f;
    [Tooltip("Минимум для перехода «стоит → отлёт».")]
    public float knockbackInputLockDuration = 0.001f;
    [Tooltip("Минимум при прорыве щита.")]
    public float shieldBreakKnockbackInputLockDuration = 0.002f;
    [Tooltip("Доп. пауза перед следующим ударом врага после пробития щита (чтобы второй удар не слипался).")]
    public float extraEnemyCooldownAfterShieldBreak = 0.35f;

    private float nextDamageTime;
    private Animator animator;
    private int playerTouchCount;
    private float combatEnteredAt = -1f;
    private bool hasStruckInThisEngagement;

    private static AudioClip _shieldBlockClipCached;

    static void PlayShieldBlockSound()
    {
        if (_shieldBlockClipCached == null)
            _shieldBlockClipCached = Resources.Load<AudioClip>("Sounds/sound_shield");
        if (_shieldBlockClipCached == null) return;
        Vector3 p = Vector3.zero;
        if (Camera.main != null) p = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_shieldBlockClipCached, p);
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        RegisterPlayerTouch(collision.collider, true);
        TryDealDamage(collision.collider);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        RegisterPlayerTouch(collision.collider, false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayerTouch(other, true);
        TryDealDamage(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        RegisterPlayerTouch(other, false);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDealDamage(collision.collider);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void RegisterPlayerTouch(Collider2D other, bool enter)
    {
        if (other == null) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        if (enter)
        {
            playerTouchCount++;
            if (playerTouchCount == 1)
            {
                combatEnteredAt = Time.time;
                hasStruckInThisEngagement = false;
            }
        }
        else
        {
            playerTouchCount = Mathf.Max(0, playerTouchCount - 1);
            if (playerTouchCount == 0)
            {
                combatEnteredAt = -1f;
                hasStruckInThisEngagement = false;
            }
        }
    }

    void ApplyKnockbackAwayFromSelf(Rigidbody2D playerRb, Transform playerTransform, float horizontalSpeed, float inputLockDuration, float instantSlideDistance, float instantSlidePortion = 1f)
    {
        if (playerRb == null || horizontalSpeed <= 0f) return;
        float dx = playerTransform.position.x - transform.position.x;
        float push = Mathf.Abs(dx) < 0.02f ? 1f : Mathf.Sign(dx);
        float vx = push * horizontalSpeed;
        PlayerCombatKnockback pk = playerRb != null
            ? (playerRb.GetComponent<PlayerCombatKnockback>() ?? playerRb.GetComponentInChildren<PlayerCombatKnockback>(true))
            : (playerTransform.GetComponent<PlayerCombatKnockback>() ?? playerTransform.GetComponentInChildren<PlayerCombatKnockback>(true));
        if (pk != null)
            pk.ApplyKnockback(vx, inputLockDuration, instantSlideDistance, instantSlidePortion);
        else
        {
            instantSlidePortion = Mathf.Clamp01(instantSlidePortion);
            float instant = instantSlideDistance * instantSlidePortion;
            float deferred = instantSlideDistance * (1f - instantSlidePortion);
            float t = Mathf.Max(inputLockDuration, 0.02f);
            float addV = deferred / t;
            float vx2 = push * (horizontalSpeed + addV);
            if (instant > 0f)
                playerRb.MovePosition(playerRb.position + new Vector2(push * instant, 0f));
            playerRb.linearVelocity = new Vector2(vx2, playerRb.linearVelocity.y);
        }
    }

    void RegisterEnemyHitSlowdown()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.RegisterHitImpactSlowdown();
    }

    private void TryDealDamage(Collider2D other)
    {
        if (other == null) return;
        SimpleHealth selfHp = GetComponentInParent<SimpleHealth>();
        if (selfHp != null && selfHp.IsDead) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;
        if (playerTouchCount <= 0 || combatEnteredAt < 0f) return;

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null && !ai.CanDealContactMelee())
            return;

        if (!hasStruckInThisEngagement && thinkTimeBeforeFirstHit > 0f &&
            Time.time < combatEnteredAt + thinkTimeBeforeFirstHit)
        {
            SimpleHealth healthEarly = other.GetComponentInParent<SimpleHealth>();
            PlayerShieldDefense shieldEarly = healthEarly != null ? healthEarly.GetComponent<PlayerShieldDefense>() : null;
            bool shieldBlocksNow = shieldEarly != null && shieldEarly.ShouldBlockHitFromWorldPosition(transform.position);
            if (!shieldBlocksNow)
                return;
        }

        if (Time.time < nextDamageTime) return;

        nextDamageTime = Time.time + damageCooldown;
        hasStruckInThisEngagement = true;

        SimpleHealth health = other.GetComponentInParent<SimpleHealth>();
        if (health != null)
        {
            PlayerShieldDefense shield = health.GetComponent<PlayerShieldDefense>();
            Rigidbody2D playerRb = other.GetComponentInParent<Rigidbody2D>();
            if (shield != null)
            {
                int damageToApply = damage;
                if (shield.AbsorbMeleeHitIfPossible(transform.position, damage, out damageToApply, out bool brokeShield))
                {
                    PlayShieldBlockSound();
                    ApplyKnockbackAwayFromSelf(playerRb, health.transform, shieldBlockKnockbackSpeed, shieldBlockInputLockDuration, shieldBlockKnockbackSlideDistance, shieldBlockInstantSlidePortion);
                    RegisterEnemyHitSlowdown();
                    if (ai != null)
                        ai.RegisterShieldBlockMeleePause();
                    if (triggerAttackAnimation && animator != null)
                        animator.SetTrigger("Attack");
                    return;
                }

                float kb = brokeShield ? shieldBreakKnockbackSpeed : hitKnockbackSpeed;
                float lockDur = brokeShield ? shieldBreakKnockbackInputLockDuration : knockbackInputLockDuration;
                float slide = brokeShield ? shieldBreakKnockbackSlideDistance : hitKnockbackSlideDistance;
                ApplyKnockbackAwayFromSelf(playerRb, health.transform, kb, lockDur, slide);
                health.TakeDamage(damageToApply);
                RegisterEnemyHitSlowdown();
                if (brokeShield)
                {
                    nextDamageTime += extraEnemyCooldownAfterShieldBreak;
                    if (ai != null)
                        ai.RegisterCriticalShieldBreakMeleePause();
                }
                if (triggerAttackAnimation && animator != null)
                    animator.SetTrigger("Attack");
                return;
            }

            ApplyKnockbackAwayFromSelf(playerRb, health.transform, hitKnockbackSpeed, knockbackInputLockDuration, hitKnockbackSlideDistance);
            health.TakeDamage(damage);
            RegisterEnemyHitSlowdown();
            if (triggerAttackAnimation && animator != null)
            {
                animator.SetTrigger("Attack");
            }
        }
    }
}
