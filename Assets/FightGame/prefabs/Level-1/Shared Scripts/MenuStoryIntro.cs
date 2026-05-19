using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Экран перед туториалом: фон + текст по буквам (Aventura).</summary>
public class MenuStoryIntro : MonoBehaviour
{
    static readonly Color StoryColor = new Color(0.94f, 0.86f, 0.62f, 1f);

    const string DefaultStory =
        "Это случилось на рассвете.\n\n" +
        "Гниющий Король пришел к стенам Альтариса не один. С ним была армия - рыцари без чести, самураи из мертвых земель, маги с пустыми глазами и гарпии, застилавшие небо.\n\n" +
        "Замок пал за час.\n\n" +
        "Королевская стража полегла у ворот. Совет магов исчез в зеленом пламени.\n\n" +
        "Гниющий Король прошел через тронный зал, не останавливаясь. Ему не нужен был трон. Ему не нужна была корона.\n\n" +
        "Ему нужна была принцесса.\n\n" +
        "Теперь она в его руках. Никто не знает, что он с ней сделает.\n\n" +
        "Но ты знаешь одно - ты должен помочь.\n\n" +
        "Замок ждет. Принцесса ждет.\n\n" +
        "А впереди - только враги.";

    [Header("Оформление")]
    [SerializeField] Sprite loadingBackground;
    [SerializeField] int storyFontSize = 32;
    [SerializeField] float lineSpacing = 0.92f;
    [SerializeField] Color storyTextColor = StoryColor;

    [Header("Тайминг")]
    [SerializeField] float charsPerSecond = 32f;
    [SerializeField] float pauseAfterPeriod = 0.28f;
    [SerializeField] float pauseAfterParagraph = 0.55f;
    [SerializeField] float pauseBeforeSceneLoad = 2.5f;
    [SerializeField] float skipDelaySeconds = 0.4f;
    [SerializeField] bool allowSkip = true;

    [TextArea(12, 24)]
    [SerializeField] string storyText = DefaultStory;

    GameObject _panel;
    Canvas _storyCanvas;
    Image _backgroundImage;
    Text _storyUi;
    ScrollRect _scroll;
    RectTransform _scrollContent;
    Coroutine _routine;
    string _targetScene;
    bool _isPlaying;
    float _playStartTime;

    public bool IsPlaying => _isPlaying;

