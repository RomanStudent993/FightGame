using System.Collections;
using UnityEngine;

/// <summary>
/// Огненная дальняя атака мага: бьёт на расстоянии, после удара иногда отходит назад.
/// </summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Rigidbody2D))]
public class EvilWizardRangedCombat : MonoBehaviour
{
    [Header("Дистанция")]
    [Tooltip("Минимальная дистанция по X для выстрела.")]
    public float attackRangeMin = 0.18f;
    [Tooltip("Максимальная дистанция по X для выстрела.")]
    public float attackRangeMax = 3.35f;
    [Tooltip("На каком расстоянии EnemyAI останавливается перед игроком.")]
    public float preferredStopDistance = 2.35f;
    [Tooltip("Допуск по вертикали (пересечение коллайдеров).")]
    public float verticalOverlapSlack = 0.35f;

    [Header("Атака")]
    public int damage = 25;
    public float damageCooldown = 0.95f;
    public float attackWindup = 0.22f;
    public float thinkTimeBeforeFirstHit = 0.12f;

    [Header("Отход после удара")]
    [Range(0f, 1f)]
    public float retreatChance = 0.72f;
    public float retreatSpeed = 2.6f;
    public float retreatDuration = 0.38f;
    [Tooltip("Случайный доп. отход по X.")]
    public float retreatDistanceExtra = 0.35f;

    [Header("Откидывание игрока")]
    public float hitKnockbackSpeed = 5.1f;
    public float shieldBreakKnockbackSpeed = 6.75f;
    public float hitKnockbackSlideDistance = 0.112f;
    public float shieldBreakKnockbackSlideDistance = 0.165f;
    public float shieldBlockKnockbackSpeed = 3.15f;
    public float shieldBlockKnockbackSlideDistance = 0.098f;
    public float shieldBlockInputLockDuration = 0.12f;
    [Range(0f, 1f)]
    public float shieldBlockInstantSlidePortion = 0.25f;
    public float knockbackInputLockDuration = 0.001f;
    public float shieldBreakKnockbackInputLockDuration = 0.002f;
    public float extraEnemyCooldownAfterShieldBreak = 0.35f;

    [Range(0f, 1f)]
    public float shieldBlockSoundVolume = 0.4f;

    EnemyAI _enemyAi;
    Rigidbody2D _rb;
    Animator _animator;
    Collider2D _bodyCollider;

    float _nextDamageTime;
    float _combatEnteredAt = -1f;
    bool _hasStruckInEngagement;
    float _retreatUntil;
    bool _attackInProgress;
    static AudioClip _shieldBlockClipCached;

    void Awake()
    {
        _enemyAi = GetComponent<EnemyAI>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _bodyCollider = GetComponent<Collider2D>();

        if (_enemyAi != null)
            _enemyAi.stopDistance = preferredStopDistance;

        EnemyContactDamage contact = GetComponent<EnemyContactDamage>();
        if (contact != null)
            contact.enabled = false;
    }

    void Update()
    {
        if (_attackInProgress || Time.time < _retreatUntil) return;

        SimpleHealth selfHp = GetComponent<SimpleHealth>();
        if (selfHp != null && selfHp.IsDead) return;
        if (_enemyAi != null && !_enemyAi.CanDealContactMelee()) return;
        if (Time.time < _nextDamageTime) return;

        Transform player = FindPlayerRoot();
        if (player == null)
        {
            TrackCombatEngagement(false);
            return;
        }

        if (!IsPlayerInFireRange(player))
        {
            TrackCombatEngagement(false);
            return;
        }

        if (!_hasStruckInEngagement && thinkTimeBeforeFirstHit > 0f &&
            _combatEnteredAt >= 0f && Time.time < _combatEnteredAt + thinkTimeBeforeFirstHit)
        {
            SimpleHealth healthEarly = player.GetComponentInChildren<SimpleHealth>(true);
            PlayerShieldDefense shieldEarly = healthEarly != null ? healthEarly.GetComponent<PlayerShieldDefense>() : null;
            bool shieldBlocksNow = shieldEarly != null && shieldEarly.ShouldBlockHitFromWorldPosition(transform.position);
            if (!shieldBlocksNow)
                return;
        }

        StartCoroutine(FireAttackRoutine(player));
    }

