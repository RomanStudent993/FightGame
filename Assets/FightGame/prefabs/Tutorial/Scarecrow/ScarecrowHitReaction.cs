using UnityEngine;

/// <summary>
/// Длительность 2-го кадра урона на чучеле. SimpleHealth по-прежнему вызывает триггер Hurt.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ScarecrowHitReaction : MonoBehaviour
{
    const float HurtClipLength = 0.4f;
    static readonly int HurtStateHash = Animator.StringToHash("Hurt");

    [Tooltip("Сколько секунд показывать 2-й кадр (поза урона). Меньше — быстрее.")]
    [Min(0.05f)]
    [SerializeField] float hurtHoldSeconds = 0.28f;

    [Header("Звук")]
    [Tooltip("Громкость удара. Значения > 1 усиливают звук (PlayOneShot).")]
    [Min(0f)]
    [SerializeField] float hitSoundVolume = 2.5f;

    AudioClip _hitClip;

    Animator _animator;
    AudioSource _audioSource;
    bool _wasInHurt;

    public void PlayHitSound()
    {
        if (_hitClip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(_hitClip, hitSoundVolume);
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _hitClip = Resources.Load<AudioClip>("Sounds/sound_hay");

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }

    void Update()
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;
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
    }
#endif
}
