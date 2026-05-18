using System.Collections;
using UnityEngine;

/// <summary>
/// Hurt через Animator. После N ударов — падение (смена спрайтов death 1–3).
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class ScarecrowHitReaction : MonoBehaviour
{
    const float HurtClipLength = 0.4f;
    static readonly int HurtStateHash = Animator.StringToHash("Hurt");

    [Tooltip("Сколько секунд показывать 2-й кадр (поза урона).")]
    [Min(0.05f)]
    [SerializeField] float hurtHoldSeconds = 0.28f;

    [Tooltip("Сколько ударов до падения.")]
    [Min(1)]
    [SerializeField] int hitsToDefeat = 3;

    [Header("Падение")]
    [SerializeField] Sprite[] deathSprites;
    [Min(0.05f)]
    [SerializeField] float deathFrameSeconds = 0.3f;
    [Tooltip("Насколько увеличить чучело при падении (1 = без изменений).")]
    [Min(1f)]
    [SerializeField] float deathScaleMultiplier = 1.08f;

    [Header("Звук")]
    [SerializeField] AudioClip hitClip;
    [Min(0f)]
    [SerializeField] float hitSoundVolume = 3f;
    [Range(0.5f, 3f)]
    [SerializeField] float hitSoundPitch = 1.4f;

    Animator _animator;
    SpriteRenderer _spriteRenderer;
    AudioSource _audioSource;
    Coroutine _hitSoundRoutine;
    Coroutine _deathRoutine;
    bool _wasInHurt;
    bool _defeated;
    int _hitCount;
    Vector3 _baseScale;

    public bool RegisterHit()
    {
        if (_defeated) return true;

        _hitCount++;
        if (_hitCount < hitsToDefeat) return false;

        TriggerDefeat();
        return true;
    }

    public bool IsDefeated => _defeated;

    public void PlayHitSound()
    {
        if (hitClip == null || _audioSource == null) return;

        if (_hitSoundRoutine != null)
            StopCoroutine(_hitSoundRoutine);

        _hitSoundRoutine = StartCoroutine(PlayHitSoundRoutine());
    }

    void TriggerDefeat()
    {
        _defeated = true;

        if (_animator != null)
        {
            _animator.ResetTrigger("Hurt");
            _animator.enabled = false;
        }

        transform.localScale = _baseScale * deathScaleMultiplier;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(PlayDeathSprites());
    }

    IEnumerator PlayDeathSprites()
    {
        if (_spriteRenderer == null || deathSprites == null || deathSprites.Length == 0)
            yield break;

        int last = deathSprites.Length - 1;
        for (int i = 0; i < deathSprites.Length; i++)
        {
            if (deathSprites[i] != null)
                _spriteRenderer.sprite = deathSprites[i];

            if (i < last)
                yield return new WaitForSeconds(deathFrameSeconds);
        }
    }

    IEnumerator PlayHitSoundRoutine()
    {
        _audioSource.pitch = hitSoundPitch;
        _audioSource.PlayOneShot(hitClip, Mathf.Max(0.01f, hitSoundVolume));

        float wait = hitClip.length / Mathf.Max(0.01f, hitSoundPitch);
        yield return new WaitForSeconds(wait);

        _audioSource.pitch = 1f;
        _hitSoundRoutine = null;
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;

        if (hitClip == null)
            hitClip = Resources.Load<AudioClip>("Sounds/sound_hay");

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
    }

    void Update()
    {
        if (_defeated || _animator == null || !_animator.isActiveAndEnabled) return;
        if (_animator.runtimeAnimatorController == null || _animator.layerCount < 1) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        bool inHurt = state.shortNameHash == HurtStateHash || state.IsName("Hurt");

        if (inHurt)
        {
            float hold = Mathf.Max(0.05f, hurtHoldSeconds);
            _animator.speed = HurtClipLength / hold;
            _wasInHurt = true;
            return;
        }

        if (_wasInHurt)
        {
            _animator.speed = 1f;
            _wasInHurt = false;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        hurtHoldSeconds = Mathf.Max(0.05f, hurtHoldSeconds);
        hitsToDefeat = Mathf.Max(1, hitsToDefeat);
        deathFrameSeconds = Mathf.Max(0.05f, deathFrameSeconds);
        deathScaleMultiplier = Mathf.Max(1f, deathScaleMultiplier);
    }
#endif
}
