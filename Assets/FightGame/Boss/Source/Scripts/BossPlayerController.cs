using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossPlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float runAnimSpeed = 2f;
    [SerializeField] float attackAnimSpeed = 1.5f;
    [SerializeField] float deathAnimSpeed = 1.5f;
    [SerializeField] float jumpForce = 13f;
    [SerializeField] float jumpAnimSpeed = 1f;
    [SerializeField, Range(1, 6)] int jumpImpulseFrame = 4;
    [SerializeField] float jumpClipStopTime = 0.7f;
    [SerializeField] float jumpFrameStep = 0.1f;
    [SerializeField] Sprite[] jumpSprites = new Sprite[6];
    [SerializeField] float groundCheckDistance = 0.12f;
    [SerializeField] bool useADKeys = true;
    [SerializeField] bool flipByNegativeScaleX = true;
    [SerializeField] float groundSnapRayDistance = 3f;
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] bool stabilizeRunSprites = true;

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");
    static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    static readonly int DeathSpeedHash = Animator.StringToHash("DeathSpeed");
    static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");
    static readonly int AttackStateHash = Animator.StringToHash("Attack");
    static readonly int DeathStateHash = Animator.StringToHash("Death");
    static readonly int JumpStateHash = Animator.StringToHash("Jump");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int RunStateHash = Animator.StringToHash("Run");
    static readonly int BaseLayer = 0;

    Rigidbody2D _rb;
    Animator _animator;
    SpriteRenderer _animSprite;
    SpriteRenderer _sprite;
    Transform _visual;
    BoxCollider2D _collider;
    float _inputX;
    bool _wasMoving;
    bool _runAnchorReady;
    bool _deathAnchorReady;
    bool _deathHold;
    bool _jumpAnchorReady;
    bool _jumpActive;
    bool _jumpImpulseApplied;
    bool _animatorWasEnabled = true;
    float _jumpTimer;
    int _jumpFrameIndex = -1;
    float _anchorCenterX;
    float _anchorFeetY;
    Vector2 _normalSpriteSize;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _animSprite = GetComponent<SpriteRenderer>();
        _collider = GetComponent<BoxCollider2D>();
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        SetupVisualRenderer();
        ApplyAnimSpeeds();
    }

    void Start()
    {
        CacheNormalSpriteSize();
        AlignColliderToSprite();
        SnapFeetToGround();
        CacheAnchorFromSprite(_animSprite.sprite);
    }

    void CacheNormalSpriteSize()
    {
        Sprite sprite = _animSprite != null ? _animSprite.sprite : null;
        if (sprite == null)
            return;

        _normalSpriteSize = sprite.bounds.size;
    }

    void OnValidate()
    {
        ApplyAnimSpeeds();
    }

    void ApplyAnimSpeeds()
    {
        if (_animator == null)
            return;

        _animator.SetFloat(RunSpeedHash, Mathf.Max(0.1f, runAnimSpeed));
        _animator.SetFloat(AttackSpeedHash, Mathf.Max(0.1f, attackAnimSpeed));
        _animator.SetFloat(DeathSpeedHash, Mathf.Max(0.1f, deathAnimSpeed));
        _animator.SetFloat(JumpSpeedHash, Mathf.Max(0.1f, jumpAnimSpeed));
    }

    void FixedUpdate()
    {
        if (IsAttacking() || IsDeathActive())
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        _inputX = ReadHorizontalInput();
        bool moving = Mathf.Abs(_inputX) > 0.01f;
        if (!IsJumping())
            _animator.SetBool(IsMovingHash, moving);

        float vx = _inputX * moveSpeed;
        float vy = _rb.linearVelocity.y;
        if (_jumpActive && !_jumpImpulseApplied)
            vy = 0f;

        _rb.linearVelocity = new Vector2(vx, vy);
    }

    void Update()
    {
        if (JumpPressedThisFrame() && IsGrounded() && !IsPlayingAction())
            StartJump();

        if (AttackPressedThisFrame() && !IsPlayingAction())
            StartAttack();

        if (DeathPressedThisFrame() && !IsPlayingAction())
            StartDeath();

        ApplyFacing(ReadHorizontalInput());
    }

    void StartJump()
    {
        _jumpActive = true;
        _jumpImpulseApplied = false;
        _jumpTimer = 0f;
        _jumpFrameIndex = -1;
        _jumpAnchorReady = false;
        _animator.SetBool(IsMovingHash, false);
        _animatorWasEnabled = _animator.enabled;
        _animator.enabled = false;
        ApplyJumpFrame(0);
        CacheAnchorFromSprite(_sprite != null ? _sprite.sprite : jumpSprites[0]);
        _jumpAnchorReady = true;
    }

    void ApplyJumpFrame(int frameIndex)
    {
        if (jumpSprites == null || jumpSprites.Length == 0)
            return;

        int i = Mathf.Clamp(frameIndex, 0, jumpSprites.Length - 1);
        Sprite sprite = jumpSprites[i];
        if (sprite == null)
            return;

        _jumpFrameIndex = i;
        if (_animSprite != null)
        {
            _animSprite.sprite = sprite;
            if (_sprite != null)
                _sprite.flipX = _animSprite.flipX;
        }

        if (_sprite != null)
            _sprite.sprite = sprite;

        ApplyJumpVisualScale(sprite);
    }

    void ApplyJumpVisualScale(Sprite sprite)
    {
        if (_visual == null || sprite == null || _normalSpriteSize.y <= 0f)
            return;

        float h = sprite.bounds.size.y;
        if (h <= 0f)
            return;

        float scale = _normalSpriteSize.y / h;
        _visual.localScale = new Vector3(scale, scale, 1f);
    }

    void ApplyJumpImpulse()
    {
        if (_jumpImpulseApplied)
            return;

        _jumpImpulseApplied = true;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
    }

    bool JumpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Space);
    }

    bool IsGrounded()
    {
        var renderer = _sprite != null ? _sprite : _animSprite;
        if (renderer == null || renderer.sprite == null)
            return false;

        Bounds b = renderer.bounds;
        var origin = new Vector2(b.center.x, b.min.y + 0.02f);
        return Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayers);
    }

    void StartAttack()
    {
        _animator.SetBool(IsMovingHash, false);
        _animator.Play(AttackStateHash, BaseLayer, 0f);
    }

    void StartDeath()
    {
        _deathHold = false;
        _deathAnchorReady = false;
        _animator.SetBool(IsMovingHash, false);
        CacheAnchorFromSprite(_animSprite.sprite);
        _deathAnchorReady = true;
        _animator.Play(DeathStateHash, BaseLayer, 0f);
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

    bool IsPlayingAction() => IsAttacking() || IsDeathActive() || IsJumping();

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

        if (stateHash == AttackStateHash)
        {
            if (state.normalizedTime >= 0.999f)
            {
                onFinished();
                return false;
            }

            return true;
        }

        if (state.normalizedTime >= 0.99f)
        {
            onFinished();
            return false;
        }

        return true;
    }

    bool IsAttacking() => IsInActionState(AttackStateHash, FinishAttack);

    bool IsJumping() => _jumpActive;

    void LateUpdate()
    {
        if (_jumpActive)
        {
            UpdateJumpPlayback();
            UpdateDeathHold();

            if (stabilizeRunSprites)
            {
                if (!_jumpAnchorReady)
                {
                    CacheAnchorFromSprite(_animSprite != null ? _animSprite.sprite : _sprite.sprite);
                    _jumpAnchorReady = true;
                }

                StabilizeVisual();
            }

            return;
        }

        SyncVisibleSprite();
        UpdateDeathHold();

        bool moving = _animator.GetBool(IsMovingHash);
        if (moving != _wasMoving)
        {
            _wasMoving = moving;
            _runAnchorReady = false;
            if (!moving && !IsDeathActive())
                CacheAnchorFromSprite(_animSprite.sprite);
        }

        if (stabilizeRunSprites && IsDeathActive())
        {
            if (!_deathAnchorReady)
            {
                CacheAnchorFromSprite(_animSprite.sprite);
                _deathAnchorReady = true;
            }

            StabilizeVisual();
        }
        else if (stabilizeRunSprites && moving && !IsAttacking())
        {
            if (!_runAnchorReady)
            {
                CacheAnchorFromSprite(_animSprite.sprite);
                _runAnchorReady = true;
            }

            StabilizeVisual();
        }
        else if (_visual != null)
        {
            _visual.localPosition = Vector3.zero;
        }
    }

    void UpdateJumpPlayback()
    {
        if (!_jumpActive)
            return;

        _jumpTimer += Time.deltaTime;
        float step = Mathf.Max(0.01f, jumpFrameStep);
        int frame = Mathf.Clamp(Mathf.FloorToInt(_jumpTimer / step), 0, 5);

        if (frame != _jumpFrameIndex)
        {
            ApplyJumpFrame(frame);
            if (stabilizeRunSprites)
                StabilizeVisual();
        }

        int impulseFrame = Mathf.Clamp(jumpImpulseFrame, 1, 6) - 1;
        if (!_jumpImpulseApplied && frame >= impulseFrame)
            ApplyJumpImpulse();

        if (_jumpTimer >= jumpClipStopTime)
            FinishJump();
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

    void SetupVisualRenderer()
    {
        if (_animSprite == null)
            return;

        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(transform, false);
        _visual = visualGo.transform;
        _sprite = visualGo.AddComponent<SpriteRenderer>();

        _sprite.color = _animSprite.color;
        _sprite.sortingLayerID = _animSprite.sortingLayerID;
        _sprite.sortingOrder = _animSprite.sortingOrder;
        _sprite.material = _animSprite.sharedMaterial;
        _sprite.drawMode = _animSprite.drawMode;
        _sprite.maskInteraction = _animSprite.maskInteraction;
        _sprite.spriteSortPoint = _animSprite.spriteSortPoint;

        _animSprite.enabled = false;
    }

    void SyncVisibleSprite()
    {
        if (_sprite == null || _animSprite == null)
            return;

        _sprite.sprite = _animSprite.sprite;
        _sprite.flipX = _animSprite.flipX;
        _sprite.flipY = _animSprite.flipY;
    }

    void CacheAnchorFromSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        Bounds b = sprite.bounds;
        _anchorCenterX = b.center.x;
        _anchorFeetY = b.min.y;
    }

    void StabilizeVisual()
    {
        if (_visual == null || _sprite.sprite == null)
            return;

        Bounds b = _sprite.sprite.bounds;
        float dx = _anchorCenterX - b.center.x;
        float dy = _anchorFeetY - b.min.y;
        _visual.localPosition = new Vector3(dx, dy, 0f);
    }

    void FinishAttack() => FinishAction();

    void FinishJump()
    {
        _jumpActive = false;
        _jumpImpulseApplied = false;
        _jumpAnchorReady = false;
        _jumpTimer = 0f;
        _jumpFrameIndex = -1;
        _animator.enabled = _animatorWasEnabled;
        if (_visual != null)
        {
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }

        FinishAction();
    }

    void FinishDeath()
    {
        _deathHold = false;
        _deathAnchorReady = false;
        FinishAction();
    }

    void FinishAction()
    {
        float x = ReadHorizontalInput();
        bool moving = Mathf.Abs(x) > 0.01f;
        _animator.SetBool(IsMovingHash, moving);

        if (moving)
            _animator.Play(RunStateHash, BaseLayer, 0f);
        else
            _animator.Play(IdleStateHash, BaseLayer, 0f);
    }

    float ReadHorizontalInput()
    {
        if (useADKeys)
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.A))
                x -= 1f;
            if (Input.GetKey(KeyCode.D))
                x += 1f;
            return x;
        }

        return Input.GetAxisRaw("Horizontal");
    }

    void ApplyFacing(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f)
            return;

        if (_animSprite != null)
        {
            _animSprite.flipX = dirX < 0f;
            return;
        }

        if (!flipByNegativeScaleX)
        {
            Vector3 s = transform.localScale;
            float mag = Mathf.Abs(s.x);
            if (mag < 1e-3f)
                mag = 1f;
            transform.localScale = new Vector3(dirX > 0f ? mag : -mag, s.y, s.z);
            return;
        }

        Vector3 scale = transform.localScale;
        float magX = Mathf.Abs(scale.x);
        if (magX < 1e-3f)
            magX = 1f;
        float magY = Mathf.Abs(scale.y);
        float magZ = Mathf.Abs(scale.z);
        if (magY < 1e-3f) magY = 1f;
        if (magZ < 1e-3f) magZ = 1f;
        transform.localScale = new Vector3(dirX > 0f ? -magX : magX, magY, magZ);
    }

    void AlignColliderToSprite()
    {
        Sprite sprite = _animSprite != null ? _animSprite.sprite : null;
        if (_collider == null || sprite == null)
            return;

        Bounds b = sprite.bounds;
        _collider.offset = new Vector2(b.center.x, b.extents.y);
        _collider.size = new Vector2(b.size.x * 0.45f, b.size.y * 0.95f);
    }

    void SnapFeetToGround()
    {
        if (_sprite == null || _sprite.sprite == null)
            return;

        Bounds b = _sprite.bounds;
        var origin = new Vector2(b.center.x, b.min.y + 0.05f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundSnapRayDistance, groundLayers);
        if (!hit.collider)
            return;

        float deltaY = hit.point.y - b.min.y;
        if (Mathf.Abs(deltaY) < 0.0001f)
            return;

        var pos = transform.position;
        pos.y += deltaY;
        transform.position = pos;
        _rb.position = new Vector2(_rb.position.x, pos.y);
    }
}
