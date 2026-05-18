using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Главное меню: main.png, кнопки; «Новая игра» — выбор пустой ячейки сохранения.</summary>
[DefaultExecutionOrder(-100)]
public class StartMenuBootstrap : MonoBehaviour
{
    static readonly Color TitleColor = new Color(0.95f, 0.82f, 0.2f, 1f);

    [Header("Фон")]
    [SerializeField] Sprite menuBackgroundSprite;

    [Header("Кнопки меню")]
    [SerializeField] Sprite newGameButtonSprite;
    [SerializeField] Sprite downloadButtonSprite;
    [SerializeField] Sprite continueButtonSprite;
    [SerializeField] Sprite exitButtonSprite;

    [Header("Ячейки сохранения")]
    [SerializeField] Sprite emptySlotSprite;
    [Tooltip("Aventura.ttf — подтягивается автоматически, если пусто.")]
    [SerializeField] Font titleFont;
    [SerializeField] string saveSlotTitle = "Выберите ячейку для сохранения";
    [Tooltip("Должен совпадать с Font Size в импорте Aventura.ttf (Inspector).")]
    [SerializeField] int saveSlotTitleFontSize = 40;

    [Header("Сцены")]
    [SerializeField] string newGameSceneName = "EducationDemo";
    [SerializeField] string continueSceneName = "battle";

    [Header("Вёрстка меню")]
    [SerializeField] float buttonWidth = 560f;
    [SerializeField] float buttonBottomMargin = 40f;
    [SerializeField] float buttonSpacing = 12f;

    [Header("Вёрстка ячеек")]
    [SerializeField] float slotWidth = 260f;
    [SerializeField] float slotSpacing = 52f;
    [SerializeField] float slotsCenterYOffset = -30f;
    [SerializeField] float titleGapAboveSlots = 32f;

    GameObject _menuButtonsRoot;
    GameObject _saveSlotPanel;
    MenuStoryIntro _storyIntro;
    bool _saveSlotsVisible;

    void Awake()
    {
        GameFont.Reload();
        titleFont = ResolveTitleFont();

        EnsureEventSystem();
        BuildUi();
    }

    Font ResolveTitleFont()
    {
        titleFont = GameFont.Aventura;
        if (titleFont == null)
            Debug.LogError("StartMenuBootstrap: не найден Aventura.ttf в Resources/Fonts.");
        return titleFont;
    }

