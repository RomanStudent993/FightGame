using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Интро боя (Level 1–3): камера на игроке → удар → враг → удар → отъезд → 3-2-1 → бой.</summary>
[DefaultExecutionOrder(-200)]
public class BattleIntroController : MonoBehaviour
{
    public static bool FightStarted { get; private set; } = true;

    [Header("Камера — крупный план")]
    [SerializeField] float closeOrthoSize = 1.35f;
    [SerializeField] float closeFocusHeightFromFeet = 0.82f;
    [SerializeField] float enemySnapDuration = 0.22f;
    [SerializeField] float pullBackDuration = 1.1f;

    [Header("Интро — паузы на персонажах")]
    [SerializeField] float playerIntroAttackMinHold = 0.28f;
    [SerializeField] float holdAfterPlayerAttack = 0.32f;
    [SerializeField] float holdAfterEnemyAttack = 0.38f;

    [Header("Обратный отсчёт")]
    [SerializeField] float countdownStepDuration = 0.65f;
    [SerializeField] int countdownFontSize = 96;

    [Header("Интро — атака врага")]
    [Tooltip("Множитель скорости аниматора во время удара врага в катсцене.")]
    [SerializeField] float enemyIntroAttackAnimSpeed = 1f;
    [Tooltip("На какой доле клипа Attack считать удар показанным (маг: раньше 0.95, до idle-хвоста).")]
    [SerializeField] [Range(0.2f, 1f)] float enemyIntroAttackExitNormalized = 0.95f;
    [Tooltip("Минимальное время показа удара врага в катсцене.")]
    [SerializeField] float enemyIntroAttackMinHold = 0f;

    [Header("Финальная камера")]
    [SerializeField] Vector3 finalCameraPosition = new Vector3(0f, 4.07f, -10f);
    [SerializeField] float finalOrthoSize = 5f;

    static readonly Color CountdownColor = Color.black;

