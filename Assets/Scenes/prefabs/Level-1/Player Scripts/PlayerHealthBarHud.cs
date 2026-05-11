using UnityEngine;
using UnityEngine.UI;

/// <summary>Верхний левый угол: красная полоска HP и красные цифры (игрок).</summary>
public class PlayerHealthBarHud : MonoBehaviour
{
    static Sprite s_whiteSprite;

    [SerializeField] Vector2 anchorFromTopLeft = new Vector2(16f, -16f);
    [SerializeField] float panelWidth = 240f;
    [SerializeField] float barHeight = 18f;
    [Tooltip("Отступ между нижним краем текста HP и верхом полоски.")]
    [SerializeField] float spaceBetweenTextAndBar = 14f;
    [SerializeField] float topTextPadding = 8f;
    [SerializeField] float textBlockHeight = 28f;
    [SerializeField] float barBottomMargin = 12f;

    static readonly Color BarBackground = new Color(0.35f, 0.05f, 0.05f, 0.95f);
    static readonly Color BarFill = new Color(0.92f, 0.12f, 0.12f, 1f);
    static readonly Color TextColor = new Color(0.95f, 0.18f, 0.18f, 1f);

    SimpleHealth health;
    Image fillImage;
    Text hpText;

    static Sprite WhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;
        Texture2D t = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }

    void Awake()
    {
        health = GetComponent<SimpleHealth>();
    }

    void Start()
    {
        if (health == null) return;
        BuildHud();
        Refresh();
    }

    void LateUpdate()
    {
        Refresh();
    }

    void BuildHud()
    {
        GameObject canvasGo = new GameObject("PlayerHP_HUD", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.transform.SetParent(null);

        float panelHeight = topTextPadding + textBlockHeight + spaceBetweenTextAndBar + barHeight + barBottomMargin;
        RectTransform panel = CreateRect(canvasGo.transform, "HPPanel",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            anchorFromTopLeft, new Vector2(panelWidth, panelHeight));

        RectTransform textRt = CreateRect(panel, "HPText",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -topTextPadding), new Vector2(-8f, textBlockHeight));
        hpText = textRt.gameObject.AddComponent<Text>();
        hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hpText.fontSize = 26;
        hpText.fontStyle = FontStyle.Bold;
        hpText.color = TextColor;
        hpText.alignment = TextAnchor.UpperLeft;
        hpText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hpText.verticalOverflow = VerticalWrapMode.Overflow;
        hpText.raycastTarget = false;

        RectTransform barBgRt = CreateRect(panel, "BarBackground",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, barBottomMargin), new Vector2(-8f, barHeight));
        Image bg = barBgRt.gameObject.AddComponent<Image>();
        bg.sprite = WhiteSprite();
        bg.color = BarBackground;
        bg.raycastTarget = false;

        RectTransform fillRt = CreateRect(barBgRt, "BarFill",
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        fillImage = fillRt.gameObject.AddComponent<Image>();
        fillImage.sprite = WhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = BarFill;
        fillImage.raycastTarget = false;
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

    void Refresh()
    {
        if (health == null || fillImage == null || hpText == null) return;
        int cur = health.CurrentHp;
        int max = Mathf.Max(1, health.MaxHp);
        fillImage.fillAmount = Mathf.Clamp01((float)cur / max);
        hpText.text = cur + " / " + max;
    }
}
