using UnityEngine;

/// <summary>
/// ПОСЛЕ <see cref="EnemyAI"/> (<see cref="DefaultExecutionOrder"/>).
/// Разная высота: игрок выше — подбег и прыжок; ниже — спуск (к краю платформы и вниз, в воздухе подруливание по X);
/// почти один уровень — подталкивание при залипании.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ChandelierAI : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] Transform player;
    [SerializeField] Rigidbody2D playerBody;
    [SerializeField] Transform groundCheck;
    [SerializeField] Animator animator;
    [Tooltip("Если есть дочерний GroundSensor с Sensor_Bandit — земля точнее, чем OverlapCircle.")]
    [SerializeField] Sensor_Bandit groundSensor;

    [Header("Спуск — игрок ниже")]
    [Tooltip("Если (игрок.y − бот.y) < этого значения (отрицательно) — идём к игроку по X или к краю платформы.")]
    [SerializeField] float playerBelowStartDy = -0.12f;
    [SerializeField] float descendRunSpeed = 3.45f;
    [Tooltip("Пока |ΔX| больше этого — всегда идём к герою по X. Меньше — выбираем сторону схода с края (лучи). Не делай слишком большим, иначе бот будет метаться влево-вправо.")]
    [SerializeField] float descendVerticalAlignSlack = 0.16f;
    [SerializeField] float descendRayLength = 4.5f;
    [Tooltip("В полёте подтягиваемся по X к герою, если не прямо над ним.")]
    [SerializeField] float descendAirSteerSpeed = 2.1f;
    [SerializeField] float descendAirSteerMinDx = 0.05f;

    [Header("Условие «игрок наверху»")]
    [SerializeField] float playerHigherThanBotBy = 0.55f;
    [SerializeField] float maxHorizontalToJump = 3.25f;

    [Header("Прыжок (linearVelocity, как у героя)")]
    [SerializeField] float jumpVerticalSpeed = 15f;
    [SerializeField] float jumpHorizontalPush = 2.85f;
    [SerializeField] float jumpCooldown = 0.42f;
    [SerializeField] float groundSensorJumpDisable = 0.18f;

    [Header("Подбег пока далеко по X (режим «игрок выше»)")]
    [SerializeField] float runSpeed = 3.6f;

    [Header("Подталкивание, если игрок НЕ выше порога")]
    [Tooltip("Если |ΔX| больше этого и горизонталь почти 0 — идём к игроку (тот же уровень / ниже / чуть выше, но ещё не «на мостике»).")]
    [SerializeField] float assistMinHorizontalDistance = 0.22f;
    [SerializeField] float assistRunSpeed = 3.35f;
    [Tooltip("Подталкивание только если |vx| меньше этого (основной AI «отпустил» движение).")]
    [SerializeField] float assistStallVelocityX = 0.28f;
    [SerializeField] bool assistOnlyWhenGrounded = true;

    [Header("Запасной контакт с землёй")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckRadius = 0.18f;
    [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.42f);

    [Header("Поворот (как EnemyAI: сохраняем |scale| префаба, меняем только знак X)")]
    [SerializeField] bool flipByNegativeScaleX = true;
    [Tooltip("Если текущий |scale.x| почти 0 — подставить это значение.")]
    [SerializeField] float fallbackScaleX = 1f;

    Rigidbody2D _rb;
    Collider2D _body;
    float _nextJumpTime;
    /// <summary>При равных лучах не менять сторону каждый кадр.</summary>
    float _descendEdgeLatchDir;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _body = GetComponent<Collider2D>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (groundSensor == null)
            groundSensor = GetComponentInChildren<Sensor_Bandit>(true);
        if (Mathf.Abs(fallbackScaleX) < 1e-3f)
            fallbackScaleX = 1f;
    }

    void FixedUpdate()
    {
        ResolvePlayer();
        if (player == null)
            return;

        float dy = player.position.y - transform.position.y;
        float dx = player.position.x - transform.position.x;
        bool grounded = IsGrounded();

        if (dy >= playerBelowStartDy)
            _descendEdgeLatchDir = 0f;

        if (dy > playerHigherThanBotBy)
        {
            RunClimbTowardPlayerAbove(grounded, dx);
            return;
        }

        if (dy < playerBelowStartDy)
        {
            RunDescendTowardPlayerBelow(grounded, dx);
            return;
        }

        if (!assistOnlyWhenGrounded || grounded)
        {
            if (Mathf.Abs(dx) > assistMinHorizontalDistance &&
                Mathf.Abs(_rb.linearVelocity.x) < assistStallVelocityX)
            {
                float sx = Mathf.Sign(dx);
                _rb.linearVelocity = new Vector2(sx * assistRunSpeed, _rb.linearVelocity.y);
                ApplyFacing(sx);
            }
        }
    }

    void RunClimbTowardPlayerAbove(bool grounded, float dx)
    {
        if (grounded && Mathf.Abs(dx) <= maxHorizontalToJump && Time.time >= _nextJumpTime)
        {
            float sx = Mathf.Abs(dx) < 0.08f ? 1f : Mathf.Sign(dx);
            _rb.linearVelocity = new Vector2(sx * jumpHorizontalPush, jumpVerticalSpeed);
            _nextJumpTime = Time.time + jumpCooldown;
            if (groundSensor != null)
                groundSensor.Disable(groundSensorJumpDisable);
            if (animator != null)
                animator.SetTrigger("Jump");
            ApplyFacing(sx);
            return;
        }

        if (grounded)
        {
            float sx = Mathf.Abs(dx) < 0.08f ? 1f : Mathf.Sign(dx);
            _rb.linearVelocity = new Vector2(sx * runSpeed, _rb.linearVelocity.y);
            ApplyFacing(sx);
        }
    }

    void RunDescendTowardPlayerBelow(bool grounded, float dx)
    {
        float sx;
        if (Mathf.Abs(dx) > descendVerticalAlignSlack)
            sx = Mathf.Sign(dx);
        else
            sx = PickDropOffDirection(dx);

        if (grounded)
        {
            _rb.linearVelocity = new Vector2(sx * descendRunSpeed, _rb.linearVelocity.y);
            ApplyFacing(sx);
            return;
        }

        if (Mathf.Abs(dx) > descendAirSteerMinDx)
        {
            float airSx = Mathf.Sign(dx);
            _rb.linearVelocity = new Vector2(airSx * descendAirSteerSpeed, _rb.linearVelocity.y);
            ApplyFacing(airSx);
        }
    }

    const float DropRayDistanceTieEps = 0.08f;

    /// <summary>Куда идти по X, чтобы сойти с платформы: сторона, под которой раньше кончается земля.</summary>
    float PickDropOffDirection(float dx)
    {
        if (_body == null)
        {
            float c = TieBreakDropDir(dx);
            _descendEdgeLatchDir = c;
            return c;
        }

        float feetY = _body.bounds.min.y;
        float x = _rb.position.x;
        float half = Mathf.Max(0.06f, _body.bounds.extents.x * 0.42f);
        Vector2 oL = new Vector2(x - half, feetY + 0.06f);
        Vector2 oR = new Vector2(x + half, feetY + 0.06f);
        RaycastHit2D hL = RaycastGroundDown(oL);
        RaycastHit2D hR = RaycastGroundDown(oR);

        float chosen;
        if (hL.collider == null && hR.collider != null)
            chosen = -1f;
        else if (hL.collider != null && hR.collider == null)
            chosen = 1f;
        else if (hL.collider != null && hR.collider != null)
        {
            float d = hL.distance - hR.distance;
            if (d < -DropRayDistanceTieEps)
                chosen = -1f;
            else if (d > DropRayDistanceTieEps)
                chosen = 1f;
            else
                chosen = TieBreakDropDir(dx);
        }
        else
            chosen = TieBreakDropDir(dx);

        _descendEdgeLatchDir = chosen;
        return chosen;
    }

    float TieBreakDropDir(float dx)
    {
        if (Mathf.Abs(dx) > 0.02f)
            return Mathf.Sign(dx);
        if (Mathf.Abs(_descendEdgeLatchDir) > 0.01f)
            return _descendEdgeLatchDir;
        return 1f;
    }

    RaycastHit2D RaycastGroundDown(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, descendRayLength, groundLayer);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null) continue;
            if (c.transform.root == transform.root)
                continue;
            return hits[i];
        }
        return default;
    }

    bool IsGrounded()
    {
        if (groundSensor != null)
            return groundSensor.State();
        Vector2 p = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)_rb.position + groundCheckOffset;
        return Physics2D.OverlapCircle(p, groundCheckRadius, groundLayer) != null;
    }

    void ApplyFacing(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return;

        float magX = Mathf.Abs(transform.localScale.x);
        if (magX < 1e-3f)
            magX = Mathf.Abs(fallbackScaleX);
        if (magX < 1e-3f)
            magX = 1f;

        float sy = Mathf.Abs(transform.localScale.y);
        float sz = Mathf.Abs(transform.localScale.z);
        if (sy < 1e-3f) sy = 1f;
        if (sz < 1e-3f) sz = 1f;

        if (!flipByNegativeScaleX)
        {
            transform.localScale = new Vector3(dirX > 0f ? magX : -magX, sy, sz);
            return;
        }

        transform.localScale = new Vector3(
            dirX > 0f ? -magX : magX,
            sy,
            sz);
    }

    void ResolvePlayer()
    {
        if (player != null && playerBody != null)
            return;
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform.root;
            else
            {
                HeroKnight hk = Object.FindAnyObjectByType<HeroKnight>();
                if (hk != null)
                    player = hk.transform.root;
            }
        }
        if (player != null && playerBody == null)
            playerBody = player.GetComponentInChildren<Rigidbody2D>();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;
        Vector2 p = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position + groundCheckOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, groundCheckRadius);
    }
#endif
}
