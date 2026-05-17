using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class WizardDemoController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] bool useADKeys = true;

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int AttackStateHash = Animator.StringToHash("Attack");
    static readonly int DeathStateHash = Animator.StringToHash("Death");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int RunStateHash = Animator.StringToHash("Run");
    static readonly int BaseLayer = 0;

    Rigidbody2D _rb;
    Animator _animator;
    SpriteRenderer _sprite;
    float _facingX = 1f;
    bool _deathHold;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _sprite = GetComponent<SpriteRenderer>();
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (AttackPressedThisFrame() && !IsPlayingAction())
            StartAttack();

        if (DeathPressedThisFrame() && !IsPlayingAction())
            StartDeath();
    }

    void FixedUpdate()
    {
        if (IsAttacking() || IsDeathActive())
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        float x = ReadHorizontalInput();
        bool moving = Mathf.Abs(x) > 0.01f;
        _animator.SetBool(IsMovingHash, moving);

        var vel = _rb.linearVelocity;
        vel.x = moving ? x * moveSpeed : 0f;
        _rb.linearVelocity = vel;

        if (moving)
        {
            _facingX = x > 0f ? 1f : -1f;
            ApplyFacing();
        }
    }

    void LateUpdate()
    {
        UpdateDeathHold();
    }

    void StartAttack()
    {
        _animator.SetBool(IsMovingHash, false);
        _animator.Play(AttackStateHash, BaseLayer, 0f);
    }

    void StartDeath()
    {
        _deathHold = false;
        _animator.SetBool(IsMovingHash, false);
        _animator.Play(DeathStateHash, BaseLayer, 0f);
    }

    bool IsPlayingAction() => IsAttacking() || IsDeathActive();

    bool IsDeathActive() => _deathHold || IsInDeathState();

    bool IsInDeathState()
    {
        if (_animator.IsInTransition(BaseLayer))
        {
            var next = _animator.GetNextAnimatorStateInfo(BaseLayer);
            if (next.shortNameHash == DeathStateHash)
                return true;
        }

        return _animator.GetCurrentAnimatorStateInfo(BaseLayer).shortNameHash == DeathStateHash;
    }

    bool IsAttacking() => IsInActionState(AttackStateHash, FinishAttack);

    bool IsInActionState(int stateHash, System.Action onFinished)
    {
        if (_animator.IsInTransition(BaseLayer))
        {
            var next = _animator.GetNextAnimatorStateInfo(BaseLayer);
            if (next.shortNameHash == stateHash)
                return true;

            var current = _animator.GetCurrentAnimatorStateInfo(BaseLayer);
            if (current.shortNameHash == stateHash)
                return false;
        }

        var state = _animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (state.shortNameHash != stateHash)
            return false;

        if (state.normalizedTime >= 0.99f)
        {
            onFinished();
            return false;
        }

        return true;
    }

    void UpdateDeathHold()
    {
        if (IsInDeathState() && !_deathHold)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(BaseLayer);
            if (state.shortNameHash == DeathStateHash && state.normalizedTime >= 0.99f)
                _deathHold = true;
        }

        if (!_deathHold)
            return;

        _animator.Play(DeathStateHash, BaseLayer, 0.999f);

        if (Mathf.Abs(ReadHorizontalInput()) > 0.01f)
            FinishDeath();
    }

    void FinishAttack() => FinishAction();

    void FinishDeath()
    {
        _deathHold = false;
        FinishAction();
    }

    void FinishAction()
    {
        float x = ReadHorizontalInput();
        bool moving = Mathf.Abs(x) > 0.01f;
        _animator.SetBool(IsMovingHash, moving);
        _animator.Play(moving ? RunStateHash : IdleStateHash, BaseLayer, 0f);
    }

    void ApplyFacing()
    {
        if (_sprite != null)
            _sprite.flipX = _facingX < 0f;
    }

    bool AttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
#endif
        return Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1");
    }

    bool DeathPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Q);
    }

    float ReadHorizontalInput()
    {
        if (useADKeys)
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            return x;
        }

        return Input.GetAxisRaw("Horizontal");
    }
}
