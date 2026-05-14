using UnityEngine;

/// <summary>
/// 2D platformer AI: Rigidbody2D, лучи, проверка достижимости, FSM без «дёрганья» у нижней грани платформы.
/// Препятствие «над головой» (низ коллайдера, нормаль вниз) не считается стеной впереди — бот идёт к краю.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyPlatformMovementAI : MonoBehaviour
{
    public enum AiState
    {
        Patrol,
        Chase,
        Jump,
        StuckRecovery
    }

    [Header("Движение (только velocity)")]
    [SerializeField] float moveSpeed = 2.35f;
    [SerializeField] float jumpForce = 11.25f;
    [Range(0f, 1f)]
    [SerializeField] float airControl = 0.32f;
    [SerializeField] float stuckRecoveryMoveMultiplier = 0.65f;

    [Header("Детекция и прыжок")]
    [SerializeField] float detectionDistance = 0.48f;
    [SerializeField] float maxJumpHeight = 2.15f;
    [SerializeField] float jumpCooldown = 0.55f;
    [Tooltip("Нормаль удара: если Y ниже порога — это низ нависающей платформы, не «стена впереди».")]
    [SerializeField] float undersideNormalYMax = -0.18f;
    [Tooltip("Высота луча «стена» от ног (ниже — только вертикальные препятствия).")]
    [SerializeField] float forwardWallRayHeight = 0.38f;
    [Tooltip("Доп. луч чуть выше для уступа; всё ещё отсекается по нормали низа.")]
    [SerializeField] float forwardLedgeRayExtraHeight = 0.85f;
    [SerializeField] float ceilingCheckDistance = 1.05f;
    [SerializeField] float landingProbeDown = 3.2f;
    [SerializeField] int arcSampleCount = 9;
    [SerializeField] float arcSampleRadius = 0.11f;

    [Header("Яма / край")]
    [SerializeField] float gapRayLength = 2.6f;
    [SerializeField] float gapProbeForward = 0.38f;
    [SerializeField] float gapMinDepth = 0.38f;

    [Header("Земля")]
    [SerializeField] LayerMask groundLayers = default;
    [SerializeField] LayerMask platformLayers = default;
    [SerializeField] LayerMask obstacleLayers = default;
    [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.42f);
    [SerializeField] float groundCheckRadius = 0.12f;

    [Header("Патруль / цель")]
    [SerializeField] int startFacing = 1;
    [SerializeField] Transform optionalChaseTarget;

    [Header("Anti-stuck")]
    [SerializeField] float stuckTimeThreshold = 0.72f;
    [SerializeField] float stuckSpeedThreshold = 0.1f;
    [SerializeField] float stuckRecoveryDuration = 0.55f;
    [SerializeField] float stuckJumpBanDuration = 1.1f;
    [SerializeField] float minTimeBetweenFlips = 0.65f;

    [Header("Отладка (кадр)")]
    [SerializeField] bool drawGizmos = true;

    Rigidbody2D _rb;
    Collider2D _body;
    AiState _state = AiState.Patrol;
    int _face = 1;
    float _nextJumpAllowedTime;
    float _jumpBannedUntil;
    float _stuckRecoveryUntil;
    float _stuckTimer;
    float _lastFlipTime = -999f;
    float _gravityY;
    bool _jumpLeftGround;

    // Снимок детекции (для логики и gizmo)
    public bool obstacleAhead { get; private set; }
    /// <summary>«Стена» над головой в смысле потолка — блокирует вертикаль прыжка.</summary>
    public bool wallAbove { get; private set; }
    public bool platformReachable { get; private set; }
    public bool edgeDetected { get; private set; }
    public bool underOverhang { get; private set; }

    Vector2 _gizmoWallRayFrom, _gizmoWallRayTo;
    Vector2 _gizmoCeilingFrom, _gizmoCeilingTo;
    Vector2 _gizmoGapFrom, _gizmoGapTo;
    Vector2 _gizmoJumpTarget;
    bool _gizmoHasJumpTarget;

    LayerMask ObstacleMask =>
        obstacleLayers.value != 0 ? obstacleLayers : platformLayers;

    LayerMask CombinedGroundMask()
    {
        int a = groundLayers.value;
        int b = platformLayers.value;
        if (a == 0 && b == 0)
            return Physics2D.DefaultRaycastLayers;
        return a | b;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _body = GetComponent<Collider2D>();
        _face = startFacing >= 0 ? 1 : -1;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.gravityScale = Mathf.Max(_rb.gravityScale, 1f);
        _gravityY = Physics2D.gravity.y * _rb.gravityScale;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool grounded = IsGrounded();
        _gravityY = Physics2D.gravity.y * _rb.gravityScale;

        RunDetection(grounded);
        UpdateStuck(grounded, dt);
        RunFsm(grounded, dt);
    }

    void RunDetection(bool grounded)
    {
        edgeDetectResetGizmos();
        edgeDetected = DetectEdge(grounded);
        underOverhang = DetectUnderOverhang();
        obstacleAhead = ComputeForwardWallObstacle();
        wallAbove = ComputeCeilingBlocked();
        platformReachable = ComputeReachableAndArcClear(grounded);
    }

    void edgeDetectResetGizmos()
    {
        _gizmoHasJumpTarget = false;
    }

    bool DetectEdge(bool grounded)
    {
        if (!grounded)
            return false;
        Vector2 foot = FootWorldPoint();
        Vector2 probe = foot + new Vector2(_face * gapProbeForward, 0.04f);
        RaycastHit2D down = Physics2D.Raycast(probe, Vector2.down, gapRayLength, CombinedGroundMask());
        _gizmoGapFrom = probe;
        _gizmoGapTo = probe + Vector2.down * gapRayLength;
        if (down.collider == null)
            return true;
        float drop = probe.y - down.point.y;
        return drop > gapMinDepth;
    }

    /// <summary>Нависание: впереди «низ» коллайдера (нормаль вниз), а не вертикальная стена у ног.</summary>
    bool DetectUnderOverhang()
    {
        Vector2 forward = new Vector2(_face, 0f);
        float dist = detectionDistance + _body.bounds.extents.x * 0.55f;
        Vector2 mid = MidBodyOrigin();

        RaycastHit2D hit = Physics2D.Raycast(mid, forward, dist, ObstacleMask);
        if (hit.collider == null)
            return false;
        if (hit.normal.y <= undersideNormalYMax)
            return true;

        Vector2 low = FootWorldPoint() + Vector2.up * 0.08f;
        RaycastHit2D lowHit = Physics2D.Raycast(low, forward, dist * 0.95f, ObstacleMask);
        if (lowHit.collider == null)
            return false;
        return lowHit.normal.y <= undersideNormalYMax;
    }

    /// <summary>Только вертикальная стена / уступ впереди, не низ платформы над головой.</summary>
    bool ComputeForwardWallObstacle()
    {
        Vector2 forward = new Vector2(_face, 0f);
        float dist = detectionDistance + _body.bounds.extents.x * 0.45f;
        Vector2 lowOrigin = FootWorldPoint() + Vector2.up * Mathf.Min(forwardWallRayHeight, _body.bounds.size.y * 0.45f);
        RaycastHit2D lowHit = Physics2D.Raycast(lowOrigin, forward, dist, ObstacleMask);
        _gizmoWallRayFrom = lowOrigin;
        _gizmoWallRayTo = lowOrigin + forward * dist;

        if (lowHit.collider != null && lowHit.normal.y > undersideNormalYMax)
            return true;

        Vector2 hi = lowOrigin + Vector2.up * Mathf.Min(forwardLedgeRayExtraHeight, maxJumpHeight * 0.65f);
        RaycastHit2D hiHit = Physics2D.Raycast(hi, forward, dist * 0.92f, ObstacleMask);
        if (hiHit.collider != null && hiHit.normal.y > undersideNormalYMax)
        {
            if (lowHit.collider == null || hiHit.distance < lowHit.distance - 0.02f)
                return true;
        }

        return false;
    }

    bool ComputeCeilingBlocked()
    {
        Vector2 head = HeadOrigin();
        RaycastHit2D hit = Physics2D.Raycast(head, Vector2.up, ceilingCheckDistance, ObstacleMask);
        _gizmoCeilingFrom = head;
        _gizmoCeilingTo = head + Vector2.up * ceilingCheckDistance;
        return hit.collider != null;
    }

    bool ComputeReachableAndArcClear(bool grounded)
    {
        if (!grounded)
            return false;
        if (wallAbove)
            return false;

        Vector2 foot = FootWorldPoint();
        float approxMaxH = ApproxMaxJumpHeight();

        Vector2 forward = new Vector2(_face, 0f);
        float dist = detectionDistance + _body.bounds.extents.x * 0.5f;
        Vector2 lowOrigin = FootWorldPoint() + Vector2.up * Mathf.Min(forwardWallRayHeight, _body.bounds.size.y * 0.45f);
        RaycastHit2D wallHit = Physics2D.Raycast(lowOrigin, forward, dist, ObstacleMask);

        if (edgeDetected)
        {
            Vector2 across = foot + forward * (gapProbeForward + detectionDistance * 1.15f);
            RaycastHit2D land = Physics2D.Raycast(across, Vector2.down, landingProbeDown, CombinedGroundMask());
            if (land.collider == null)
                return false;
            _gizmoJumpTarget = land.point;
            _gizmoHasJumpTarget = true;
            float dy = land.point.y - foot.y;
            if (dy > Mathf.Min(maxJumpHeight, approxMaxH * 1.05f) + 0.15f)
                return false;
            return ArcClearToPoint(foot, land.point);
        }

        if (!obstacleAhead || wallHit.collider == null)
            return false;

        if (wallHit.normal.y <= undersideNormalYMax)
            return false;

        float ledgeTop = wallHit.collider.bounds.max.y;
        float dyLedge = ledgeTop - foot.y;
        if (dyLedge > Mathf.Min(maxJumpHeight, approxMaxH * 1.08f) || dyLedge < 0.03f)
            return false;

        Vector2 landingProbe = wallHit.point + forward * 0.1f + Vector2.up * 0.06f;
        RaycastHit2D landHit = Physics2D.Raycast(landingProbe, Vector2.down, landingProbeDown, CombinedGroundMask());
        if (landHit.collider == null)
            return false;
        if (landHit.point.y < ledgeTop - 0.35f)
            return false;

        _gizmoJumpTarget = landHit.point;
        _gizmoHasJumpTarget = true;

        if (!ArcClearToPoint(foot, new Vector2(landHit.point.x, ledgeTop + 0.05f)))
            return false;

        return true;
    }

    float ApproxMaxJumpHeight()
    {
        float ay = _gravityY;
        if (ay >= -1e-3f)
            return maxJumpHeight;
        return (jumpForce * jumpForce) / (2f * Mathf.Abs(ay));
    }

    /// <summary>Грубая проверка свободной дуги: от ног до цели по образцу параболы с начальной vy и vx.</summary>
    bool ArcClearToPoint(Vector2 from, Vector2 toApprox)
    {
        float ay = _gravityY;
        if (ay >= -1e-3f)
            return true;

        float vx = _face * moveSpeed;
        float vy0 = jumpForce;
        float totalT = 0.55f;
        int samples = Mathf.Clamp(arcSampleCount, 4, 24);

        for (int i = 1; i <= samples; i++)
        {
            float t = (i / (float)samples) * totalT;
            Vector2 p = from + new Vector2(vx * t, vy0 * t + 0.5f * ay * t * t);
            if (p.y < from.y - 0.2f)
                break;
            Collider2D block = Physics2D.OverlapCircle(p, arcSampleRadius, ObstacleMask);
            if (block != null && block.transform.root != transform.root)
            {
                if (Vector2.Distance(p, toApprox) > 0.22f)
                    return false;
            }
        }
        return true;
    }

    void UpdateStuck(bool grounded, float dt)
    {
        if (_state == AiState.StuckRecovery || _state == AiState.Jump)
        {
            _stuckTimer = 0f;
            return;
        }

        float vx = _rb.linearVelocity.x;
        bool tryingToMove = Mathf.Abs(_face * moveSpeed) > 0.01f;
        if (grounded && tryingToMove && Mathf.Abs(vx) < stuckSpeedThreshold)
            _stuckTimer += dt;
        else
            _stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 0.35f);

        if (_stuckTimer >= stuckTimeThreshold && Time.time >= _lastFlipTime + minTimeBetweenFlips)
        {
            EnterStuckRecovery();
        }
    }

    void EnterStuckRecovery()
    {
        _state = AiState.StuckRecovery;
        _stuckTimer = 0f;
        _face = -_face;
        _lastFlipTime = Time.time;
        _jumpBannedUntil = Time.time + stuckJumpBanDuration;
        _stuckRecoveryUntil = Time.time + stuckRecoveryDuration;
    }

    void RunFsm(bool grounded, float dt)
    {
        if (_state == AiState.StuckRecovery)
        {
            ApplyMove(grounded, stuckRecoveryMoveMultiplier);
            if (Time.time >= _stuckRecoveryUntil)
                _state = optionalChaseTarget != null ? AiState.Chase : AiState.Patrol;
            return;
        }

        if (optionalChaseTarget != null && !underOverhang)
        {
            if (_state != AiState.Jump)
                _state = AiState.Chase;
            float dx = optionalChaseTarget.position.x - _rb.position.x;
            bool chaseBlocked = obstacleAhead && !platformReachable;
            if (Mathf.Abs(dx) > 0.12f && !chaseBlocked)
                _face = dx > 0f ? 1 : -1;
        }
        else if (_state != AiState.Jump)
        {
            _state = AiState.Patrol;
        }

        if (_state == AiState.Jump)
        {
            if (!grounded)
                _jumpLeftGround = true;
            if (grounded && _jumpLeftGround && _rb.linearVelocity.y <= 0.12f)
            {
                _jumpLeftGround = false;
                _state = optionalChaseTarget != null ? AiState.Chase : AiState.Patrol;
            }
            ApplyMove(grounded, 1f);
            return;
        }

        bool wantJump = grounded &&
                        Time.time >= _jumpBannedUntil &&
                        Time.time >= _nextJumpAllowedTime &&
                        obstacleAhead &&
                        platformReachable &&
                        !wallAbove &&
                        !underOverhang;

        if (edgeDetected && !underOverhang)
        {
            bool gapJump = grounded &&
                           Time.time >= _jumpBannedUntil &&
                           Time.time >= _nextJumpAllowedTime &&
                           platformReachable &&
                           !wallAbove;
            if (gapJump && TryJumpImpulse())
                return;
        }

        if (wantJump && TryJumpImpulse())
            return;

        if (underOverhang)
        {
            ApplyMove(grounded, 1f);
            return;
        }

        if (obstacleAhead && !platformReachable && grounded && Time.time >= _lastFlipTime + minTimeBetweenFlips)
        {
            _face = -_face;
            _lastFlipTime = Time.time;
        }

        ApplyMove(grounded, 1f);
    }

    bool TryJumpImpulse()
    {
        if (Time.time < _nextJumpAllowedTime || Time.time < _jumpBannedUntil)
            return false;
        if (!IsGrounded())
            return false;

        _rb.linearVelocity = new Vector2(_face * moveSpeed, jumpForce);
        _nextJumpAllowedTime = Time.time + jumpCooldown;
        _state = AiState.Jump;
        _jumpLeftGround = false;
        _stuckTimer = 0f;
        return true;
    }

    void ApplyMove(bool grounded, float speedMul)
    {
        float vxTarget = _face * moveSpeed * speedMul;
        if (!grounded)
            vxTarget *= airControl;
        float vy = _rb.linearVelocity.y;
        _rb.linearVelocity = new Vector2(vxTarget, vy);
    }

    bool IsGrounded()
    {
        Vector2 p = _rb.position + groundCheckOffset;
        return Physics2D.OverlapCircle(p, groundCheckRadius, CombinedGroundMask()) != null;
    }

    Vector2 FootWorldPoint() =>
        new Vector2(_rb.position.x, _body.bounds.min.y);

    Vector2 HeadOrigin() =>
        _rb.position + Vector2.up * (_body.bounds.extents.y - 0.02f);

    Vector2 MidBodyOrigin() =>
        _rb.position + Vector2.up * (_body.bounds.extents.y * 0.35f);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        var rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();
        if (rb == null || col == null) return;

        int face = Application.isPlaying ? _face : (startFacing >= 0 ? 1 : -1);
        Vector2 gc = rb.position + groundCheckOffset;
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.35f);
        UnityEditor.Handles.DrawWireDisc(gc, Vector3.forward, groundCheckRadius);

        Gizmos.color = obstacleAhead ? new Color(1f, 0.3f, 0.2f) : new Color(0.2f, 0.85f, 1f);
        Gizmos.DrawLine(_gizmoWallRayFrom, _gizmoWallRayTo);

        Gizmos.color = wallAbove ? Color.red : new Color(0.7f, 0.7f, 1f);
        Gizmos.DrawLine(_gizmoCeilingFrom, _gizmoCeilingTo);

        Gizmos.color = edgeDetected ? new Color(1f, 0.85f, 0f) : new Color(0.4f, 0.4f, 0.4f);
        Gizmos.DrawLine(_gizmoGapFrom, _gizmoGapTo);

        if (_gizmoHasJumpTarget)
        {
            Gizmos.color = platformReachable ? Color.green : Color.magenta;
            Gizmos.DrawWireSphere(_gizmoJumpTarget, 0.12f);
            Gizmos.DrawLine(col.bounds.center, _gizmoJumpTarget);
        }

        UnityEditor.Handles.Label(col.bounds.max + Vector3.up * 0.15f,
            $"state={_state}\nobs={obstacleAhead} reach={platformReachable}\nedge={edgeDetected} ceil={wallAbove}\nunder={underOverhang}");
    }
#endif
}
