using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Пошаговое обучение в EducationDemo: ходьба → прыжки → блок → кувырок → удары по чучелу.
/// HUD в правом верхнем углу, заголовок «Обучение», прогресс 0/3 … 3/3.
/// </summary>
public class TutorialQuestController : MonoBehaviour
{
    enum Step
    {
        Walk,
        Jump,
        Block,
        Roll,
        Attack,
        Done
    }

    const int TargetCount = 3;

    [SerializeField] Vector2 anchorFromTopRight = new Vector2(-16f, -16f);
    [SerializeField] Vector2 panelSize = new Vector2(380f, 96f);
    [SerializeField] int subtitleFontSize = 28;
    [SerializeField] int taskFontSize = 18;
    [SerializeField] float walkSegmentDistance = 0.7f;
    [SerializeField] float blockInputCooldown = 0.25f;
    [SerializeField] float rollInputCooldown = 0.35f;
    [Tooltip("Счётчик блока (0/3) виден только пока игрок в кадре Main Camera.")]
    [SerializeField] float cameraVisibilityPadding = 0.05f;
    [Header("Переход после обучения")]
    [SerializeField] string nextSceneName = "battle";
    [SerializeField] string nextScenePath = "Assets/FightGame/prefabs/Level-1/Scenes/battle.unity";
    [SerializeField] Sprite loadingBackground;
    [SerializeField] float delayBeforeFade = 1.2f;
    [SerializeField] float fadeDuration = 0.35f;

    static readonly Color YellowSubtitle = new Color(0.95f, 0.82f, 0.2f, 1f);
    static readonly Color YellowTask = new Color(1f, 0.92f, 0.35f, 1f);

    Step _step = Step.Walk;
    int _progress;
    float _walkDistanceAccum;
    int _walkMilestones;
    bool _grounded = true;
    bool _wasGrounded = true;
    float _lastBlockCountTime = -10f;
    float _lastRollCountTime = -10f;

    Transform _player;
    Camera _mainCamera;
    Sensor_HeroKnight _groundSensor;
    Text _subtitleUi;
    Text _taskUi;
    Canvas _transitionCanvas;
    Image _transitionBlack;
    Image _transitionLoadingImage;
    Text _continueUi;
    ScarecrowHitReaction _scarecrow;
    bool _lastBlockCounterVisible;
    bool _endingStarted;

    void Start()
    {
        FindPlayerRefs();
        if (_scarecrow != null)
            _scarecrow.AcceptHits = false;
        BuildHud();
        ScarecrowHitReaction.HitCountChanged += OnScarecrowHit;
        RefreshHud();
    }

    void OnDestroy()
    {
        ScarecrowHitReaction.HitCountChanged -= OnScarecrowHit;
    }

    void FindPlayerRefs()
    {
        HeroKnight hero = FindAnyObjectByType<HeroKnight>();
        if (hero != null)
        {
            _player = hero.transform;
            Transform sensorT = _player.Find("GroundSensor");
            if (sensorT != null)
                _groundSensor = sensorT.GetComponent<Sensor_HeroKnight>();
        }

        _scarecrow = FindAnyObjectByType<ScarecrowHitReaction>();
        _mainCamera = Camera.main;

        if (_player != null)
            _lastWalkX = _player.position.x;
    }

    void Update()
    {
        if (_endingStarted) return;
        if (_step == Step.Done) return;

        UpdateGrounded();
        switch (_step)
        {
            case Step.Walk:
                TickWalk();
                break;
            case Step.Jump:
                TickJump();
                break;
            case Step.Block:
                TickBlock();
                RefreshHudIfBlockCounterVisibilityChanged();
                break;
            case Step.Roll:
                TickRoll();
                break;
        }
    }

    void RefreshHudIfBlockCounterVisibilityChanged()
    {
        bool visible = ShouldShowBlockCounterInHud();
        if (visible == _lastBlockCounterVisible) return;
        _lastBlockCounterVisible = visible;
        RefreshHud();
    }

    bool ShouldShowBlockCounterInHud()
    {
        return true;
    }

    bool IsPlayerVisibleInMainCamera()
    {
        if (_player == null) return true;
        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera == null) return true;

        Bounds bounds = GetPlayerVisibilityBounds();
        Vector3 min = _mainCamera.WorldToViewportPoint(bounds.min);
        Vector3 max = _mainCamera.WorldToViewportPoint(bounds.max);
        if (min.z < 0f && max.z < 0f) return false;

