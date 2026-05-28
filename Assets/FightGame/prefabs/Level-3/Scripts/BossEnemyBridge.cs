using UnityEngine;

/// <summary>
/// Босс: IsMoving + flipX, скорости клипов, возврат из Attack (контроллер без кода игрока).
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossEnemyBridge : MonoBehaviour
{
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");
    static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    static readonly int DeathSpeedHash = Animator.StringToHash("DeathSpeed");
    static readonly int AttackStateHash = Animator.StringToHash("Attack");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int DeathTriggerHash = Animator.StringToHash("Death");

    [SerializeField] float moveThreshold = 0.12f;
    [SerializeField] float runAnimSpeed = 0.48f;
    [SerializeField] float attackAnimSpeed = 1.5f;
    [SerializeField] float deathAnimSpeed = 1.5f;
    [SerializeField] float deathGroundProbeDistance = 2f;
    [SerializeField] float deathGroundOffsetY = 0.005f;
    [Tooltip("Доп. подъём спрайта во время анимации смерти (локальные единицы).")]
    [SerializeField] float deathVisualLiftY = 0.14f;
    [Tooltip("Включи, если босс смотрит не в ту сторону при flipX = dirX < 0.")]
    [SerializeField] bool invertFlipX;

    EnemyAI _enemyAi;
    Rigidbody2D _rb;
    Collider2D _bodyCollider;
    SpriteRenderer _sprite;
    SpriteRenderer _visibleSprite;
    Animator _animator;
    SimpleHealth _health;
    Transform _player;
    Transform _visual;
    bool _hasIsMovingParam;
    bool _hasRunSpeedParam;
    bool _hasAttackSpeedParam;
    bool _hasDeathSpeedParam;
    bool _deathYLocked;
    bool _deathStateForced;
    bool _deathVisualReady;
    float _lockedDeathY;
    float _baseScaleX = 1.5f;
    float _anchorCenterX;
    float _anchorFeetY;

    void Awake()
    {
        _enemyAi = GetComponent<EnemyAI>();
        _rb = GetComponent<Rigidbody2D>();
        _bodyCollider = GetComponent<Collider2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _health = GetComponent<SimpleHealth>();

        if (_enemyAi != null)
            _enemyAi.flipByScale = false;

        _baseScaleX = Mathf.Abs(transform.localScale.x);
        if (_baseScaleX < 0.01f)
            _baseScaleX = 1.5f;

        ApplyPositiveScale();

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.nameHash == IsMovingHash)
                    _hasIsMovingParam = true;
                else if (p.nameHash == RunSpeedHash)
                    _hasRunSpeedParam = true;
                else if (p.nameHash == AttackSpeedHash)
                    _hasAttackSpeedParam = true;
                else if (p.nameHash == DeathSpeedHash)
                    _hasDeathSpeedParam = true;
            }
        }

        ApplyAnimSpeeds();
    }

    void Start()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
            _player = playerGo.transform;

        CacheDeathAnchorFromSprite(_sprite != null ? _sprite.sprite : null);
    }

    void LateUpdate()
    {
        if (_sprite == null) return;

        if (StabilizeDeadBoss())
            return;

        ApplyPositiveScale();
        RecoverFromFinishedAttack();

        float vx = _rb != null ? _rb.linearVelocity.x : 0f;
        bool isMoving = Mathf.Abs(vx) > moveThreshold;
        if (_hasIsMovingParam && _animator != null)
            _animator.SetBool(IsMovingHash, isMoving);

        // Fallback for controllers where RunSpeed is not wired in transitions/states:
        // slow down only movement animation while keeping gameplay speed unchanged.
        if (_animator != null && !_hasRunSpeedParam)
            _animator.speed = isMoving ? Mathf.Max(0.1f, runAnimSpeed) : 1f;

        float dirX = 0f;
        if (_player != null)
        {
            float dx = _player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.02f)
                dirX = Mathf.Sign(dx);
        }

        if (Mathf.Abs(dirX) < 0.01f && Mathf.Abs(vx) > 0.02f)
            dirX = Mathf.Sign(vx);

        if (Mathf.Abs(dirX) < 0.01f) return;

        bool faceLeft = dirX < 0f;
        if (invertFlipX)
            faceLeft = !faceLeft;

        _sprite.flipX = faceLeft;
    }

    bool StabilizeDeadBoss()
    {
        if (_health == null || !_health.IsDead || _rb == null)
            return false;

        if (_animator != null)
        {
            _animator.speed = 1f;
            if (_hasIsMovingParam)
                _animator.SetBool(IsMovingHash, false);

            if (!_deathStateForced)
            {
                _animator.ResetTrigger("Attack");
                _animator.SetTrigger(DeathTriggerHash);
                _deathStateForced = true;
            }
        }

        if (!_deathYLocked)
        {
            _lockedDeathY = ComputeDeathLockY();
            _deathYLocked = true;
            _rb.position = new Vector2(_rb.position.x, _lockedDeathY);
        }

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;

        Vector2 p = _rb.position;
        if (Mathf.Abs(p.y - _lockedDeathY) > 0.0001f)
            _rb.position = new Vector2(p.x, _lockedDeathY);

        EnsureDeathVisual();
        SyncDeathVisual();
        StabilizeDeathVisual();

        return true;
    }

    void CacheDeathAnchorFromSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        Bounds b = sprite.bounds;
        _anchorCenterX = b.center.x;
        _anchorFeetY = b.min.y;
    }

    void EnsureDeathVisual()
    {
        if (_deathVisualReady || _sprite == null)
            return;

        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(transform, false);
        _visual = visualGo.transform;
        _visibleSprite = visualGo.AddComponent<SpriteRenderer>();

        _visibleSprite.color = _sprite.color;
        _visibleSprite.sortingLayerID = _sprite.sortingLayerID;
        _visibleSprite.sortingOrder = _sprite.sortingOrder;
        _visibleSprite.material = _sprite.sharedMaterial;
        _visibleSprite.drawMode = _sprite.drawMode;
        _visibleSprite.maskInteraction = _sprite.maskInteraction;
        _visibleSprite.spriteSortPoint = _sprite.spriteSortPoint;

        _sprite.enabled = false;
        _deathVisualReady = true;
    }

    void SyncDeathVisual()
    {
        if (_visibleSprite == null || _sprite == null)
            return;

        _visibleSprite.sprite = _sprite.sprite;
        _visibleSprite.flipX = _sprite.flipX;
        _visibleSprite.flipY = _sprite.flipY;
    }

    void StabilizeDeathVisual()
    {
        if (_visual == null || _visibleSprite == null || _visibleSprite.sprite == null)
            return;

        Bounds b = _visibleSprite.sprite.bounds;
        float dx = _anchorCenterX - b.center.x;
        float dy = _anchorFeetY - b.min.y + deathVisualLiftY;
        _visual.localPosition = new Vector3(dx, dy, 0f);
    }

    float ComputeDeathLockY()
    {
        if (_bodyCollider == null)
            return transform.position.y;

        Bounds b = _bodyCollider.bounds;
        float probeDistance = Mathf.Max(0.1f, deathGroundProbeDistance);
        Vector2 origin = new Vector2(b.center.x, b.min.y + 0.05f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, probeDistance, ~0);

        if (hit.collider == null)
            return transform.position.y;

        float desiredMinY = hit.point.y + deathGroundOffsetY;
        float deltaY = desiredMinY - b.min.y;
        return _rb.position.y + deltaY;
    }

    void ApplyAnimSpeeds()
    {
        if (_animator == null) return;
        if (_hasRunSpeedParam)
            _animator.SetFloat(RunSpeedHash, Mathf.Max(0.1f, runAnimSpeed));
        if (_hasAttackSpeedParam)
            _animator.SetFloat(AttackSpeedHash, Mathf.Max(0.1f, attackAnimSpeed));
        if (_hasDeathSpeedParam)
            _animator.SetFloat(DeathSpeedHash, Mathf.Max(0.1f, deathAnimSpeed));
    }

    void RecoverFromFinishedAttack()
    {
        if (_animator == null) return;

        AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(0);
        if (st.shortNameHash != AttackStateHash || st.normalizedTime < 0.99f)
            return;

        bool moving = _rb != null && Mathf.Abs(_rb.linearVelocity.x) > moveThreshold;
        if (moving && _hasIsMovingParam)
            _animator.SetBool(IsMovingHash, true);
        else
            _animator.Play(IdleStateHash, 0, 0f);
    }

    void ApplyPositiveScale()
    {
        Vector3 s = transform.localScale;
        float magY = Mathf.Abs(s.y);
        float magZ = Mathf.Abs(s.z);
        if (s.x < 0f || Mathf.Abs(s.x - _baseScaleX) > 0.001f)
            transform.localScale = new Vector3(_baseScaleX, magY, magZ);
    }
}