    void FixedUpdate()
    {
        if (Time.time >= _retreatUntil) return;

        Transform player = FindPlayerRoot();
        if (player == null)
        {
            _retreatUntil = 0f;
            return;
        }

        float awaySign = 1f;
        if (player != null)
        {
            float sepX = _rb.position.x - player.position.x;
            if (sepX > 0.02f) awaySign = 1f;
            else if (sepX < -0.02f) awaySign = -1f;
            else
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                awaySign = sr != null && sr.flipX ? 1f : -1f;
            }
        }

        _rb.linearVelocity = new Vector2(awaySign * retreatSpeed, _rb.linearVelocity.y);
    }

    IEnumerator FireAttackRoutine(Transform player)
    {
        _attackInProgress = true;

        if (_animator != null)
            _animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindup);

        SimpleHealth selfHp = GetComponent<SimpleHealth>();
        if (selfHp != null && selfHp.IsDead)
        {
            _attackInProgress = false;
            yield break;
        }

        if (_enemyAi != null && !_enemyAi.CanDealContactMelee())
        {
            _attackInProgress = false;
            yield break;
        }

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            _attackInProgress = false;
            yield break;
        }

        if (IsPlayerInFireRange(player))
            ApplyFireHit(player);

        _attackInProgress = false;
    }

    Transform FindPlayerRoot()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null && tagged.activeInHierarchy && IsLikelyPlayerRoot(tagged.transform.root))
            return tagged.transform.root;

        PlayerAttackDamage pad = FindAnyObjectByType<PlayerAttackDamage>();
        if (pad != null && pad.gameObject.activeInHierarchy && IsLikelyPlayerRoot(pad.transform.root))
            return pad.transform.root;

        HeroKnight hero = FindAnyObjectByType<HeroKnight>();
        if (hero != null && hero.gameObject.activeInHierarchy && IsLikelyPlayerRoot(hero.transform.root))
            return hero.transform.root;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            Transform root = all[i].root;
            if (root == null || !root.gameObject.activeInHierarchy) continue;
            string lower = root.name.ToLowerInvariant();
            if ((lower.Contains("hero") || lower.Contains("knight") || lower.Contains("player")) &&
                IsLikelyPlayerRoot(root))
                return root;
        }

        return null;
    }

    bool IsLikelyPlayerRoot(Transform root)
    {
        if (root == null || !root.gameObject.activeInHierarchy) return false;
        if (root.gameObject.scene != gameObject.scene) return false;
        if (root.CompareTag("Player")) return true;
        if (root.GetComponentInChildren<HeroKnight>(true) != null) return true;
        if (root.GetComponentInChildren<PlayerAttackDamage>(true) != null) return true;
        if (root.GetComponentInChildren<PlayerShieldDefense>(true) != null) return true;
        return false;
    }

    bool IsPlayerInFireRange(Transform player)
    {
        if (player == null) return false;

        Collider2D playerCol = player.GetComponentInChildren<Collider2D>();
        if (playerCol == null) return false;

        float dx = Mathf.Abs(player.position.x - transform.position.x);
        if (dx < attackRangeMin || dx > attackRangeMax) return false;

        if (_bodyCollider != null)
        {
            Bounds wb = _bodyCollider.bounds;
            Bounds pb = playerCol.bounds;
            float overlap = Mathf.Min(wb.max.y, pb.max.y) - Mathf.Max(wb.min.y, pb.min.y);
            if (overlap < -verticalOverlapSlack) return false;
        }

        TrackCombatEngagement(true);
        return true;
    }

    void TrackCombatEngagement(bool inRange)
    {
        if (inRange)
        {
            if (_combatEnteredAt < 0f)
            {
                _combatEnteredAt = Time.time;
                _hasStruckInEngagement = false;
            }
        }
        else if (_combatEnteredAt >= 0f)
        {
            _combatEnteredAt = -1f;
            _hasStruckInEngagement = false;
        }
    }

    void ApplyFireHit(Transform player)
    {
        _nextDamageTime = Time.time + damageCooldown;
        _hasStruckInEngagement = true;

        SimpleHealth health = player.GetComponentInChildren<SimpleHealth>(true);
        if (health == null) return;

        PlayerShieldDefense shield = health.GetComponent<PlayerShieldDefense>();
        Rigidbody2D playerRb = health.GetComponentInParent<Rigidbody2D>();

        if (shield != null)
        {
            int damageToApply = damage;
            if (shield.AbsorbMeleeHitIfPossible(transform.position, damage, out damageToApply, out bool brokeShield))
            {
                PlayShieldBlockSound();
                ApplyKnockbackAwayFromSelf(playerRb, health.transform, shieldBlockKnockbackSpeed, shieldBlockInputLockDuration, shieldBlockKnockbackSlideDistance, shieldBlockInstantSlidePortion);
                if (_enemyAi != null)
                {
                    _enemyAi.RegisterHitImpactSlowdown();
                    _enemyAi.RegisterShieldBlockMeleePause();
                }
                MaybeRetreat();
                return;
            }

            float kb = brokeShield ? shieldBreakKnockbackSpeed : hitKnockbackSpeed;
            float lockDur = brokeShield ? shieldBreakKnockbackInputLockDuration : knockbackInputLockDuration;
            float slide = brokeShield ? shieldBreakKnockbackSlideDistance : hitKnockbackSlideDistance;
            ApplyKnockbackAwayFromSelf(playerRb, health.transform, kb, lockDur, slide);
            health.TakeDamage(damageToApply, playDamageSound: !brokeShield);
            if (_enemyAi != null)
            {
                _enemyAi.RegisterHitImpactSlowdown();
                if (brokeShield)
                {
                    _nextDamageTime += extraEnemyCooldownAfterShieldBreak;
                    _enemyAi.RegisterCriticalShieldBreakMeleePause();
                }
            }
            MaybeRetreat();
            return;
        }

        ApplyKnockbackAwayFromSelf(playerRb, health.transform, hitKnockbackSpeed, knockbackInputLockDuration, hitKnockbackSlideDistance);
        health.TakeDamage(damage);
        if (_enemyAi != null)
            _enemyAi.RegisterHitImpactSlowdown();
        MaybeRetreat();
    }

    void MaybeRetreat()
    {
        if (Random.value > retreatChance) return;
        float extra = Random.Range(0f, retreatDistanceExtra);
        _retreatUntil = Time.time + retreatDuration + extra * 0.08f;
    }

    void ApplyKnockbackAwayFromSelf(Rigidbody2D playerRb, Transform playerTransform, float horizontalSpeed, float inputLockDuration, float instantSlideDistance, float instantSlidePortion = 1f)
    {
        if (playerRb == null || horizontalSpeed <= 0f) return;
        float dx = playerTransform.position.x - transform.position.x;
        float push = Mathf.Abs(dx) < 0.02f ? 1f : Mathf.Sign(dx);
        float vx = push * horizontalSpeed;
        PlayerCombatKnockback pk = playerRb.GetComponent<PlayerCombatKnockback>() ?? playerRb.GetComponentInChildren<PlayerCombatKnockback>(true);
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

    void PlayShieldBlockSound()
    {
        if (_shieldBlockClipCached == null)
            _shieldBlockClipCached = Resources.Load<AudioClip>("Sounds/sound_shield");
        if (_shieldBlockClipCached == null) return;
        Vector3 p = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(_shieldBlockClipCached, p, shieldBlockSoundVolume);
    }

    void OnDisable()
    {
        _attackInProgress = false;
        _retreatUntil = 0f;
    }
}
