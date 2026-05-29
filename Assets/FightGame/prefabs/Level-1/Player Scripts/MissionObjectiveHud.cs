using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Правый верхний угол: жёлтый подзаголовок и строка задачи. После смерти врага — смена текста задачи.</summary>
public class MissionObjectiveHud : MonoBehaviour
{
    [SerializeField] Vector2 anchorFromTopRight = new Vector2(-16f, -16f);
    [SerializeField] Vector2 panelSize = new Vector2(340f, 88f);
    [SerializeField] int subtitleFontSize = 28;
    [SerializeField] int taskFontSize = 18;
    [SerializeField] string subtitleText = "Первая кровь";
    [SerializeField] string taskBeforeKill = "Одолейте врага";
    [SerializeField] string taskAfterKill = "Переход на уровень 2...";
    [Header("Переход на следующий уровень")]
    [SerializeField] bool loadNextLevelAfterKill = true;
    [SerializeField] string nextSceneName = "Level-2";
    [SerializeField] string nextScenePath = "Assets/FightGame/prefabs/Level-2/Level-2.unity";
    [SerializeField] Sprite loadingBackground;
    [SerializeField] float delayBeforeFade = 1f;
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] float loadingScreenDuration = 0.9f;

    static readonly Color YellowSubtitle = new Color(0.95f, 0.82f, 0.2f, 1f);
    static readonly Color YellowTask = new Color(1f, 0.92f, 0.35f, 1f);

    Text subtitleUi;
    Text taskUi;
    bool enemyKilled;
    bool transitionStarted;

    void OnEnable()
    {
        SimpleHealth.Died += OnCharacterDied;
    }

    void OnDisable()
    {
        SimpleHealth.Died -= OnCharacterDied;
    }

    void Start()
    {
        if (!BattleIntroController.FightStarted)
        {
            StartCoroutine(WaitForFightStartThenBuildHud());
            return;
        }

        BuildHud();
        ApplyTexts();
    }

    System.Collections.IEnumerator WaitForFightStartThenBuildHud()
    {
        while (!BattleIntroController.FightStarted)
            yield return null;

        BuildHud();
        ApplyTexts();
    }

    void OnCharacterDied(GameObject who)
    {
        if (who == null || enemyKilled) return;
        if (!who.transform.root.CompareTag("Enemy")) return;
        if (!BattleIntroController.FightStarted)
            return;

        enemyKilled = true;
        if (taskUi != null)
            taskUi.text = taskAfterKill;
        if (loadNextLevelAfterKill && !transitionStarted)
            StartCoroutine(LoadNextLevelRoutine());
    }

    System.Collections.IEnumerator LoadNextLevelRoutine()
    {
        transitionStarted = true;
        ContinuePrompt.SetLevelTransitionActive(true);

        Canvas canvas = BuildLoadingCanvas();
        if (canvas == null)
        {
            ContinuePrompt.SetLevelTransitionActive(false);
            yield break;
        }

        Image bg = canvas.transform.Find("LoadingImage")?.GetComponent<Image>();
        Text continueUi = canvas.transform.Find("ContinueLabel")?.GetComponent<Text>();
        if (bg == null || continueUi == null)
        {
            ContinuePrompt.SetLevelTransitionActive(false);
            yield break;
        }

        canvas.gameObject.SetActive(true);
        bg.color = new Color(1f, 1f, 1f, 0f);

        yield return ContinuePrompt.WaitForSecondsRealtimePauseAware(Mathf.Max(0f, delayBeforeFade));

        float fadeT = Mathf.Max(0.05f, fadeDuration);
        float t = 0f;
        while (t < fadeT)
        {
            while (GamePauseController.IsPaused)
                yield return null;

            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeT);
            bg.color = new Color(1f, 1f, 1f, k);
            yield return null;
        }

        yield return ContinuePrompt.WaitForSecondsRealtimePauseAware(Mathf.Max(0.1f, loadingScreenDuration));
        continueUi.gameObject.SetActive(true);

        yield return ContinuePrompt.WaitUntilContinuePressedPauseAware();

        ContinuePrompt.SetLevelTransitionActive(false);

        if (!TryLoadNextScene())
            Debug.LogWarning($"MissionObjectiveHud: scenes '{nextSceneName}' and '{nextScenePath}' are unavailable.");
    }

    Canvas BuildLoadingCanvas()
    {
        Canvas canvas = ContinuePrompt.CreateTransitionCanvas("LevelTransition_Loading", 1300);
        Transform root = canvas.transform;

        RectTransform imgRt = CreateRect(root, "LoadingImage",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image bg = imgRt.gameObject.AddComponent<Image>();
        bg.sprite = loadingBackground;
        bg.preserveAspect = false;
        bg.color = new Color(1f, 1f, 1f, 0f);

        ContinuePrompt.CreateLabel(root);

        canvas.gameObject.SetActive(false);
        return canvas;
    }

    bool TryLoadNextScene()
    {
        SaveProgressStage nextStage = GameSaveService.GetNextStageAfterScene(SceneManager.GetActiveScene().name);
        if (nextStage != SaveProgressStage.None)
            GameSaveService.AdvanceStage(nextStage);

        // Для перехода из Level-1 в Level-2 грузим целевую сцену напрямую по пути из Build Settings.
        // Это исключает попадание в другой scene entry с таким же именем.
        if (!string.IsNullOrEmpty(nextScenePath) && Application.CanStreamedLevelBeLoaded(nextScenePath))
        {
            SceneManager.LoadScene(nextScenePath, LoadSceneMode.Single);
            return true;
        }

        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            return true;
        }

        return false;
    }

    void BuildHud()
    {
        GameObject canvasGo = new GameObject("MissionObjective_HUD", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 399;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        // Как у PlayerHealthBarHud — одинаковый масштаб UI, без «огромных» чистых пикселей на больших экранах.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.transform.SetParent(null);

        RectTransform panel = CreateRect(canvasGo.transform, "MissionPanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            anchorFromTopRight, panelSize);

        Font font = GameFont.Default;
        GameFont.RequestGlyphs(subtitleText + taskBeforeKill + taskAfterKill, subtitleFontSize, taskFontSize);

        const float subtitleBlockH = 36f;
        const float gap = 8f;
        const float taskBlockH = 30f;

        // Ширина через растяжение (0–1), как у HP-текста; иначе при якоре в одной точке
        // sizeDelta.x < 0 даёт «перевёрнутый» rect — Unity UI не рисует текст.
        RectTransform subRt = CreateRect(panel, "Subtitle",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, -6f), new Vector2(-16f, subtitleBlockH));
        subtitleUi = subRt.gameObject.AddComponent<Text>();
        subtitleUi.font = font;
        subtitleUi.fontSize = subtitleFontSize;
        // Bold у legacy Text — отдельный проход со сдвигом («двойная» картинка). Оставляем Normal.
        subtitleUi.fontStyle = FontStyle.Normal;
        subtitleUi.color = YellowSubtitle;
        subtitleUi.alignment = TextAnchor.UpperRight;
        subtitleUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitleUi.verticalOverflow = VerticalWrapMode.Truncate;
        subtitleUi.raycastTarget = false;

        float taskTop = -(6f + subtitleBlockH + gap);
        RectTransform taskRt = CreateRect(panel, "Task",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, taskTop), new Vector2(-16f, taskBlockH));
        taskUi = taskRt.gameObject.AddComponent<Text>();
        taskUi.font = font;
        taskUi.fontSize = taskFontSize;
        taskUi.fontStyle = FontStyle.Normal;
        taskUi.color = YellowTask;
        taskUi.alignment = TextAnchor.UpperRight;
        taskUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        taskUi.verticalOverflow = VerticalWrapMode.Truncate;
        taskUi.raycastTarget = false;
    }

    void ApplyTexts()
    {
        if (subtitleUi != null) subtitleUi.text = subtitleText;
        if (taskUi != null) taskUi.text = enemyKilled ? taskAfterKill : taskBeforeKill;
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