    void Update()
    {
        if (_storyIntro != null && _storyIntro.IsPlaying) return;

        if (!_saveSlotsVisible) return;
        if (WasEscapePressed())
            ShowMainMenu();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildUi()
    {
        GameObject canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        if (menuBackgroundSprite != null)
        {
            GameObject bgGo = CreateUiObject("Background", canvasGo.transform);
            StretchFull(bgGo.GetComponent<RectTransform>());
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = menuBackgroundSprite;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.raycastTarget = false;
            if (menuBackgroundSprite.texture != null)
                menuBackgroundSprite.texture.filterMode = FilterMode.Point;
        }

        _menuButtonsRoot = CreateUiObject("MenuButtons", canvasGo.transform);
        StretchFull(_menuButtonsRoot.GetComponent<RectTransform>());
        BuildMenuButtons(_menuButtonsRoot.transform);

        _saveSlotPanel = CreateUiObject("SaveSlotPanel", canvasGo.transform);
        StretchFull(_saveSlotPanel.GetComponent<RectTransform>());
        BuildSaveSlotPanel(_saveSlotPanel.transform);
        _saveSlotPanel.SetActive(false);

        _storyIntro = GetComponent<MenuStoryIntro>();
        if (_storyIntro == null)
            _storyIntro = gameObject.AddComponent<MenuStoryIntro>();
        _storyIntro.Build(canvasGo.transform);
    }

    void BuildMenuButtons(Transform parent)
    {
        float bottom = buttonBottomMargin;

        if (exitButtonSprite != null)
        {
            AddSpriteButton(parent, "Exit", exitButtonSprite, bottom, QuitGame);
            bottom += ButtonHeight(exitButtonSprite) + buttonSpacing;
        }

        if (continueButtonSprite != null)
        {
            AddSpriteButton(parent, "Continue", continueButtonSprite, bottom, OnContinue);
            bottom += ButtonHeight(continueButtonSprite) + buttonSpacing;
        }

        if (downloadButtonSprite != null)
        {
            AddSpriteButton(parent, "Download", downloadButtonSprite, bottom, OnDownload);
            bottom += ButtonHeight(downloadButtonSprite) + buttonSpacing;
        }

        if (newGameButtonSprite != null)
            AddSpriteButton(parent, "NewGame", newGameButtonSprite, bottom, OnNewGame);
    }

    void BuildSaveSlotPanel(Transform parent)
    {
        if (emptySlotSprite == null) return;

        float slotHeight = SlotHeight(emptySlotSprite);
        float rowWidth = slotWidth * 3f + slotSpacing * 2f;
        float titleBlockHeight = Mathf.Max(80f, saveSlotTitleFontSize + 36f);
        float titleY = slotsCenterYOffset + slotHeight * 0.5f + titleGapAboveSlots + titleBlockHeight * 0.5f;

        Font font = ResolveTitleFont();
        if (font == null) return;

        font.RequestCharactersInTexture(saveSlotTitle, saveSlotTitleFontSize, FontStyle.Normal);

        GameObject titleGo = CreateUiObject("Title", parent);
        titleGo.transform.SetAsLastSibling();
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1700f, titleBlockHeight);
        titleRt.anchoredPosition = new Vector2(0f, titleY);

        Text titleText = titleGo.AddComponent<Text>();
        titleText.text = saveSlotTitle;
        titleText.font = font;
        titleText.fontSize = saveSlotTitleFontSize;
        titleText.fontStyle = FontStyle.Normal;
        titleText.color = TitleColor;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.raycastTarget = false;
        titleText.supportRichText = false;

        GameObject rowGo = CreateUiObject("Slots", parent);
        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(rowWidth, slotHeight);
        rowRt.anchoredPosition = new Vector2(0f, slotsCenterYOffset);

        float startX = -rowWidth * 0.5f + slotWidth * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (slotWidth + slotSpacing);
            AddEmptySlot(rowGo.transform, i + 1, x, OnSaveSlotClicked);
        }
    }

    void AddEmptySlot(Transform parent, int slotIndex, float localX, UnityAction<int> onClick)
    {
        Vector2 size = new Vector2(slotWidth, SlotHeight(emptySlotSprite));

        GameObject slotGo = CreateUiObject($"Slot{slotIndex}", parent);
        RectTransform rt = slotGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(localX, 0f);

        Image img = slotGo.AddComponent<Image>();
        img.sprite = emptySlotSprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        if (emptySlotSprite.texture != null)
            emptySlotSprite.texture.filterMode = FilterMode.Point;

        Button btn = slotGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        btn.colors = colors;

        int captured = slotIndex;
        btn.onClick.AddListener(() => onClick(captured));
    }

    float ButtonHeight(Sprite sprite)
    {
        if (sprite == null) return 0f;
        float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
        return buttonWidth * aspect;
    }

    float SlotHeight(Sprite sprite)
    {
        if (sprite == null) return 0f;
        float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
        return slotWidth * aspect;
    }

    void AddSpriteButton(Transform parent, string objectName, Sprite sprite, float bottomOffset, UnityAction onClick)
    {
        Vector2 size = new Vector2(buttonWidth, ButtonHeight(sprite));

        GameObject btnGo = CreateUiObject(objectName, parent);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, bottomOffset);

        Image img = btnGo.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        if (sprite.texture != null)
            sprite.texture.filterMode = FilterMode.Point;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
    }

    void OnNewGame() => ShowSaveSlots();

    void ShowSaveSlots()
    {
        _saveSlotsVisible = true;
        if (_menuButtonsRoot != null)
            _menuButtonsRoot.SetActive(false);
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(true);
    }

    void ShowMainMenu()
    {
        _saveSlotsVisible = false;
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(false);
        if (_menuButtonsRoot != null)
            _menuButtonsRoot.SetActive(true);
    }

    static bool WasEscapePressed()
    {
        bool pressed = Input.GetKeyDown(KeyCode.Escape);
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    void OnSaveSlotClicked(int slotIndex)
    {
        _saveSlotsVisible = false;
        if (_saveSlotPanel != null) _saveSlotPanel.SetActive(false);
        if (_menuButtonsRoot != null) _menuButtonsRoot.SetActive(false);

        if (_storyIntro != null)
            _storyIntro.Play(newGameSceneName);
        else
            LoadScene(newGameSceneName);
    }

    void OnContinue() => LoadScene(continueSceneName);

    void OnDownload()
    {
        Debug.Log("Download — пока не реализовано.");
    }

    static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("StartMenuBootstrap: имя сцены не задано.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"StartMenuBootstrap: сцена «{sceneName}» не в Build Settings.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