    public void Build(Transform canvasParent)
    {
        if (_panel != null) return;

        _panel = new GameObject("StoryIntro", typeof(RectTransform));
        _panel.transform.SetParent(canvasParent, false);
        StretchFull(_panel.GetComponent<RectTransform>());

        _storyCanvas = _panel.AddComponent<Canvas>();
        _storyCanvas.overrideSorting = true;
        _storyCanvas.sortingOrder = 200;
        _panel.AddComponent<GraphicRaycaster>();

        Canvas parentCanvas = canvasParent.GetComponent<Canvas>();
        if (parentCanvas != null)
            _storyCanvas.worldCamera = parentCanvas.worldCamera;

        GameObject bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(_panel.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        _backgroundImage = bgGo.AddComponent<Image>();
        _backgroundImage.raycastTarget = false;
        _backgroundImage.type = Image.Type.Simple;
        _backgroundImage.preserveAspect = false;

        GameObject scrollGo = new GameObject("StoryScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(_panel.transform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(36f, 28f);
        scrollRt.offsetMax = new Vector2(-36f, -28f);

        _scroll = scrollGo.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 24f;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        StretchFull(viewportRt);
        viewportGo.AddComponent<RectMask2D>();
        _scroll.viewport = viewportRt;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        _scrollContent = contentGo.GetComponent<RectTransform>();
        _scrollContent.anchorMin = new Vector2(0f, 1f);
        _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = new Vector2(0f, 400f);
        _scroll.content = _scrollContent;

        GameObject textGo = new GameObject("StoryText", typeof(RectTransform));
        textGo.transform.SetParent(contentGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(-8f, 0f);

        _storyUi = textGo.AddComponent<Text>();
        _storyUi.text = "";
        _storyUi.font = LoadStoryFont();
        _storyUi.fontSize = storyFontSize;
        _storyUi.lineSpacing = lineSpacing;
        _storyUi.fontStyle = FontStyle.Normal;
        _storyUi.color = storyTextColor;
        _storyUi.alignment = TextAnchor.UpperLeft;
        _storyUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _storyUi.verticalOverflow = VerticalWrapMode.Overflow;
        _storyUi.raycastTarget = false;
        _storyUi.supportRichText = false;

        _panel.SetActive(false);
    }

    void ShowBackground()
    {
        if (_backgroundImage == null || loadingBackground == null) return;

        if (loadingBackground.texture != null)
            loadingBackground.texture.filterMode = FilterMode.Point;

        _backgroundImage.sprite = loadingBackground;
        _backgroundImage.enabled = true;
    }

    public void Play(string sceneName)
    {
        if (_panel == null) return;

        _targetScene = sceneName;
        _playStartTime = Time.unscaledTime;
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);
        ShowBackground();

        Font font = LoadStoryFont();
        _storyUi.font = font;

        string full = GetFullStory();
        _storyUi.text = "";
        PrepareFontForStory(font, full, storyFontSize);

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(PlayRoutine(full));
    }

    IEnumerator PlayRoutine(string full)
    {
        _isPlaying = true;
        yield return null;
        UpdateContentHeight();

        var shown = new StringBuilder(full.Length);

        for (int i = 0; i < full.Length; i++)
        {
            if (allowSkip && Time.unscaledTime - _playStartTime >= skipDelaySeconds && WasSkipPressed())
            {
                shown.Clear();
                shown.Append(full);
                break;
            }

            char c = full[i];
            shown.Append(c);
            _storyUi.text = shown.ToString();
            UpdateContentHeight();
            ScrollToLatestLine();

            yield return new WaitForSecondsRealtime(DelayFor(c, full, i));
        }

        _storyUi.text = full;
        UpdateContentHeight();
        ScrollToLatestLine();
        yield return null;
        ScrollToLatestLine();

        yield return new WaitForSecondsRealtime(pauseBeforeSceneLoad);
        _isPlaying = false;

        if (!string.IsNullOrEmpty(_targetScene) && Application.CanStreamedLevelBeLoaded(_targetScene))
            SceneManager.LoadScene(_targetScene);
        else
            Debug.LogWarning($"MenuStoryIntro: сцена «{_targetScene}» недоступна.");
    }

    string GetFullStory()
    {
        string raw = string.IsNullOrWhiteSpace(storyText) || storyText.Length < DefaultStory.Length * 0.5f
            ? DefaultStory
            : storyText;
        return CompressStorySpacing(raw);
    }

    static string CompressStorySpacing(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw.Replace("\r\n", "\n").Replace("\n\n\n", "\n\n");
    }

    static Font LoadStoryFont()
    {
        GameFont.Reload();
        Font font = GameFont.Aventura;
        if (font == null)
            Debug.LogError("MenuStoryIntro: не найден ofont.ru_Aventura.ttf (prefabs/Menu или Resources/Fonts).");
        return font;
    }

    static void PrepareFontForStory(Font font, string full, int fontSize)
    {
        if (font == null || string.IsNullOrEmpty(full)) return;
        GameFont.RequestGlyphs(full, fontSize, FontStyle.Normal);
    }

    void UpdateContentHeight()
    {
        if (_storyUi == null || _scrollContent == null) return;

        float height = _storyUi.preferredHeight + 24f;
        _scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(height, 200f));
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        Canvas.ForceUpdateCanvases();
    }

    void ScrollToLatestLine()
    {
        if (_scroll == null) return;
        _scroll.verticalNormalizedPosition = 0f;
    }

    float DelayFor(char c, string full, int index)
    {
        float baseDelay = 1f / Mathf.Max(1f, charsPerSecond);

        if (c == '\n')
        {
            bool paragraphBreak = index > 0 && full[index - 1] == '\n';
            return paragraphBreak ? pauseAfterParagraph : baseDelay * 0.5f;
        }

        if (c == '.' || c == '!' || c == '?')
            return baseDelay + pauseAfterPeriod;

        if (c == ',' || c == ';' || c == ':')
            return baseDelay + pauseAfterPeriod * 0.35f;

        if (c == '-')
            return baseDelay + pauseAfterPeriod * 0.2f;

        return baseDelay;
    }

    static bool WasSkipPressed()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            return true;
#endif
        return false;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
