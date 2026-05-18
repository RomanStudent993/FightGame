using UnityEngine;
using UnityEngine.UI;

/// <summary>Правый верхний угол: жёлтый подзаголовок и строка задачи. После смерти врага — смена текста задачи.</summary>
public class MissionObjectiveHud : MonoBehaviour
{
    [SerializeField] Vector2 anchorFromTopRight = new Vector2(-16f, -16f);
    [SerializeField] Vector2 panelSize = new Vector2(340f, 88f);
    [SerializeField] int subtitleFontSize = 28;
    [SerializeField] int taskFontSize = 18;
    [SerializeField] string subtitleText = "Первая кровь";
    [SerializeField] string taskBeforeKill = "Убейте врага";
    [SerializeField] string taskAfterKill = "Покиньте помещение";

    static readonly Color YellowSubtitle = new Color(0.95f, 0.82f, 0.2f, 1f);
    static readonly Color YellowTask = new Color(1f, 0.92f, 0.35f, 1f);

    Text subtitleUi;
    Text taskUi;
    bool enemyKilled;

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
        BuildHud();
        ApplyTexts();
    }

    void OnCharacterDied(GameObject who)
    {
        if (who == null || enemyKilled) return;
        if (!who.transform.root.CompareTag("Enemy")) return;
        enemyKilled = true;
        if (taskUi != null)
            taskUi.text = taskAfterKill;
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