        float pad = Mathf.Max(0f, cameraVisibilityPadding);
        float x0 = Mathf.Min(min.x, max.x);
        float x1 = Mathf.Max(min.x, max.x);
        float y0 = Mathf.Min(min.y, max.y);
        float y1 = Mathf.Max(min.y, max.y);
        return x1 >= pad && x0 <= 1f - pad && y1 >= pad && y0 <= 1f - pad;
    }

    Bounds GetPlayerVisibilityBounds()
    {
        Collider2D col = _player.GetComponent<Collider2D>();
        if (col != null) return col.bounds;

        SpriteRenderer sr = _player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds;

        return new Bounds(_player.position, Vector3.one * 0.5f);
    }

    void UpdateGrounded()
    {
        _wasGrounded = _grounded;
        if (_groundSensor != null)
            _grounded = _groundSensor.State();
        else if (_player != null)
        {
            Rigidbody2D rb = _player.GetComponent<Rigidbody2D>();
            _grounded = rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.08f;
        }
    }

    void TickWalk()
    {
        if (_player == null) return;

        bool moveInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f
                         || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D)
                         || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S);
        if (!moveInput) return;

        float dx = Mathf.Abs(_player.position.x - (_lastWalkX));
        _lastWalkX = _player.position.x;
        if (dx < 0.0001f) return;

        _walkDistanceAccum += dx;
        while (_walkMilestones < TargetCount
               && _walkDistanceAccum >= walkSegmentDistance * (_walkMilestones + 1))
        {
            _walkMilestones++;
            _progress = _walkMilestones;
            RefreshHud();
            if (_progress >= TargetCount)
                AdvanceStep();
        }
    }

    float _lastWalkX;

    void TickJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (!_wasGrounded) return;

        RegisterProgress();
    }

    void TickBlock()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        if (Time.time - _lastBlockCountTime < blockInputCooldown) return;

        _lastBlockCountTime = Time.time;
        RegisterProgress();
    }

    void TickRoll()
    {
        if (!WasShiftPressedThisFrame()) return;
        if (!_grounded) return;
        if (Time.time - _lastRollCountTime < rollInputCooldown) return;

        _lastRollCountTime = Time.time;
        RegisterProgress();
    }

    static bool WasShiftPressedThisFrame()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            return true;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null
            && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame))
            return true;
