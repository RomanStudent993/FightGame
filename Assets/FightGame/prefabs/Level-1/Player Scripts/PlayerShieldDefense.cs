using UnityEngine;

/// <summary>
/// Щит по ПКМ: блокирует урон только если враг с той стороны, куда смотрит персонаж (SpriteRenderer.flipX как у HeroKnight).
/// После случайного числа успешных блоков (2 или 3) следующий удар пробивает щит: отдача и обычный урон врага, короткий локаут блока.
/// </summary>
public class PlayerShieldDefense : MonoBehaviour
{
    [Tooltip("Если true — блок только пока зажата ПКМ (как IdleBlock в HeroKnight).")]
    public bool requireMouseHeld = true;

    [Header("Прорыв щита")]
    [Tooltip("Сколько секунд после прорыва щит не блокирует (ПКМ можно держать — урон не гасится).")]
    public float postBreakBlockLockout = 0.85f;

    [Header("Звук")]
    [Tooltip("Воспроизводится в момент прорыва щита (после 2–3 блоков и толчка).")]
    public AudioClip shieldBreakSound;

    SpriteRenderer spriteRenderer;
    AudioSource audioSource;
    private int blocksSuccessfullyStopped;
    private int blocksAllowedBeforeBreak;
    private float blockLockoutUntil;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        RollBlocksBeforeBreak();
    }

    void RollBlocksBeforeBreak()
    {
        blocksAllowedBeforeBreak = Random.Range(2, 4);
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
    /// Иначе false и damageToApply — сколько нанести (при прорыве щита тот же базовый урон, что и у обычного удара врага).
    /// brokeShieldThisHit — только что сработал прорыв щита; сильный толчок задаёт EnemyContactDamage.
    /// </summary>
    public bool AbsorbMeleeHitIfPossible(Vector2 attackerWorldPosition, int baseDamage, out int damageToApply, out bool brokeShieldThisHit)
    {
        damageToApply = baseDamage;
        brokeShieldThisHit = false;

        if (Time.time < blockLockoutUntil) return false;
        if (!IsShieldRaised()) return false;
        if (!IsAttackerInShieldArc(attackerWorldPosition)) return false;

        if (blocksSuccessfullyStopped < blocksAllowedBeforeBreak)
        {
            blocksSuccessfullyStopped++;
            damageToApply = 0;
            return true;
        }

        blocksSuccessfullyStopped = 0;
        RollBlocksBeforeBreak();
        blockLockoutUntil = Time.time + postBreakBlockLockout;
        brokeShieldThisHit = true;
        damageToApply = Mathf.Max(1, baseDamage);
        PlayShieldBreakSound();
        return false;
    }

    void PlayShieldBreakSound()
    {
        if (shieldBreakSound == null) return;
        if (audioSource != null)
            audioSource.PlayOneShot(shieldBreakSound);
        else
            AudioSource.PlayClipAtPoint(shieldBreakSound, transform.position);
    }
}
