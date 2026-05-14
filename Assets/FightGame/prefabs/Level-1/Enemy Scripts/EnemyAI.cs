using System.Collections.Generic;
using UnityEngine;

/// <summary>Преследование игрока, щит, прыжки на платформу к цели. Общий патруль по лучам без боя — <see cref="EnemyPlatformMovementAI"/>.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    public float speed = 2.5f;
    public float stopDistance = 0.2f;
    public float gravityScale = 3f;
    public bool flipByScale = true;
    public int maxHp = 4;
    [Header("Уклонение от удара игрока")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.45f;
    public float dodgeInstantStep = 0.35f;
    public float dodgeHorizontalSpeed = 4.5f;
    public float dodgeControlLockDuration = 0.28f;

    [Header("Удар по поднятому щиту")]
    [Tooltip("Секунды без контактной атаки после попадания по активному щиту (отдельно от cooldown в EnemyContactDamage).")]
    public float shieldBlockMeleePauseDuration = 0.5f;
    [Tooltip("Игрок держит щит на нас: не подходим ближе этого |ΔX| (иначе rigidbody проталкивает героя).")]
    public float shieldBraceStopDistance = 0.52f;
    [Tooltip("Если всё же ближе по X — отходим, пока не выйдем из зоны.")]
    public float shieldBraceMinSeparation = 0.48f;
    public float shieldBraceBackupSpeed = 1.4f;

    [Header("Замедление после удара")]
    [Tooltip("На сколько секунд враг двигается медленнее после попадания по игроку или по щиту.")]
    public float hitImpactSlowDuration = 0.5f;
    [Range(0.05f, 1f)]
    [Tooltip("Доля обычной скорости в это время (0.25 ≈ в четыре раза медленнее).")]
    public float hitImpactSpeedMultiplier = 0.28f;

    [Header("Прыжок на платформу (игрок сверху)")]
    [Tooltip("Игрок на заметно более высокой позиции.")]
    public float playerAboveMinHeight = 0.45f;
    [Tooltip("Сколько секунд игрок остаётся наверху, прежде чем враг начнёт обход и прыжок.")]
    public float playerAbovePersistSeconds = 0.12f;
    [Tooltip("Насколько далеко по X от игрока стоит точка обхода (сначала идём к player.x ± это).")]
    public float platformDetourOffset = 2.85f;
    [Tooltip("После первой точки обхода идём ещё дальше от платформы, чтобы оторваться от земли и прыгнуть дугой.")]
    public float detourPastPlatformEdge = 0.85f;
    [Tooltip("Длина лучей влево/вправо, чтобы выбрать более свободную сторону обхода.")]
    public float detourSideProbeDistance = 4f;
    [Tooltip("Считаем, что до точки обхода дошли, если |x - цель| меньше этого.")]
    public float detourArrivalSlack = 0.42f;
    [Tooltip("Если уперлись в край платформы, ещё чуть сдвигаем цель в ту же сторону.")]
    public float detourExtraStep = 0.55f;
    [Tooltip("Шаг в сторону обхода, пока горизонтальный луч к игроку упирается в коллайдер платформы (полный выход из-под нависания).")]
    public float detourClearanceStride = 0.48f;
    [Tooltip("Запас по X при луче «враг → игрок», чтобы не цеплять коллайдер игрока.")]
    public float platformHorizProbeMargin = 0.18f;
    [Tooltip("Слои коллайдеров платформ/стен для лучей (не включай игрока/врага).")]
    public LayerMask platformBlockingLayers = ~0;
    [Tooltip("Слои, исключаемые из поиска платформы и лучей (пикапы, декор с коллайдером и т.п.).")]
    public LayerMask platformDetectionIgnoreLayers = 0;
    [Tooltip("Мин. ширина коллайдера по X, чтобы считать его «платформой» (отсечь узкий мусор в Overlap).")]
    public float platformMinColliderWorldWidth = 0.28f;
    [Tooltip("Если обход упирается в стену — сменить сторону через столько секунд.")]
    public float flankStuckFlipTime = 0.85f;
    [Tooltip("Прыжок только если враг уже сбоку: мин. |ΔX| до игрока (чуть ниже underPlayerJumpMaxX — без мёртвой зоны).")]
    public float sideJumpMinHorizontal = 0.42f;
    [Tooltip("Макс. |ΔX| для прыжка наверх (слишком далеко — подойти ближе).")]
    public float sideJumpMaxHorizontal = 1.35f;
    [Tooltip("Макс. |ΔX| до прыжка наверх после обхода (держим врага близко к краю — почти вертикальный прыжок).")]
    public float detourJumpMaxHorizontal = 1.22f;
    [Header("Прыжок снизу (под игроком на платформе)")]
    [Tooltip("Если |ΔX| меньше — прыжок снизу; должен пересекаться с боковым прыжком, иначе зона 0.48–0.52 даёт «вечный» шаг.")]
    public float underPlayerJumpMaxX = 0.58f;
    [Tooltip("Мин. высота до игрока для прыжка снизу.")]
    public float underJumpMinDy = 0.38f;
    [Tooltip("Макс. высота (выше — не дотянется jumpForce, нужен другой маршрут).")]
    public float underJumpMaxDy = 5.5f;
    [Tooltip("Секунды «игрок наверху» перед прыжком снизу (короче, чем полный обход).")]
    public float underPlayerAlignedPersistSeconds = 0.22f;
    [Tooltip("Сдвиг от target.position.y к уровню опорной поверхности игрока (зависит от pivot префаба).")]
    public float underJumpTargetFeetYOffset = -0.42f;
    [Tooltip("Множитель jumpForce только для прыжка снизу под игроком.")]
    public float underJumpForceMultiplier = 1.14f;
    [Tooltip("Горизонталь к игроку при прыжке снизу (без этого Rigidbody только вверх — не долетает до платформы).")]
    public float underJumpHorizontalSpeed = 2.65f;
    [Tooltip("Множитель platformApproachAirSpeed, пока активен прыжок снизу (и на подъёме, и на спуске).")]
    public float underJumpAirSteerMultiplier = 1.52f;
    [Tooltip("Вертикальная скорость прыжка.")]
    public float jumpForce = 12.25f;
    [Tooltip("Секунды почти без горизонтали в начале прыжка (не «выстрел» под 45°).")]
    public float platformJumpVerticalHold = 0.14f;
    [Tooltip("Слабая подтяжка к игроку в воздухе только на спуске (после вершины прыжка).")]
    public float platformApproachAirSpeed = 2.15f;
    [Tooltip("Окно слабой подтяжки в воздухе после прыжка.")]
    public float platformApproachAirDuration = 0.42f;
    [Header("Воздух (как у героя: не залипать у стен)")]
    [Tooltip("Слои окружения для Cast в воздухе (платформы/стены, не персонажи).")]
    public LayerMask airCollisionLayers = ~0;
    public float airWallCastDistance = 0.22f;
    public float airStuckFallAssist = -3.5f;
    public float airStuckTime = 0.06f;
    [Tooltip("Пауза между прыжками преследования.")]
    public float platformJumpCooldown = 1.15f;
    [Tooltip("После прыжка отключаем землю на мгновение.")]
    public float groundSensorJumpDisable = 0.2f;
    [Header("Цель приземления на платформе")]
    [Tooltip("Горизонталь цели прыжка = позиция игрока, но зажатая внутрь коллайдера платформы на столько от края (меньше ударов о бок).")]
    public float platformLandingEdgeInset = 0.24f;
    [Tooltip("Луч вниз от игрока: старт выше pivot (мировые единицы).")]
    public float platformProbeStartAbovePlayer = 0.55f;
    [Tooltip("Длина луча вниз для поиска платформы под ногами.")]
    public float platformProbeDownDistance = 7f;
    [Tooltip("Мин. зазор между краем платформы и коллайдером врага перед прыжком = ширина тела × множитель + доп. метры ниже.")]
    public float platformJumpStandoffBodyMultiplier = 1.75f;
    [Tooltip("Дополнительный зазор в мирах (!) поверх формулы по ширине тела — больше разбег до прыжка.")]
    public float platformJumpStandoffExtraWorldUnits = 0.42f;
    [Tooltip("Пока зазор до платформы меньше нужного — множитель скорости бега к точке разбега.")]
    public float platformStandoffRunSpeedMultiplier = 1.5f;
    [Tooltip("Если вкл — прыжок возможен только после набора горизонтального зазора; если выкл — всё равно прыгает (разбег только помогает позиции).")]
    public bool platformJumpRequireHorizontalStandoff = false;
    [Tooltip("Боковой прыжок: разрешить, когда прямой луч к игроку упирается в платформу сверху (типичный случай «я снизу, игрок на платформе»).")]
    public bool platformJumpAllowWhenVerticalPathBlocked = true;
    [Tooltip("Ширина OverlapBox под ногами игрока для поиска платформы (надёжнее лучей).")]
    public float platformFeetOverlapWidth = 3.2f;
    [Tooltip("Высота OverlapBox под ногами игрока.")]
    public float platformFeetOverlapHeight = 0.55f;
    [Header("Прыжок: игрок заметно выше (агрессивнее)")]
    [Tooltip("Если ΔY ≥ playerAboveMinHeight + это — можно прыгать с более широкого диапазона по X (только вместе с climbIntent).")]
    public float relaxedHighJumpExtraDy = 0.75f;
    [Tooltip("Мин. секунд «игрок сверху» в этом режиме (не ниже обычного порога — иначе лишние прыжки).")]
    public float relaxedHighJumpMinPersistSeconds = 0.14f;
    [Tooltip("Нижняя граница |ΔX| в ослабленном режиме.")]
    public float relaxedHighJumpMinHorizontal = 0.1f;
    [Tooltip("Верхняя граница |ΔX| в ослабленном режиме.")]
    public float relaxedHighJumpMaxHorizontal = 2.75f;
    [Header("Прыжок: боковой на платформу")]
    [Tooltip("Вертикальный коридор свободен и нет нависания — боковой прыжок только если игрок заметно выше (иначе высокие подпрыгивания при беге).")]
    public float sideJumpRequireExtraDyWhenOpen = 0.55f;
    [Range(0.55f, 1.05f)]
    [Tooltip("Множитель импульса вверх для бокового прыжка (ниже — меньше «батут» за спиной).")]
    public float sideJumpVerticalMultiplier = 0.96f;
    [Tooltip("Горизонталь к точке приземления в начале бокового прыжка (дуга вместо столба).")]
    public float sideJumpTakeoffHorizontalSpeed = 3.05f;
    [Tooltip("Длительность «почти только вверх» в начале бокового прыжка (короче, чем прыжок снизу).")]
    public float platformJumpVerticalHoldSide = 0.045f;
    [Header("Прыжок: только с платформы игрока")]
    [Tooltip("Если вкл — не прыгает «просто потому что игрок выше по Y»: нужна опора под ногами игрока, заметно выше уровня ног врага.")]
    public bool platformJumpOnlyWhenPlayerOnElevatedSurface = true;
    [Tooltip("Макс. |опорная поверхность − «ноги» игрока|, чтобы считать, что он стоит на найденном коллайдере.")]
    public float playerFeetPlatformContactSlop = 0.35f;
    [Tooltip("Мин. зазор: верх коллайдера под игроком выше низа коллайдера врага (иначе общий пол / один этаж).")]
    public float platformTopMinAboveEnemyFeet = 0.2f;
    [Header("Обход к платформе (кратчайший путь)")]
    [Tooltip("Отступ от края AABB платформы до цели X «встать сбоку», чтобы выйти из-под нависания.")]
    public float climbEdgeStandoffPad = 0.34f;
    [Tooltip("Если платформа уже по X — не гоняем к «краям» (они слипаются → дёрганье); идём под точку приземления и прыгаем.")]
    public float narrowClimbPlatformWidth = 1.15f;
    [Tooltip("На узкой платформе на сколько расширить допустимый |ΔX| для прыжка «снизу под игроком».")]
    public float narrowPlatUnderJumpExtraX = 0.22f;

    private Rigidbody2D rb;
    private Transform target;
    private Animator animator;
    private Sensor_Bandit groundSensor;
    private float dodgeLockUntil;
    private float hitImpactSlowUntil;
    private float criticalShieldBreakMeleeUntil;
    private float shieldBlockMeleeLockUntil;
    private float playerAboveSince = -1f;
    private float nextPlatformJumpTime;
    private int flankDir = 1;
    private int detourSign = 1;
    private bool hasDetourChoice;
    private float detourStuckTimer;
    private float platformAirSteerUntil = -1f;
    private float platformVerticalJumpUntil = -1f;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
    private ContactFilter2D airContactFilter;
    private float airBlockedHorizTimer;
    private bool lastJumpWasStraightUnder;
    private bool lastPlatformJumpWasSideArc;
    private bool underJumpAirborneAssist;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        Transform gs = transform.Find("GroundSensor");
        if (gs != null)
            groundSensor = gs.GetComponent<Sensor_Bandit>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = gravityScale;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        airContactFilter.useTriggers = false;
        airContactFilter.useLayerMask = true;
        airContactFilter.SetLayerMask(EffectiveAirCollisionMask);
        if (GetComponent<SimpleHealth>() == null)
        {
            SimpleHealth health = gameObject.AddComponent<SimpleHealth>();
            health.maxHp = maxHp;
        }
    }

    void Start()
    {
        AcquireTarget();
        flankDir = Random.value < 0.5f ? -1 : 1;
    }

    void FixedUpdate()
    {
        if (Time.time < dodgeLockUntil)
        {
            return;
        }

        rb.WakeUp();

        if (target == null || target == transform || !IsAliveCombatTarget(target))
        {
            AcquireTarget();
        }

        if (target == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (animator != null) animator.SetInteger("AnimState", 0);
            return;
        }

        float deltaX = target.position.x - transform.position.x;
        float distanceX = Mathf.Abs(deltaX);
        float dyPlayer = target.position.y - transform.position.y;
        float landingGoalX = (dyPlayer > playerAboveMinHeight)
            ? ComputePlatformLandingGoalX()
            : target.position.x;
        float distanceToLandingX = Mathf.Abs(landingGoalX - transform.position.x);

        bool playerBracingAgainstUs = false;
        PlayerShieldDefense playerShield = target.GetComponentInChildren<PlayerShieldDefense>(true);
        if (playerShield != null)
            playerBracingAgainstUs = playerShield.ShouldBlockHitFromWorldPosition(rb.position);

        UpdatePlayerAbovePlatformTimer(dyPlayer);

        bool climbIntent = playerAboveSince > 0f &&
            (Time.time - playerAboveSince) >= playerAbovePersistSeconds &&
            dyPlayer > playerAboveMinHeight;

        bool horizontalUnderHang = IsHorizontallyUnderPlatformCollider();

        bool jumpStraightFromBelowFootprint = ComputeJumpStraightFromBelowFootprint(dyPlayer);

        bool straightUnderGeometryOk = (StraightUnderJumpGeometryOk(dyPlayer, distanceToLandingX, landingGoalX) ||
                                        jumpStraightFromBelowFootprint) &&
            SatisfiesHorizontalJumpStandoff() &&
            PlatformJumpPlayerSurfaceOk();

        bool needDetour = climbIntent && !HasClearVerticalChannelToPlayer() && !straightUnderGeometryOk;

        float steerX = landingGoalX;

        if (climbIntent && !needDetour)
        {
            hasDetourChoice = false;
            detourStuckTimer = 0f;
        }

        if (climbIntent && needDetour && !hasDetourChoice)
        {
            PickDetourSide();
            hasDetourChoice = true;
        }

        if (climbIntent && needDetour && hasDetourChoice)
        {
            if (TryFindPlatformColliderUnderPlayer(out Collider2D climbPlat))
            {
                if (EnemyBodyOverlapsPlatformX(climbPlat.bounds, 0.04f))
                {
                    if (climbPlat.bounds.size.x <= narrowClimbPlatformWidth)
                        steerX = landingGoalX;
                    else
                    {
                        steerX = ReachablePlatformEdgeStandX(rb.position.x, climbPlat.bounds, detourSign);
                        float sd = steerX - rb.position.x;
                        if (Mathf.Abs(sd) > 0.04f)
                            detourSign = sd > 0f ? 1 : -1;
                    }
                }
                else
                {
                    float idealSide = (sideJumpMinHorizontal + sideJumpMaxHorizontal) * 0.5f;
                    float ex = rb.position.x;
                    int pickSign = Mathf.Abs(ex - landingGoalX) < 0.18f ? detourSign : (ex > landingGoalX ? 1 : -1);
                    steerX = landingGoalX + pickSign * idealSide;
                }
            }
            else
            {
                float innerX = landingGoalX + detourSign * platformDetourOffset;
                float outerX = landingGoalX + detourSign * (platformDetourOffset + detourPastPlatformEdge);
                bool pastInner = Mathf.Abs(rb.position.x - innerX) <= detourArrivalSlack
                    || (detourSign > 0f && rb.position.x >= innerX - detourArrivalSlack)
                    || (detourSign < 0f && rb.position.x <= innerX + detourArrivalSlack);

                if (!pastInner)
                    steerX = innerX;
                else
                {
                    if (PlatformOverhangBlocksPathToPlayer())
                        steerX = rb.position.x + detourSign * detourClearanceStride;
                    else if (!HasClearVerticalChannelToPlayer())
                    {
                        steerX = rb.position.x + detourSign * Mathf.Max(detourExtraStep, detourClearanceStride * 0.85f);
                        if (Mathf.Abs(rb.position.x - outerX) <= detourArrivalSlack)
                            steerX = rb.position.x + detourSign * detourExtraStep;
                    }
                    else
                    {
                        float ideal = (sideJumpMinHorizontal + sideJumpMaxHorizontal) * 0.5f;
                        float ex = rb.position.x;
                        float away = Mathf.Abs(ex - landingGoalX) < 0.1f ? detourSign : Mathf.Sign(ex - landingGoalX);
                        if (Mathf.Abs(away) < 0.01f) away = detourSign;
                        steerX = landingGoalX + away * ideal;
                    }
                }
            }

            if (IsSideBlocked(new Vector2(detourSign, 0f)))
            {
                detourSign = -detourSign;
                flankDir = -flankDir;
                detourStuckTimer = 0f;
            }

            bool underFootprintStuck = horizontalUnderHang &&
                TryFindPlatformColliderUnderPlayer(out Collider2D hangPlat2) &&
                EnemyBodyOverlapsPlatformX(hangPlat2.bounds, 0.04f);

            if (groundSensor != null && groundSensor.State() && Mathf.Abs(rb.linearVelocity.x) < 0.11f)
            {
                detourStuckTimer += Time.fixedDeltaTime;
                if (detourStuckTimer >= flankStuckFlipTime && !underFootprintStuck)
                {
                    detourSign = -detourSign;
                    flankDir = -flankDir;
                    detourStuckTimer = 0f;
                }
            }
            else
                detourStuckTimer = 0f;
        }
        else if (!climbIntent)
        {
            hasDetourChoice = false;
            detourStuckTimer = 0f;
        }

        if (platformJumpRequireHorizontalStandoff && dyPlayer > playerAboveMinHeight &&
            TryFindPlatformColliderUnderPlayer(out Collider2D jumpPlat))
            steerX = AdjustSteerXForJumpRunUpStandoff(steerX, jumpPlat.bounds);

        float moveDelta = steerX - transform.position.x;
        float moveDist = Mathf.Abs(moveDelta);

        float runSpeed = speed;
        if (Time.time < hitImpactSlowUntil)
            runSpeed *= hitImpactSpeedMultiplier;

        if (platformJumpRequireHorizontalStandoff &&
            dyPlayer > playerAboveMinHeight &&
            TryFindPlatformColliderUnderPlayer(out Collider2D platRun) &&
            !HorizontalGapToPlatformSufficient(platRun.bounds))
            runSpeed *= platformStandoffRunSpeedMultiplier;

        bool meleeLocked = Time.time < criticalShieldBreakMeleeUntil;

        lastJumpWasStraightUnder = false;
        bool didPlatformJump = TryPlatformJumpTowardPlayer(dyPlayer, distanceToLandingX, climbIntent, straightUnderGeometryOk);
        if (didPlatformJump && lastJumpWasStraightUnder)
            underJumpAirborneAssist = true;
        else if (groundSensor != null && groundSensor.State() && !didPlatformJump && rb.linearVelocity.y <= 0.12f)
            underJumpAirborneAssist = false;

        float activeStopDistance = stopDistance;
        if (playerBracingAgainstUs && !climbIntent)
            activeStopDistance = Mathf.Max(stopDistance, shieldBraceStopDistance);

        float moveX = 0f;
        if (moveDist > activeStopDistance)
            moveX = Mathf.Sign(moveDelta) * runSpeed;

        bool groundedForMove = groundSensor != null && groundSensor.State();

        if (didPlatformJump)
        {
            float vHold = platformJumpVerticalHold;
            if (lastPlatformJumpWasSideArc)
                vHold = platformJumpVerticalHoldSide;
            platformVerticalJumpUntil = Time.time + vHold;
            platformAirSteerUntil = Time.time + platformApproachAirDuration;
        }
        else if (groundedForMove)
            platformVerticalJumpUntil = -1f;

        if (Time.time < platformVerticalJumpUntil)
        {
            if (underJumpAirborneAssist && target != null)
            {
                float toGoal = landingGoalX - rb.position.x;
                if (Mathf.Abs(toGoal) > 0.04f)
                    moveX = Mathf.Sign(toGoal) * underJumpHorizontalSpeed;
                else
                    moveX = 0f;
            }
            else if (lastPlatformJumpWasSideArc && target != null)
            {
                float toGoal = landingGoalX - rb.position.x;
                if (Mathf.Abs(toGoal) > 0.04f)
                    moveX = Mathf.Sign(toGoal) * sideJumpTakeoffHorizontalSpeed;
                else
                    moveX = 0f;
            }
            else
                moveX = 0f;
        }
        else if (!groundedForMove && Time.time < platformAirSteerUntil && dyPlayer > playerAboveMinHeight &&
                 (rb.linearVelocity.y <= 0.08f || underJumpAirborneAssist))
        {
            if (target != null)
            {
                float toGoalX = landingGoalX - rb.position.x;
                float airSpeed = platformApproachAirSpeed * (underJumpAirborneAssist ? underJumpAirSteerMultiplier : 1f);
                if (Mathf.Abs(toGoalX) > 0.07f)
                    moveX = Mathf.Sign(toGoalX) * airSpeed;
                else
                    moveX = 0f;
            }
        }

        float vy = rb.linearVelocity.y;
        if (didPlatformJump)
        {
            float vMul = 1f;
            if (lastJumpWasStraightUnder)
                vMul = underJumpForceMultiplier;
            else if (lastPlatformJumpWasSideArc)
                vMul = sideJumpVerticalMultiplier;
            vy = jumpForce * vMul;
        }

        float preCollisionMoveX = moveX;
        if (!groundedForMove && bodyCollider != null && Mathf.Abs(moveX) > 0.02f)
        {
            if (AirHorizontalCastBlocks(Mathf.Sign(moveX)))
                moveX = 0f;
        }

        if (groundedForMove)
            airBlockedHorizTimer = 0f;
        else if (Mathf.Abs(preCollisionMoveX) > 0.18f && Mathf.Abs(moveX) < 0.01f)
        {
            airBlockedHorizTimer += Time.fixedDeltaTime;
            if (airBlockedHorizTimer >= airStuckTime)
                vy = Mathf.Min(vy, airStuckFallAssist);
        }
        else
            airBlockedHorizTimer = 0f;

        if (playerBracingAgainstUs && groundedForMove && !climbIntent &&
            distanceX < shieldBraceMinSeparation)
        {
            float sepX = rb.position.x - target.position.x;
            float awaySign;
            if (sepX > 0.02f) awaySign = 1f;
            else if (sepX < -0.02f) awaySign = -1f;
            else awaySign = -Mathf.Sign(deltaX);
            moveX = awaySign * shieldBraceBackupSpeed;
        }

        rb.linearVelocity = new Vector2(moveX, vy);

        if (animator != null)
        {
            if (moveDist > activeStopDistance)
                animator.SetInteger("AnimState", 2);
            else
                animator.SetInteger("AnimState", meleeLocked ? 0 : 1);
        }

        if (flipByScale && (moveDist > 0.01f || distanceX > 0.01f))
        {
            float faceX = Mathf.Abs(moveDelta) > 0.01f ? moveDelta : deltaX;
            float sx = Mathf.Abs(transform.localScale.x);
            if (sx < 1e-4f) sx = 1f;
            transform.localScale = new Vector3(
                faceX > 0f ? -sx : sx,
                Mathf.Abs(transform.localScale.y),
                Mathf.Abs(transform.localScale.z)
            );
        }

        if (animator != null && groundSensor != null)
            animator.SetBool("Grounded", groundSensor.State());
    }

    static bool IsAliveCombatTarget(Transform root)
    {
        if (root == null || !root.gameObject.activeInHierarchy) return false;
        SimpleHealth h = root.GetComponentInChildren<SimpleHealth>(true);
        if (h == null) return true;
        return !h.IsDead;
    }

    private void AcquireTarget()
    {
        Transform selfRoot = transform.root;
        List<Transform> candidates = new List<Transform>();

        // 1) Preferred: tag Player
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            Transform root = players[i].transform.root;
            if (root == selfRoot) continue;
            if (!IsAliveCombatTarget(root)) continue;
            if (!candidates.Contains(root)) candidates.Add(root);
        }

        // 2) Fallback: object that has player attack script
        PlayerAttackDamage[] playerAttackScripts = Object.FindObjectsByType<PlayerAttackDamage>(FindObjectsInactive.Exclude);
        for (int i = 0; i < playerAttackScripts.Length; i++)
        {
            Transform root = playerAttackScripts[i].transform.root;
            if (root == selfRoot) continue;
            if (!IsAliveCombatTarget(root)) continue;
            if (!candidates.Contains(root)) candidates.Add(root);
        }

        // 3) Fallback: HeroKnight from demo asset
        HeroKnight[] heroes = Object.FindObjectsByType<HeroKnight>(FindObjectsInactive.Exclude);
        for (int i = 0; i < heroes.Length; i++)
        {
            Transform root = heroes[i].transform.root;
            if (root == selfRoot) continue;
            if (!IsAliveCombatTarget(root)) continue;
            if (!candidates.Contains(root)) candidates.Add(root);
        }

        // 4) Last fallback by name
        if (candidates.Count == 0)
        {
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform root = allTransforms[i].root;
                if (root == selfRoot) continue;
                if (!IsAliveCombatTarget(root)) continue;
                string lowerName = root.name.ToLowerInvariant();
                if (!lowerName.Contains("hero") && !lowerName.Contains("knight") && !lowerName.Contains("player"))
                    continue;
                if (!candidates.Contains(root)) candidates.Add(root);
            }
        }

        float bestDistance = float.MaxValue;
        Transform bestTarget = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            float dist = Mathf.Abs(candidates[i].position.x - transform.position.x);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestTarget = candidates[i];
            }
        }

        target = bestTarget;
    }

    void UpdatePlayerAbovePlatformTimer(float dyPlayer)
    {
        if (dyPlayer > playerAboveMinHeight)
        {
            if (playerAboveSince < 0f)
                playerAboveSince = Time.time;
        }
        else
            playerAboveSince = -1f;
    }

    bool ReachedDetourJumpSpot()
    {
        if (target == null) return false;
        float goalX = ComputePlatformLandingGoalX();
        float innerX = goalX + detourSign * platformDetourOffset;
        bool pastInner = Mathf.Abs(rb.position.x - innerX) <= detourArrivalSlack
            || (detourSign > 0f && rb.position.x >= innerX - detourArrivalSlack)
            || (detourSign < 0f && rb.position.x <= innerX + detourArrivalSlack);
        return pastInner && !PlatformOverhangBlocksPathToPlayer();
    }

    /// <summary>Позиция X на верхней грани платформы под игроком, с отступом от вертикальных краёв AABB.</summary>
    float ComputePlatformLandingGoalX()
    {
        if (target == null) return 0f;
        if (!TryFindPlatformColliderUnderPlayer(out Collider2D plat))
            return target.position.x;
        Bounds b = plat.bounds;
        float inset = Mathf.Max(0.04f, platformLandingEdgeInset);
        float halfRange = (b.max.x - b.min.x) * 0.5f;
        if (halfRange < 0.04f)
            return target.position.x;
        float useInset = Mathf.Min(inset, halfRange * 0.45f);
        return Mathf.Clamp(target.position.x, b.min.x + useInset, b.max.x - useInset);
    }

    bool TryFindPlatformColliderUnderPlayer(out Collider2D platformCollider)
    {
        platformCollider = null;
        if (target == null) return false;
        Transform playerRoot = target.root;
        Transform selfRoot = transform.root;
        float feetRef = target.position.y + underJumpTargetFeetYOffset;

        Vector2 feetCenter = new Vector2(target.position.x, feetRef - 0.06f);
        Collider2D[] overlapHits = Physics2D.OverlapBoxAll(
            feetCenter,
            new Vector2(Mathf.Max(0.4f, platformFeetOverlapWidth), Mathf.Max(0.15f, platformFeetOverlapHeight)),
            0f,
            GetEffectivePlatformMask());
        float bestTopWide = float.MinValue;
        Collider2D bestWide = null;
        float bestTopAny = float.MinValue;
        Collider2D bestAny = null;
        float minW = Mathf.Max(0.08f, platformMinColliderWorldWidth);
        for (int i = 0; i < overlapHits.Length; i++)
        {
            Collider2D c = overlapHits[i];
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == playerRoot || c.transform.root == selfRoot) continue;
            float topY = c.bounds.max.y;
            if (topY <= rb.position.y + 0.02f) continue;
            if (topY < feetRef - 1.8f) continue;
            if (topY > target.position.y + 0.55f) continue;
            float widthX = c.bounds.size.x;
            if (topY > bestTopAny)
            {
                bestTopAny = topY;
                bestAny = c;
            }
            if (widthX >= minW && topY > bestTopWide)
            {
                bestTopWide = topY;
                bestWide = c;
            }
        }
        platformCollider = bestWide != null ? bestWide : bestAny;
        if (platformCollider != null)
            return true;

        Vector2 origin = new Vector2(target.position.x, target.position.y + platformProbeStartAbovePlayer);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, platformProbeDownDistance, GetEffectivePlatformMask());
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == playerRoot || c.transform.root == selfRoot) continue;
            float topY = c.bounds.max.y;
            if (topY > feetRef + 0.35f)
                continue;
            if (topY < feetRef - 1.25f)
                continue;
            platformCollider = c;
            return true;
        }
        return false;
    }

    bool HasFeetOverlapPlatformForJump()
    {
        return TryFindPlatformColliderUnderPlayer(out _);
    }

    float GetEnemyFeetWorldY()
    {
        if (bodyCollider != null)
            return bodyCollider.bounds.min.y;
        return rb.position.y - 0.35f;
    }

    /// <summary>Игрок стоит на коллайдере-снаряжении под ногами, который реально выше пола врага (не общая земля).</summary>
    bool PlayerOnElevatedPlatformForJump(out Collider2D platformCollider)
    {
        platformCollider = null;
        if (target == null) return false;
        if (!TryFindPlatformColliderUnderPlayer(out Collider2D plat))
            return false;
        float feetRef = target.position.y + underJumpTargetFeetYOffset;
        float top = plat.bounds.max.y;
        if (Mathf.Abs(feetRef - top) > playerFeetPlatformContactSlop)
            return false;
        if (top < GetEnemyFeetWorldY() + platformTopMinAboveEnemyFeet)
            return false;
        platformCollider = plat;
        return true;
    }

    bool PlatformJumpPlayerSurfaceOk()
    {
        if (!platformJumpOnlyWhenPlayerOnElevatedSurface)
            return true;
        return PlayerOnElevatedPlatformForJump(out _);
    }

    bool EnemyBodyOverlapsPlatformX(Bounds platBounds, float slack)
    {
        float hw = GetEnemyColliderHalfWidth();
        float el = rb.position.x - hw;
        float er = rb.position.x + hw;
        return er > platBounds.min.x + slack && el < platBounds.max.x - slack;
    }

    float ReachablePlatformEdgeStandX(float ex, Bounds platBounds, int signPrefer)
    {
        float pl = platBounds.min.x;
        float pr = platBounds.max.x;
        float pad = Mathf.Max(climbEdgeStandoffPad, GetEnemyColliderHalfWidth() + 0.16f);
        float leftStand = pl - pad;
        float rightStand = pr + pad;
        float dL = Mathf.Abs(ex - leftStand);
        float dR = Mathf.Abs(ex - rightStand);
        bool leftCloser = dL < dR - 0.1f || (Mathf.Abs(dL - dR) <= 0.1f && signPrefer <= 0);
        Vector2 tryDir = leftCloser ? Vector2.left : Vector2.right;
        if (IsSideBlocked(tryDir))
        {
            leftCloser = !leftCloser;
            tryDir = leftCloser ? Vector2.left : Vector2.right;
            if (IsSideBlocked(tryDir))
                return leftCloser ? leftStand : rightStand;
        }
        return leftCloser ? leftStand : rightStand;
    }

    bool IsHorizontallyUnderPlatformCollider()
    {
        if (!TryFindPlatformColliderUnderPlayer(out Collider2D plat)) return false;
        Bounds b = plat.bounds;
        float hw = GetEnemyColliderHalfWidth();
        float el = rb.position.x - hw;
        float er = rb.position.x + hw;
        const float slack = 0.08f;
        return er > b.min.x + slack && el < b.max.x - slack;
    }

    /// <summary>Враг по X стоит под нависающей платформой — можно прыгать вверх, без обхода и без порога distanceToLandingX.</summary>
    bool ComputeJumpStraightFromBelowFootprint(float dyPlayer)
    {
        if (dyPlayer < underJumpMinDy || dyPlayer > underJumpMaxDy) return false;
        return IsHorizontallyUnderPlatformCollider();
    }

    float EffectiveUnderPlayerJumpMaxX()
    {
        if (!TryFindPlatformColliderUnderPlayer(out Collider2D plat))
            return underPlayerJumpMaxX;
        if (plat.bounds.size.x > narrowClimbPlatformWidth)
            return underPlayerJumpMaxX;
        return Mathf.Max(
            underPlayerJumpMaxX,
            plat.bounds.extents.x + GetEnemyColliderHalfWidth() + narrowPlatUnderJumpExtraX);
    }

    bool StraightUnderJumpGeometryOk(float dyPlayer, float distanceToAimX, float aimWorldX)
    {
        if (distanceToAimX > EffectiveUnderPlayerJumpMaxX()) return false;
        if (dyPlayer < underJumpMinDy || dyPlayer > underJumpMaxDy) return false;
        return HasJumpableLedgeUnderTarget(aimWorldX) || HasFeetOverlapPlatformForJump();
    }

    /// <summary>Луч вверх: есть коллайдер-платформа под ногами игрока, на которую можно запрыгнуть снизу.</summary>
    bool HasJumpableLedgeUnderTarget(float aimWorldX)
    {
        if (target == null) return false;
        float sampleX = Mathf.Lerp(rb.position.x, aimWorldX, 0.65f);
        float originY = rb.position.y + 0.12f;
        float playerSurfaceY = target.position.y + underJumpTargetFeetYOffset;
        float dist = playerSurfaceY + 0.35f - originY;
        if (dist <= 0.06f) return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(sampleX, originY), Vector2.up, dist, GetEffectivePlatformMask());
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Transform playerRoot = target.root;
        Transform selfRoot = transform.root;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == selfRoot || c.transform.root == playerRoot) continue;
            float topY = c.bounds.max.y;
            if (topY > rb.position.y + 0.05f && topY <= playerSurfaceY + 0.55f)
                return true;
        }

        return false;
    }

    float GetEnemyColliderHalfWidth()
    {
        if (bodyCollider != null)
            return Mathf.Max(0.06f, bodyCollider.bounds.extents.x);
        return 0.4f;
    }

    float GetRequiredJumpEdgeGap()
    {
        float fullWidth = GetEnemyColliderHalfWidth() * 2f;
        return fullWidth * Mathf.Max(1.02f, platformJumpStandoffBodyMultiplier) +
            Mathf.Max(0f, platformJumpStandoffExtraWorldUnits);
    }

    bool HorizontalGapToPlatformSufficient(Bounds platformBounds)
    {
        float hw = GetEnemyColliderHalfWidth();
        float minGap = GetRequiredJumpEdgeGap();
        float ex = rb.position.x;
        float leftEdge = ex - hw;
        float rightEdge = ex + hw;
        float pl = platformBounds.min.x;
        float pr = platformBounds.max.x;

        float slack = 0.08f;
        if (rightEdge <= pl + 0.005f)
            return (pl - rightEdge) >= minGap - slack;
        if (leftEdge >= pr - 0.005f)
            return (leftEdge - pr) >= minGap - slack;
        return false;
    }

    bool SatisfiesHorizontalJumpStandoff()
    {
        if (!platformJumpRequireHorizontalStandoff)
            return true;
        if (!TryFindPlatformColliderUnderPlayer(out Collider2D plat))
            return true;
        return HorizontalGapToPlatformSufficient(plat.bounds);
    }

    float AdjustSteerXForJumpRunUpStandoff(float steerX, Bounds pb)
    {
        float hw = GetEnemyColliderHalfWidth();
        float minGap = GetRequiredJumpEdgeGap();
        float ex = rb.position.x;
        float leftEdge = ex - hw;
        float rightEdge = ex + hw;
        float pl = pb.min.x;
        float pr = pb.max.x;

        float leftStandCenterX = pl - hw - minGap;
        float rightStandCenterX = pr + hw + minGap;

        if (HorizontalGapToPlatformSufficient(pb))
            return steerX;

        if (rightEdge <= pl + 0.02f)
            return leftStandCenterX;
        if (leftEdge >= pr - 0.02f)
            return rightStandCenterX;

        return Mathf.Abs(ex - leftStandCenterX) <= Mathf.Abs(ex - rightStandCenterX)
            ? leftStandCenterX
            : rightStandCenterX;
    }

    bool IsConsideredGroundedForJump()
    {
        if (groundSensor != null)
            return groundSensor.State();
        return Mathf.Abs(rb.linearVelocity.y) < 0.2f;
    }

    bool TryPlatformJumpTowardPlayer(float dyPlayer, float distanceX, bool climbIntent, bool straightUnderGeometryOk)
    {
        lastPlatformJumpWasSideArc = false;
        if (playerAboveSince < 0f) return false;
        if (Time.time < nextPlatformJumpTime) return false;
        if (Time.time < dodgeLockUntil) return false;
        if (dyPlayer <= playerAboveMinHeight) return false;
        if (!PlatformJumpPlayerSurfaceOk())
            return false;

        bool grounded = IsConsideredGroundedForJump();
        if (!grounded) return false;

        if (platformJumpRequireHorizontalStandoff &&
            TryFindPlatformColliderUnderPlayer(out Collider2D platForJump) &&
            !HorizontalGapToPlatformSufficient(platForJump.bounds))
            return false;

        float persistRequired = playerAbovePersistSeconds;
        if (straightUnderGeometryOk &&
            (distanceX <= EffectiveUnderPlayerJumpMaxX() || ComputeJumpStraightFromBelowFootprint(dyPlayer)))
            persistRequired = Mathf.Min(persistRequired, underPlayerAlignedPersistSeconds);

        bool relaxedHigh = dyPlayer >= playerAboveMinHeight + relaxedHighJumpExtraDy;
        if (relaxedHigh)
            persistRequired = Mathf.Min(persistRequired, relaxedHighJumpMinPersistSeconds);

        if (Time.time - playerAboveSince < persistRequired)
            return false;

        if (straightUnderGeometryOk)
        {
            lastJumpWasStraightUnder = true;
            nextPlatformJumpTime = Time.time + platformJumpCooldown;
            if (groundSensor != null)
                groundSensor.Disable(groundSensorJumpDisable);
            if (animator != null)
                animator.SetTrigger("Jump");
            return true;
        }

        // Боковой/дуговой прыжок — только когда игрок реально «наверху» (climbIntent), иначе ложные прыжки на ровном месте.
        if (!climbIntent)
            return false;

        bool fromDetour = climbIntent && hasDetourChoice && ReachedDetourJumpSpot();
        float maxHoriz = fromDetour ? detourJumpMaxHorizontal : sideJumpMaxHorizontal;
        if (climbIntent && !HasClearVerticalChannelToPlayer())
            maxHoriz = Mathf.Max(maxHoriz, detourJumpMaxHorizontal);
        float minHoriz = sideJumpMinHorizontal;
        if (relaxedHigh)
        {
            if (!TryFindPlatformColliderUnderPlayer(out _))
                return false;
            minHoriz = Mathf.Min(minHoriz, relaxedHighJumpMinHorizontal);
            maxHoriz = Mathf.Max(maxHoriz, relaxedHighJumpMaxHorizontal);
        }

        bool almostUnderPlayerVertically = platformJumpAllowWhenVerticalPathBlocked &&
            dyPlayer > playerAboveMinHeight + 0.04f &&
            distanceX <= minHoriz + 0.02f;
        if (!almostUnderPlayerVertically && (distanceX < minHoriz || distanceX > maxHoriz))
            return false;

        bool verticalClear = HasClearVerticalChannelToPlayer();
        bool overhang = PlatformOverhangBlocksPathToPlayer();
        if (verticalClear && !overhang && dyPlayer < playerAboveMinHeight + sideJumpRequireExtraDyWhenOpen)
            return false;
        if (!verticalClear &&
            !(platformJumpAllowWhenVerticalPathBlocked && dyPlayer > playerAboveMinHeight + 0.08f))
            return false;
        if (fromDetour && PlatformOverhangBlocksPathToPlayer()) return false;

        lastPlatformJumpWasSideArc = true;
        nextPlatformJumpTime = Time.time + platformJumpCooldown;
        if (groundSensor != null)
            groundSensor.Disable(groundSensorJumpDisable);
        if (animator != null)
            animator.SetTrigger("Jump");
        return true;
    }

    /// <summary>Есть ли коллайдер платформы на отрезке по X от врага к игроку (на разных высотах тела).</summary>
    bool PlatformOverhangBlocksPathToPlayer()
    {
        if (target == null) return false;
        float ex = rb.position.x;
        float px = target.position.x;
        float dx = px - ex;
        float dist = Mathf.Abs(dx) - platformHorizProbeMargin;
        if (dist <= 0.03f) return false;
        Vector2 dir = new Vector2(Mathf.Sign(dx), 0f);

        float[] probeYs = { 0.12f, 0.32f, 0.55f, 0.82f, 1.08f };
        for (int i = 0; i < probeYs.Length; i++)
        {
            float y = rb.position.y + probeYs[i];
            if (y > target.position.y + 0.15f) continue;
            Vector2 origin = new Vector2(ex, y);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, dist, GetEffectivePlatformMask());
            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D c = hits[h].collider;
                if (c == null || c.isTrigger) continue;
                if (c.transform.root == transform.root) continue;
                if (c.transform.root == target.root) continue;
                return true;
            }
        }
        return false;
    }

    LayerMask GetEffectivePlatformMask()
    {
        LayerMask m = platformBlockingLayers.value != 0
            ? platformBlockingLayers
            : (LayerMask)Physics2D.DefaultRaycastLayers;
        if (platformDetectionIgnoreLayers.value != 0)
            m = (LayerMask)(m.value & ~platformDetectionIgnoreLayers.value);
        return m;
    }

    LayerMask EffectiveAirCollisionMask =>
        airCollisionLayers.value != 0
            ? airCollisionLayers
            : (platformBlockingLayers.value != 0 ? platformBlockingLayers : (LayerMask)Physics2D.DefaultRaycastLayers);

    bool AirHorizontalCastBlocks(float signX)
    {
        if (bodyCollider == null || Mathf.Abs(signX) < 0.01f) return false;
        Vector2 dir = new Vector2(Mathf.Sign(signX), 0f);
        int n = bodyCollider.Cast(dir, airContactFilter, castHits, airWallCastDistance);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = castHits[i].collider;
            if (c == null) continue;
            if (c.transform.root == transform.root) continue;
            return true;
        }
        return false;
    }

    void PickDetourSide()
    {
        if (target != null && TryFindPlatformColliderUnderPlayer(out Collider2D plat))
        {
            Bounds b = plat.bounds;
            float pl = b.min.x;
            float pr = b.max.x;
            float ex = rb.position.x;
            float px = target.position.x;
            float mid = (pl + pr) * 0.5f;
            float pad = Mathf.Max(0.28f, GetEnemyColliderHalfWidth() + 0.16f);
            float leftMark = pl - pad;
            float rightMark = pr + pad;
            float costLeft = Mathf.Abs(ex - leftMark);
            float costRight = Mathf.Abs(ex - rightMark);
            if (costRight < costLeft - 0.06f)
            {
                detourSign = 1;
                flankDir = 1;
            }
            else if (costLeft < costRight - 0.06f)
            {
                detourSign = -1;
                flankDir = -1;
            }
            else
            {
                detourSign = px >= mid ? 1 : -1;
                flankDir = detourSign;
            }
            return;
        }

        PickDetourSideRayFallback();
    }

    void PickDetourSideRayFallback()
    {
        Vector2 o = rb.position + Vector2.up * 0.28f;
        float dl = FirstSolidDistanceAlongRay(o, Vector2.left, detourSideProbeDistance);
        float dr = FirstSolidDistanceAlongRay(o, Vector2.right, detourSideProbeDistance);
        if (Mathf.Abs(dl - dr) < 0.12f)
            detourSign = flankDir;
        else
            detourSign = dl >= dr ? -1 : 1;
    }

    float FirstSolidDistanceAlongRay(Vector2 origin, Vector2 dir, float maxDist)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxDist, GetEffectivePlatformMask());
        float best = maxDist;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == transform.root) continue;
            if (target != null && c.transform.root == target.root) continue;
            best = Mathf.Min(best, hits[i].distance);
        }
        return best;
    }

    bool IsSideBlocked(Vector2 dirX)
    {
        if (dirX.x == 0f) return false;
        Vector2 origin = rb.position + Vector2.up * 0.22f;
        float dist = 0.22f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dirX.normalized, dist, GetEffectivePlatformMask());
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == transform.root) continue;
            return true;
        }
        return false;
    }

    bool HasClearVerticalChannelToPlayer()
    {
        Vector2 from = new Vector2(rb.position.x, rb.position.y + 0.12f);
        float topY = target.position.y - 0.08f;
        float dist = topY - from.y;
        if (dist <= 0.05f) return true;
        RaycastHit2D[] hits = Physics2D.RaycastAll(from, Vector2.up, dist, GetEffectivePlatformMask());
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.root == transform.root) continue;
            if (c.transform.root == target.root) continue;
            return false;
        }
        return true;
    }

    /// <summary>Вызывается при атаке ближнего боя игрока: с шансом отшагивает назад от рыцаря.</summary>
    public void OnPlayerMeleeAttack(Vector2 attackerWorldPosition)
    {
        if (dodgeChance <= 0f) return;
        if (Time.time < dodgeLockUntil) return;
        if (UnityEngine.Random.value > dodgeChance) return;

        Vector2 self = rb.position;
        Vector2 away = self - attackerWorldPosition;
        if (away.sqrMagnitude < 0.0001f)
        {
            float face = Mathf.Sign(-transform.localScale.x);
            if (Mathf.Abs(face) < 0.01f) face = 1f;
            away = new Vector2(face, 0f);
        }
        else
        {
            away.Normalize();
        }

        rb.MovePosition(self + away * dodgeInstantStep);
        rb.linearVelocity = new Vector2(away.x * dodgeHorizontalSpeed, rb.linearVelocity.y);
        dodgeLockUntil = Time.time + dodgeControlLockDuration;
    }

    /// <summary>Вызывается, когда этот враг попал по игроку или по активному щиту.</summary>
    public void RegisterHitImpactSlowdown()
    {
        hitImpactSlowUntil = Time.time + hitImpactSlowDuration;
    }

    /// <summary>После удара в поднятый щит: короткая «заморозка» ближних ударов.</summary>
    public void RegisterShieldBlockMeleePause()
    {
        shieldBlockMeleeLockUntil = Time.time + shieldBlockMeleePauseDuration;
    }

    /// <summary>Пока false — EnemyContactDamage не бьёт и не триггерит Attack (после прорыва щита или удара по щиту).</summary>
    public bool CanDealContactMelee()
    {
        return Time.time >= criticalShieldBreakMeleeUntil && Time.time >= shieldBlockMeleeLockUntil;
    }

    /// <summary>После критического удара: столько же времени, сколько замедление ходьбы.</summary>
    public void RegisterCriticalShieldBreakMeleePause()
    {
        criticalShieldBreakMeleeUntil = Time.time + hitImpactSlowDuration;
    }
}