#endif
        return false;
    }

    void OnScarecrowHit(ScarecrowHitReaction scarecrow, int hitCount)
    {
        if (_step != Step.Attack) return;
        if (_scarecrow != null && scarecrow != _scarecrow) return;

        _progress = Mathf.Clamp(hitCount, 0, TargetCount);
        RefreshHud();
        if (_progress >= TargetCount)
            AdvanceStep();
    }

    void RegisterProgress()
    {
        int target = GetTargetCount(_step);
        if (_progress >= target) return;

        _progress++;
        RefreshHud();
        if (_progress >= target)
            AdvanceStep();
    }

    void AdvanceStep()
    {
        _progress = 0;
        _walkDistanceAccum = 0f;
        _walkMilestones = 0;
        if (_player != null)
            _lastWalkX = _player.position.x;

        switch (_step)
        {
            case Step.Walk:
                _step = Step.Jump;
                break;
            case Step.Jump:
                _step = Step.Block;
                _lastBlockCounterVisible = false;
                break;
            case Step.Block:
                _step = Step.Roll;
                _lastBlockCounterVisible = false;
                break;
            case Step.Roll:
                _step = Step.Attack;
                if (_scarecrow != null)
                {
                    _scarecrow.ResetHits();
                    _scarecrow.AcceptHits = true;
                }
                break;
            case Step.Attack:
                _step = Step.Done;
                StartEndTransition();
                break;
        }

        RefreshHud();
    }

    void RefreshHud()
    {
        if (_taskUi == null) return;

        if (_step == Step.Done)
        {
            _taskUi.text = "Обучение пройдено!";
            return;
        }

        string label = GetTaskLabel(_step);
        bool showCounter = ShouldShowBlockCounterInHud();
        _lastBlockCounterVisible = showCounter;
        int target = GetTargetCount(_step);
        _taskUi.text = showCounter
            ? $"{label} - {_progress}/{target}"
            : label;
    }

    static int GetTargetCount(Step step)
    {
        return TargetCount;
    }

    static string GetTaskLabel(Step step)
    {
        switch (step)
        {
            case Step.Walk: return "Походите (WASD)";
            case Step.Jump: return "Прыгните (Пробел)";
            case Step.Block: return "Щит (ПКМ)";
            case Step.Roll: return "Кувырок (Шифт)";
            case Step.Attack: return "Ударьте чучело (ЛКМ)";
            default: return "";
        }
    }

    public void ForceCompleteTutorial()
    {
        if (_endingStarted || _step == Step.Done)
            return;

        _step = Step.Done;
        RefreshHud();
        StartEndTransition();
    }

    void StartEndTransition()
    {
        if (_endingStarted) return;
        _endingStarted = true;
        StartCoroutine(EndTransitionRoutine());
    }

    System.Collections.IEnumerator EndTransitionRoutine()
    {
        ContinuePrompt.SetLevelTransitionActive(true);

        BuildTransitionOverlay();
        if (_transitionCanvas == null || _transitionBlack == null || _transitionLoadingImage == null)
        {
            ContinuePrompt.SetLevelTransitionActive(false);
            yield break;
        }

        _transitionCanvas.gameObject.SetActive(true);
        _transitionBlack.color = new Color(0f, 0f, 0f, 0.95f);
        _transitionLoadingImage.color = new Color(1f, 1f, 1f, 0f);
        _continueUi.gameObject.SetActive(false);
        SceneFlashSuppressor.HideGameplayStrip();

        yield return ContinuePrompt.WaitForSecondsRealtimePauseAware(Mathf.Max(0f, delayBeforeFade));

        float fadeT = Mathf.Max(0.05f, fadeDuration);
        float t = 0f;
        while (t < fadeT)
        {
            while (GamePauseController.IsPaused)
                yield return null;

            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeT);
            _transitionBlack.color = new Color(0f, 0f, 0f, 0.95f * k);
            _transitionLoadingImage.color = new Color(1f, 1f, 1f, k);
            yield return null;
        }

        _continueUi.gameObject.SetActive(true);

        yield return ContinuePrompt.WaitUntilContinuePressedPauseAware();

        ContinuePrompt.SetLevelTransitionActive(false);

        if (!TryLoadNextScene())
            Debug.LogWarning($"TutorialQuestController: scenes '{nextSceneName}' and '{nextScenePath}' are unavailable.");
    }

    void BuildTransitionOverlay()
    {
        if (_transitionCanvas != null) return;

        _transitionCanvas = ContinuePrompt.CreateTransitionCanvas("TutorialEndTransition", 1200);

        RectTransform blackRt = CreateRect(_transitionCanvas.transform, "Black", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _transitionBlack = blackRt.gameObject.AddComponent<Image>();
        _transitionBlack.color = new Color(0f, 0f, 0f, 0f);

        RectTransform imageRt = CreateRect(_transitionCanvas.transform, "LoadingImage", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _transitionLoadingImage = imageRt.gameObject.AddComponent<Image>();
        _transitionLoadingImage.sprite = loadingBackground;
        _transitionLoadingImage.preserveAspect = false;
        _transitionLoadingImage.color = new Color(1f, 1f, 1f, 0f);

        _continueUi = ContinuePrompt.CreateLabel(_transitionCanvas.transform);

        _transitionCanvas.gameObject.SetActive(false);
    }

    bool TryLoadNextScene()
    {
        SaveProgressStage nextStage = GameSaveService.GetNextStageAfterScene(SceneManager.GetActiveScene().name);
        if (nextStage != SaveProgressStage.None)
            GameSaveService.AdvanceStage(nextStage);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                return true;
            }
        }

        if (!string.IsNullOrEmpty(nextScenePath))
        {
            if (Application.CanStreamedLevelBeLoaded(nextScenePath))
            {
                SceneManager.LoadScene(nextScenePath);
                return true;
            }

            try
            {
                SceneManager.LoadScene(nextScenePath);
                return true;
            }
            catch
            {
                // ignore and report unified warning above
            }
        }

        return false;
    }

    void BuildHud()
    {
        string glyphSample = "Обучение Походите WASD Прыгните Пробел Заблокируйте щитом ПКМ Кувырок Шифт Ударьте чучело ЛКМ 0/3 пройдено!";
        GameObject canvasGo = new GameObject("TutorialQuest_HUD", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform panel = CreateRect(canvasGo.transform, "TutorialPanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            anchorFromTopRight, panelSize);

        Font font = GameFont.Default;
        GameFont.RequestGlyphs(glyphSample, subtitleFontSize, taskFontSize);

        const float subtitleBlockH = 36f;
        const float gap = 8f;
        const float taskBlockH = 34f;

        RectTransform subRt = CreateRect(panel, "Subtitle",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, -6f), new Vector2(-16f, subtitleBlockH));
        _subtitleUi = subRt.gameObject.AddComponent<Text>();
        _subtitleUi.font = font;
        _subtitleUi.fontSize = subtitleFontSize;
        _subtitleUi.fontStyle = FontStyle.Normal;
        _subtitleUi.color = YellowSubtitle;
        _subtitleUi.alignment = TextAnchor.UpperRight;
        _subtitleUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _subtitleUi.verticalOverflow = VerticalWrapMode.Truncate;
        _subtitleUi.raycastTarget = false;
        _subtitleUi.text = "Обучение";

        float taskTop = -(6f + subtitleBlockH + gap);
        RectTransform taskRt = CreateRect(panel, "Task",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, taskTop), new Vector2(-16f, taskBlockH));
        _taskUi = taskRt.gameObject.AddComponent<Text>();
        _taskUi.font = GameFont.ResolveForText(glyphSample, taskFontSize);
        _taskUi.fontSize = taskFontSize;
        _taskUi.fontStyle = FontStyle.Normal;
        _taskUi.color = YellowTask;
        _taskUi.alignment = TextAnchor.UpperRight;
        _taskUi.horizontalOverflow = HorizontalWrapMode.Overflow;
        _taskUi.verticalOverflow = VerticalWrapMode.Overflow;
        _taskUi.raycastTarget = false;
    }

    static RectTransform CreateRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
        return rt;
    }
}
