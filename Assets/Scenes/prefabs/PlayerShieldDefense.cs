using UnityEngine;

/// <summary>
/// Щит по ПКМ: блокирует урон только если враг с той стороны, куда смотрит персонаж (SpriteRenderer.flipX как у HeroKnight).
/// После случайного числа успешных блоков (2 или 3) следующий удар пробивает щит: отдача, усиленный урон, короткий локаут блока.
/// </summary>
public class PlayerShieldDefense : MonoBehaviour
{
    [Tooltip("Если true — блок только пока зажата ПКМ (как IdleBlock в HeroKnight).")]
    public bool requireMouseHeld = true;

    [Header("Прорыв щита")]
    [Tooltip("Множитель урона, когда враг «пробивает» щит после лимита блоков.")]
    public float shieldBreakDamageMultiplier = 1.65f;
    [Tooltip("Сколько секунд после прорыва щит не блокирует (ПКМ можно держать — урон не гасится).")]
    public float postBreakBlockLockout = 0.85f;

    SpriteRenderer spriteRenderer;
    private int blocksSuccessfullyStopped;
    private int blocksAllowedBeforeBreak;
    private float blockLockoutUntil;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
    /// Иначе false и damageToApply — сколько нанести (обычный или усиленный при прорыве щита).
    /// brokeShieldThisHit — только что сработал прорыв щита (сильный удар); толчок задаёт EnemyContactDamage.
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
        damageToApply = Mathf.Max(1, Mathf.RoundToInt(baseDamage * shieldBreakDamageMultiplier));
        return false;
    }
}
