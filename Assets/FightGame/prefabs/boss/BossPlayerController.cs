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
    [SerializeField] bool useADKeys = true;
    [SerializeField] bool flipByNegativeScaleX = true;
    [SerializeField] float groundSnapRayDistance = 3f;
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] bool stabilizeRunSprites = true;

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");
    static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    static readonly int DeathSpeedHash = Animator.StringToHash("DeathSpeed");
    static readonly int AttackStateHash = Animator.StringToHash("Attack");
    static readonly int DeathStateHash = Animator.StringToHash("Death");
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
    float _anchorCenterX;
    float _anchorFeetY;

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
        AlignColliderToSprite();
        SnapFeetToGround();
        CacheAnchorFromSprite(_animSprite.sprite);
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
    }

    void FixedUpdate()
    {
        if (IsPlayingAction())
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        _inputX = ReadHorizontalInput();
        bool moving = Mathf.Abs(_inputX) > 0.01f;
        _animator.SetBool(IsMovingHash, moving);

        float vx = _inputX * moveSpeed;
        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
    }

    void Update()
    {
        if (AttackPressedThisFrame() && !IsPlayingAction())
            StartAttack();

        if (DeathPressedThisFrame() && !IsPlayingAction())
            StartDeath();

        ApplyFacing(_inputX);
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

    bool IsAttacking() => IsInActionState(AttackStateHash, FinishAttack);

    void LateUpdate()
    {
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
