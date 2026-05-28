using UnityEngine;

/// <summary>
/// Щит по ПКМ: блокирует урон только если враг с той стороны, куда смотрит персонаж (SpriteRenderer.flipX как у HeroKnight).
/// После <see cref="blocksBeforeShieldBreak"/> успешных блоков подряд следующий удар пробивает щит: отдача, фиксированный урон <see cref="shieldBreakHpLoss"/> и звук прорыва.
/// Если опустить щит (отпустить ПКМ при <see cref="requireMouseHeld"/>), счётчик блоков сбрасывается.
/// </summary>
public class PlayerShieldDefense : MonoBehaviour
{
    [Tooltip("Если true — блок только пока зажата ПКМ (как IdleBlock в HeroKnight).")]
    public bool requireMouseHeld = true;

    [Header("Отражение урона")]
    [Tooltip("При успешном блоке часть входящего урона возвращается атакующему врагу.")]
    public bool reflectDamageOnBlock = true;
    [Tooltip("Доля входящего урона, наносимая врагу при блоке (1 = весь урон).")]
    [Range(0f, 2f)]
    public float reflectDamageMultiplier = 1f;
    [Tooltip("Радиус поиска врага в точке удара (позиция атакующего).")]
    public float reflectAttackerFindRadius = 0.9f;

    [Header("Прорыв щита")]
    [Tooltip("Сколько ударов подряд по поднятому щиту выдерживает, прежде чем следующий пробьёт (3 = три блока, четвёртый удар — 40 HP).")]
    public int blocksBeforeShieldBreak = 3;
    [Tooltip("Сколько секунд после прорыва щит не блокирует (ПКМ можно держать — урон не гасится).")]
    public float postBreakBlockLockout = 0.85f;
    [Tooltip("HP при пробитии щита (удар после серии блоков).")]
    public int shieldBreakHpLoss = 40;

    [Header("Звук")]
    [Tooltip("Воспроизводится в момент прорыва щита (после 2–3 блоков и толчка).")]
    public AudioClip shieldBreakSound;
    [Tooltip("Громкость звука прорыва щита (0–1).")]
    [Range(0f, 1f)]
    public float shieldBreakSoundVolume = 0.9f;

    SpriteRenderer spriteRenderer;
    AudioSource audioSource;
    private int blocksSuccessfullyStopped;
    private float blockLockoutUntil;
    private bool wasShieldRaised;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        bool raised = IsShieldRaised();
        if (wasShieldRaised && !raised)
            ResetBlockStreak();
        wasShieldRaised = raised;
    }

    void ResetBlockStreak()
    {
        blocksSuccessfullyStopped = 0;
    }

    /// <summary>Направление «вперёд» персонажа по горизонтали: +1 вправо, −1 влево (как m_facingDirection в HeroKnight).</summary>
    public int GetFacingSign()
    {
        if (spriteRenderer == null) return 1;
        return spriteRenderer.flipX ? -1 : 1;
    }

    public bool IsShieldRaised()
    {
        if (!requireMouseHeld) return true;
        return Input.GetMouseButton(1);
    }

    bool IsAttackerInShieldArc(Vector2 attackerWorldPosition)
    {
        float dx = attackerWorldPosition.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.02f) return false;
        int attackerSide = dx > 0f ? 1 : -1;
        return attackerSide == GetFacingSign();
    }

    /// <summary>Возвращает true, если урон с позиции атакующего должен быть заблокирован (без учёта лимита блоков).</summary>
    public bool ShouldBlockHitFromWorldPosition(Vector2 attackerWorldPosition)
    {
        if (!IsShieldRaised()) return false;
        if (Time.time < blockLockoutUntil) return false;
        return IsAttackerInShieldArc(attackerWorldPosition);
    }

    /// <summary>
    /// Пытается погасить удар щитом. Возвращает true, если урон полностью заблокирован (damageToApply = 0).
    /// Иначе false и damageToApply — сколько нанести (при прорыве щита — <see cref="shieldBreakHpLoss"/>).
    /// brokeShieldThisHit — только что сработал прорыв щита; сильный толчок задаёт EnemyContactDamage.
    /// </summary>
    public bool AbsorbMeleeHitIfPossible(Vector2 attackerWorldPosition, int baseDamage, out int damageToApply, out bool brokeShieldThisHit)
    {
        return AbsorbMeleeHitIfPossible(attackerWorldPosition, baseDamage, out damageToApply, out brokeShieldThisHit, allowReflectDamage: true);
    }

    public bool AbsorbMeleeHitIfPossible(Vector2 attackerWorldPosition, int baseDamage, out int damageToApply, out bool brokeShieldThisHit, bool allowReflectDamage)
    {
        damageToApply = baseDamage;
        brokeShieldThisHit = false;

        if (Time.time < blockLockoutUntil) return false;
        if (!IsShieldRaised()) return false;
        if (!IsAttackerInShieldArc(attackerWorldPosition)) return false;

        int need = Mathf.Max(1, blocksBeforeShieldBreak);
        if (blocksSuccessfullyStopped < need)
        {
            blocksSuccessfullyStopped++;
            damageToApply = 0;
            if (allowReflectDamage)
                TryReflectDamageToAttacker(attackerWorldPosition, baseDamage);
            return true;
        }

        ResetBlockStreak();
        blockLockoutUntil = Time.time + postBreakBlockLockout;
        brokeShieldThisHit = true;
        damageToApply = Mathf.Max(1, shieldBreakHpLoss);
        PlayShieldBreakSound();
        return false;
    }

    void PlayShieldBreakSound()
    {
        if (shieldBreakSound == null) return;
        if (audioSource != null)
            audioSource.PlayOneShot(shieldBreakSound, shieldBreakSoundVolume);
        else
            AudioSource.PlayClipAtPoint(shieldBreakSound, transform.position, shieldBreakSoundVolume);
    }

    void TryReflectDamageToAttacker(Vector2 attackerWorldPosition, int incomingDamage)
    {
        if (!reflectDamageOnBlock || incomingDamage <= 0 || reflectDamageMultiplier <= 0f) return;

        int reflected = Mathf.Max(1, Mathf.RoundToInt(incomingDamage * reflectDamageMultiplier));
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackerWorldPosition, reflectAttackerFindRadius);
        SimpleHealth best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == transform.root) continue;
            if (!root.CompareTag("Enemy")) continue;

            SimpleHealth hp = col.GetComponentInParent<SimpleHealth>();
            if (hp == null || hp.IsDead) continue;

            float distSq = ((Vector2)hp.transform.position - attackerWorldPosition).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = hp;
            }
        }

        if (best == null || !best.TakeDamage(reflected)) return;

        EnemyAI ai = best.GetComponent<EnemyAI>();
        if (ai != null)
            ai.RegisterHitImpactSlowdown();
    }
}
