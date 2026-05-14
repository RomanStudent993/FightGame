using System;
using System.Collections;
using UnityEngine;

public class SimpleHealth : MonoBehaviour
{
    /// <summary>Вызывается один раз при смерти этого объекта (после установки isDead).</summary>
    public static event Action<GameObject> Died;
    public int maxHp = 5;
    [Tooltip("Раньше использовалось для Destroy; труп остаётся в сцене до выхода из игры.")]
    public float destroyDelay = 0.35f;
    public float spawnInvulnerability = 0.75f;

    private int currentHp;
    private bool isDead;
    private Animator animator;
    private float invulnerableUntil;

    private static AudioClip _deathClipCached;
    private static AudioClip _damageClipCached;

    [Header("Звук")]
    [Tooltip("Громкость sound_damage (0–1).")]
    [Range(0f, 1f)]
    [SerializeField] float damageSoundVolume = 0.38f;
    [Tooltip("Громкость sound_death (0–1).")]
    [Range(0f, 1f)]
    [SerializeField] float deathSoundVolume = 0.55f;

    void PlayDamageSound()
    {
        if (_damageClipCached == null)
            _damageClipCached = Resources.Load<AudioClip>("Sounds/sound_damage");
        if (_damageClipCached == null) return;
        Vector3 p = Vector3.zero;
        if (Camera.main != null) p = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_damageClipCached, p, damageSoundVolume);
    }

    void PlayDeathSound()
    {
        if (_deathClipCached == null)
            _deathClipCached = Resources.Load<AudioClip>("Sounds/sound_death");
        if (_deathClipCached == null) return;
        Vector3 p = Vector3.zero;
        if (Camera.main != null) p = Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(_deathClipCached, p, deathSoundVolume);
    }

    void Awake()
    {
        if (maxHp < 1) maxHp = 1;
        currentHp = maxHp;
        animator = GetComponent<Animator>();
        invulnerableUntil = Time.time + spawnInvulnerability;
    }

    public bool IsDead => isDead;
    public int CurrentHp => Mathf.Max(0, currentHp);
    public int MaxHp => maxHp;

    /// <summary>Добавляет HP, не выше maxHp. Не действует на мёртвого.</summary>
    public void RestoreHp(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    /// <returns>true если HP реально изменилось (урон принят)</returns>
    /// <param name="playDamageSound">false — без sound_damage (например при пробитии щита, где уже звук крита).</param>
    public bool TakeDamage(int damage, bool playDamageSound = true)
    {
        if (isDead || damage <= 0) return false;
        if (Time.time < invulnerableUntil) return false;

        currentHp -= damage;
        if (playDamageSound)
            PlayDamageSound();
        SpawnDamagePopup(damage);

        if (currentHp > 0)
        {
            if (animator != null) animator.SetTrigger("Hurt");
            return true;
        }

        isDead = true;
        PlayDeathSound();
        if (animator != null)
        {
            ClearNonDeathTriggers(animator);
            animator.SetTrigger("Death");
        }
        Died?.Invoke(gameObject);
        EnterDeathStasis();
        return true;
    }

    void SpawnDamagePopup(int damage)
    {
        Vector3 p = transform.position;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            p = col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.15f);
        else
            p += Vector3.up * 0.65f;
        DamagePopup.Show(p, damage);
    }

    static void ClearNonDeathTriggers(Animator anim)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Trigger) continue;
            if (p.name == "Death") continue;
            anim.ResetTrigger(p.name);
        }
    }

    IEnumerator FreezeAnimatorAfterDeath(Animator anim)
    {
        yield return null;
        float elapsed = 0f;
        const float timeout = 10f;
        while (anim != null && elapsed < timeout)
        {
            AnimatorStateInfo si = anim.GetCurrentAnimatorStateInfo(0);
            if (si.IsName("Death") && si.normalizedTime >= 0.95f)
            {
                anim.speed = 0f;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (anim != null) anim.speed = 0f;
    }

    void EnterDeathStasis()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (animator != null)
        {
            ClearNonDeathTriggers(animator);
            StartCoroutine(FreezeAnimatorAfterDeath(animator));
        }

        foreach (Component c in GetComponentsInChildren<Component>(true))
        {
            if (c == null || c == this) continue;
            if (c is Animator) continue;
            if (c is Collider2D) continue;
            if (c is Rigidbody2D) continue;
            if (c is Joint2D) continue;
            if (c is PlayerHealthBarHud)
                continue;
            if (c is Behaviour be)
                be.enabled = false;
        }
    }
}