    Camera _camera;
    Transform _player;
    Transform _enemy;
    Animator _playerAnimator;
    Animator _enemyAnimator;
    HeroKnight _heroKnight;
    EnemyAI _enemyAi;
    EnemyContactDamage _enemyContact;
    EvilWizardRangedCombat _enemyRanged;
    bool _enemyContactWasEnabled;
    PlayerAttackDamage _playerAttack;
    PlayerShieldDefense _playerShield;
    PlayerCombatKnockback _playerKnockback;
    Rigidbody2D _playerRb;
    Rigidbody2D _enemyRb;
    RigidbodyConstraints2D _playerConstraints;
    RigidbodyConstraints2D _enemyConstraints;
    Canvas _countdownCanvas;
    Text _countdownText;
    bool _introRunning;
    bool _combatLocked;
    bool _finalCameraCaptured;
    Coroutine _introRoutine;
    readonly List<(Collider2D a, Collider2D b)> _ignoredCollisions = new List<(Collider2D, Collider2D)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState() => FightStarted = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHooks()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureOnBattleScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsFightIntroScene(scene))
            FightStarted = false;
    }

    public static void ResetForLevelRestart()
    {
        FightStarted = false;
    }

    static void EnsureOnBattleScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (!IsFightIntroScene(scene))
            return;

        if (Object.FindAnyObjectByType<BattleIntroController>(FindObjectsInactive.Include) != null)
            return;

        new GameObject("BattleIntroController").AddComponent<BattleIntroController>();
    }

    void Awake()
    {
        FightStarted = false;
        EarlyLockCombatInScene();
    }

    void Start()
    {
        if (!IsFightIntroScene())
        {
            BeginFight();
            return;
        }

        CaptureFinalCameraFromScene();
        BuildCountdownUi();
        _introRoutine = StartCoroutine(IntroRoutine());
    }

    void LateUpdate()
    {
        if (!_introRunning || _camera == null)
            return;

        _camera.transform.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, finalCameraPosition.z);
    }

    void OnDestroy()
    {
        _introRunning = false;
        RestoreCombatCollisions();
    }

    static bool IsFightIntroScene()
    {
        return IsFightIntroScene(SceneManager.GetActiveScene());
    }

    static bool IsFightIntroScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        string name = scene.name;
        string path = scene.path.Replace('\\', '/');
        return name == "battle" || name == "Level-2" || name == "Level-3"
            || name.Contains("battle", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/battle.unity", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Level-2.unity", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Level-3.unity", System.StringComparison.OrdinalIgnoreCase);
    }

    void EarlyLockCombatInScene()
    {
        foreach (HeroKnight hero in Object.FindObjectsByType<HeroKnight>(FindObjectsSortMode.None))
            hero.enabled = false;

        foreach (EnemyAI ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            ai.enabled = false;
            EnemyContactDamage contact = ai.GetComponent<EnemyContactDamage>();
            if (contact != null)
                contact.enabled = false;
        }

        foreach (PlayerAttackDamage atk in Object.FindObjectsByType<PlayerAttackDamage>(FindObjectsSortMode.None))
            atk.enabled = false;

        foreach (PlayerShieldDefense shield in Object.FindObjectsByType<PlayerShieldDefense>(FindObjectsSortMode.None))
            shield.enabled = false;

        foreach (EvilWizardRangedCombat ranged in Object.FindObjectsByType<EvilWizardRangedCombat>(FindObjectsSortMode.None))
            ranged.enabled = false;
    }

    void CaptureFinalCameraFromScene()
    {
        if (_finalCameraCaptured)
            return;

        Camera cam = FindBattleCamera();
        if (cam == null)
            return;

        _finalCameraCaptured = true;
        finalCameraPosition = cam.transform.position;
        finalOrthoSize = cam.orthographicSize;
    }

    IEnumerator IntroRoutine()
    {
        const int maxAttempts = 60;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (TryResolveRefs())
                break;
            yield return null;
        }

        if (_camera == null || _player == null || _enemy == null)
        {
            Debug.LogWarning("BattleIntroController: не найдены камера/игрок/враг — интро пропущено.");
            BeginFight();
            yield break;
        }

        LockCombat();
        IgnoreCombatCollisions();
        _introRunning = true;

        SetCloseCameraOn(_player);
        yield return PlayIntroAttack(_playerAnimator, "Attack1", "Attack1", minHoldSeconds: playerIntroAttackMinHold);

        if (holdAfterPlayerAttack > 0f)
            yield return new WaitForSecondsRealtime(holdAfterPlayerAttack);

        yield return MoveCameraCloseTo(_enemy, enemySnapDuration);
        yield return PlayIntroAttack(
            _enemyAnimator,
            "Attack",
            "Attack",
            animSpeedMultiplier: enemyIntroAttackAnimSpeed,
            exitAtNormalized: enemyIntroAttackExitNormalized,
            minHoldSeconds: enemyIntroAttackMinHold);

        if (holdAfterEnemyAttack > 0f)
            yield return new WaitForSecondsRealtime(holdAfterEnemyAttack);

        yield return PullBackCamera();

        if (_countdownCanvas != null)
            _countdownCanvas.gameObject.SetActive(true);

        for (int n = 3; n >= 1; n--)
        {
            if (_countdownText != null)
                _countdownText.text = n.ToString();
            yield return new WaitForSecondsRealtime(countdownStepDuration);
        }

        _introRunning = false;
        BeginFight();
    }

    bool TryResolveRefs()
    {
        _camera = FindBattleCamera();
        if (_camera == null)
            return false;

        GameObject playerGo = FindPlayer();
        GameObject enemyGo = FindEnemy();
        if (playerGo == null || enemyGo == null)
            return false;

        _player = playerGo.transform;
        _enemy = enemyGo.transform;
        _playerAnimator = playerGo.GetComponent<Animator>();
        _enemyAnimator = enemyGo.GetComponent<Animator>();
        _heroKnight = playerGo.GetComponent<HeroKnight>();
        _enemyAi = enemyGo.GetComponent<EnemyAI>();
        _enemyContact = enemyGo.GetComponent<EnemyContactDamage>();
        _enemyRanged = enemyGo.GetComponent<EvilWizardRangedCombat>();
        _playerAttack = playerGo.GetComponent<PlayerAttackDamage>();
        _playerShield = playerGo.GetComponent<PlayerShieldDefense>();
        _playerKnockback = playerGo.GetComponent<PlayerCombatKnockback>();
        _playerRb = playerGo.GetComponent<Rigidbody2D>();
        _enemyRb = enemyGo.GetComponent<Rigidbody2D>();
        return true;
    }

    static Camera FindBattleCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].CompareTag("MainCamera"))
                return cameras[i];
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    static GameObject FindPlayer()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
            return playerGo;

        HeroKnight hero = Object.FindAnyObjectByType<HeroKnight>();
        return hero != null ? hero.gameObject : null;
    }

    static GameObject FindEnemy()
    {
        GameObject enemyGo = GameObject.FindGameObjectWithTag("Enemy");
        if (enemyGo != null)
            return enemyGo;

        EnemyAI ai = Object.FindAnyObjectByType<EnemyAI>();
        return ai != null ? ai.gameObject : null;
    }

    void LockCombat()
    {
        if (_combatLocked)
            return;

        _combatLocked = true;

        if (_heroKnight != null) _heroKnight.enabled = false;
        if (_enemyAi != null) _enemyAi.enabled = false;
        if (_enemyContact != null)
        {
            _enemyContactWasEnabled = _enemyContact.enabled;
            _enemyContact.enabled = false;
        }

        if (_enemyRanged != null)
            _enemyRanged.enabled = false;

        if (_playerAttack != null) _playerAttack.enabled = false;
        if (_playerShield != null) _playerShield.enabled = false;
        if (_playerKnockback != null) _playerKnockback.enabled = false;

        if (_playerRb != null)
        {
            _playerConstraints = _playerRb.constraints;
            _playerRb.linearVelocity = Vector2.zero;
            _playerRb.angularVelocity = 0f;
            _playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (_enemyRb != null)
        {
            _enemyConstraints = _enemyRb.constraints;
            _enemyRb.linearVelocity = Vector2.zero;
            _enemyRb.angularVelocity = 0f;
            _enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void UnlockCombat()
    {
        if (_playerKnockback != null)
        {
            _playerKnockback.enabled = true;
            _playerKnockback.ClearInputLock();
        }

        if (_heroKnight != null) _heroKnight.enabled = true;
        if (_enemyAi != null) _enemyAi.enabled = true;
        if (_enemyRanged != null)
            _enemyRanged.enabled = true;
        if (_enemyContact != null && _enemyContactWasEnabled)
            _enemyContact.enabled = true;
        if (_playerAttack != null) _playerAttack.enabled = true;
        if (_playerShield != null) _playerShield.enabled = true;

        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector2.zero;
            _playerRb.angularVelocity = 0f;
            _playerRb.constraints = _playerConstraints;
        }

        if (_enemyRb != null)
        {
            _enemyRb.linearVelocity = Vector2.zero;
            _enemyRb.angularVelocity = 0f;
            _enemyRb.constraints = _enemyConstraints;
        }

        RestoreCombatCollisions();
        _combatLocked = false;
    }

    void IgnoreCombatCollisions()
    {
        if (_player == null || _enemy == null)
            return;

        Collider2D[] playerCols = _player.GetComponentsInChildren<Collider2D>();
        Collider2D[] enemyCols = _enemy.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < playerCols.Length; i++)
        {
            Collider2D a = playerCols[i];
            if (a == null)
                continue;

            for (int j = 0; j < enemyCols.Length; j++)
            {
                Collider2D b = enemyCols[j];
                if (b == null)
                    continue;

                Physics2D.IgnoreCollision(a, b, true);
                _ignoredCollisions.Add((a, b));
            }
        }
    }

    void RestoreCombatCollisions()
    {
        for (int i = 0; i < _ignoredCollisions.Count; i++)
        {
            Collider2D a = _ignoredCollisions[i].a;
            Collider2D b = _ignoredCollisions[i].b;
            if (a != null && b != null)
                Physics2D.IgnoreCollision(a, b, false);
        }

        _ignoredCollisions.Clear();
    }

    void BuildCountdownUi()
    {
        if (_countdownCanvas != null)
            return;

        GameObject go = new GameObject("BattleCountdown", typeof(RectTransform));
        _countdownCanvas = go.AddComponent<Canvas>();
        _countdownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _countdownCanvas.sortingOrder = 450;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("CountdownText", typeof(RectTransform));
        textGo.transform.SetParent(root, false);
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _countdownText = textGo.AddComponent<Text>();
        Font font = GameFont.ResolveForText("321", countdownFontSize, FontStyle.Bold);
        _countdownText.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _countdownText.fontSize = countdownFontSize;
        _countdownText.fontStyle = FontStyle.Bold;
        _countdownText.color = CountdownColor;
        _countdownText.alignment = TextAnchor.MiddleCenter;
        _countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _countdownText.verticalOverflow = VerticalWrapMode.Overflow;
        _countdownText.raycastTarget = false;
        _countdownText.text = "";

        go.SetActive(false);
    }

    void SetCloseCameraOn(Transform target)
    {
        if (_camera == null || target == null)
            return;

        Vector3 focus = GetFocusPoint(target);
        _camera.transform.position = new Vector3(focus.x, focus.y, finalCameraPosition.z);
        _camera.orthographicSize = closeOrthoSize;
    }

    IEnumerator MoveCameraCloseTo(Transform target, float duration)
    {
        Vector3 fromPos = _camera.transform.position;
        float fromSize = _camera.orthographicSize;
        Vector3 focus = GetFocusPoint(target);
        Vector3 toPos = new Vector3(focus.x, focus.y, finalCameraPosition.z);

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _camera.transform.position = Vector3.Lerp(fromPos, toPos, k);
            _camera.orthographicSize = Mathf.Lerp(fromSize, closeOrthoSize, k);
            yield return null;
        }

        SetCloseCameraOn(target);
    }

    IEnumerator PullBackCamera()
    {
        Vector3 fromPos = _camera.transform.position;
        float fromSize = _camera.orthographicSize;
        float t = 0f;
        float duration = Mathf.Max(0.05f, pullBackDuration);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _camera.transform.position = Vector3.Lerp(fromPos, finalCameraPosition, k);
            _camera.orthographicSize = Mathf.Lerp(fromSize, finalOrthoSize, k);
            yield return null;
        }

        _camera.transform.position = finalCameraPosition;
        _camera.orthographicSize = finalOrthoSize;
    }

    IEnumerator PlayIntroAttack(
        Animator animator,
        string triggerName,
        string stateName,
        int layer = 0,
        float animSpeedMultiplier = 1f,
        float exitAtNormalized = 0.95f,
        float minHoldSeconds = 0f)
    {
        if (animator == null)
            yield break;

        float prevSpeed = animator.speed;
        if (animSpeedMultiplier > 0f)
            animator.speed = prevSpeed * animSpeedMultiplier;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);

        const float timeout = 4f;
        float elapsed = 0f;
        float stateElapsed = 0f;
        bool enteredState = false;
        exitAtNormalized = Mathf.Clamp(exitAtNormalized, 0.2f, 1f);

        try
        {
            while (elapsed < timeout)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.IsName(stateName))
                    enteredState = true;

                if (animator.IsInTransition(layer))
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                    if (next.IsName(stateName))
                        enteredState = true;
                }

                if (enteredState)
                {
                    stateElapsed += Time.unscaledDeltaTime;
                    state = animator.GetCurrentAnimatorStateInfo(layer);
                    bool leftAttackState = !state.IsName(stateName) && !animator.IsInTransition(layer);
                    bool hitExitPoint = state.IsName(stateName) && state.normalizedTime >= exitAtNormalized;
                    if (stateElapsed >= minHoldSeconds && (hitExitPoint || leftAttackState))
                        yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            animator.speed = prevSpeed;
        }
    }

    Vector3 GetFocusPoint(Transform target)
    {
        Collider2D col = target.GetComponent<Collider2D>();
        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(b.center.x, b.min.y + closeFocusHeightFromFeet, 0f);
        }

        Vector3 p = target.position;
        p.y += closeFocusHeightFromFeet;
        return p;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    void BeginFight()
    {
        if (FightStarted)
            return;

        FightStarted = true;
        _introRunning = false;
        UnlockCombat();

        if (_countdownCanvas != null)
            _countdownCanvas.gameObject.SetActive(false);

        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }
    }
}
