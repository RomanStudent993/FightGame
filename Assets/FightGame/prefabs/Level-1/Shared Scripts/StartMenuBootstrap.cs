using System.Collections;
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
    [SerializeField] Sprite loadingBackground;

    [Header("Кнопки меню")]
    [SerializeField] Sprite newGameButtonSprite;
    [SerializeField] Sprite downloadButtonSprite;
    [SerializeField] Sprite continueButtonSprite;
    [SerializeField] Sprite exitButtonSprite;

    [Header("Ячейки сохранения")]
    [SerializeField] Sprite emptySlotSprite;
    [Tooltip("ofont.ru_Aventura.ttf — подтягивается автоматически, если пусто.")]
    [SerializeField] Font titleFont;
    [SerializeField] string saveSlotTitle = "Выберите ячейку для сохранения";
    [SerializeField] string loadSlotTitle = "Выберите сохранение";
    [Tooltip("Должен совпадать с Font Size в импорте ofont.ru_Aventura.ttf (Inspector).")]
    [SerializeField] int saveSlotTitleFontSize = 40;

    [Header("Сцены")]
    [SerializeField] string newGameSceneName = "EducationDemo";

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
    GameObject _menuBackgroundGo;
    GameObject _slotsRowGo;
    Text _saveSlotTitleText;
    Button _continueButton;
    Button _loadButton;
    Button _newGameButton;
    Button _exitButton;
    MenuStoryIntro _storyIntro;
    bool _saveSlotsVisible;
    bool _loadTransitionActive;
    SaveSlotPanelMode _slotPanelMode;

    enum SaveSlotPanelMode
    {
        None,
        NewGame,
        Load
    }

    void Awake()
    {
        GameFont.Reload();
        GameSaveService.RestoreActiveSlotFromPrefs();
        titleFont = ResolveTitleFont();

        EnsureEventSystem();
        BuildUi();
        RefreshSaveDependentButtons();
        DisableMenuHeroCombat();
    }

    void DisableMenuHeroCombat()
    {
        foreach (HeroKnight hero in FindObjectsByType<HeroKnight>())
            hero.enabled = false;

        foreach (PlayerAttackDamage attack in FindObjectsByType<PlayerAttackDamage>())
            attack.enabled = false;

        foreach (PlayerShieldDefense shield in FindObjectsByType<PlayerShieldDefense>())
            shield.enabled = false;
    }

    Font ResolveTitleFont()
    {
        titleFont = GameFont.ResolveForText(saveSlotTitle, saveSlotTitleFontSize);
        if (titleFont == null)
            Debug.LogError("StartMenuBootstrap: не найден шрифт для заголовка ячеек (Aventura / LegacyRuntime).");
        return titleFont;
    }

    void Update()
    {
        // Ctrl+Shift+Пробел во время текста истории обрабатывает MenuStoryIntro (скип).
        if (_storyIntro != null && _storyIntro.IsPlaying) return;

        // Ctrl+Shift+Пробел в главном меню / панели слотов — удалить все сохранения.
        if (!_loadTransitionActive && WasDeleteAllSavesPressed())
        {
            GameSaveService.DeleteAllSaves();
            RefreshSaveDependentButtons();
            if (_saveSlotsVisible)
                RefreshSaveSlotPanel();
            Debug.Log("GameSaveService: все сохранения удалены.");
            return;
        }

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
            _menuBackgroundGo = CreateUiObject("Background", canvasGo.transform);
            StretchFull(_menuBackgroundGo.GetComponent<RectTransform>());
            Image bg = _menuBackgroundGo.AddComponent<Image>();
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

        _storyIntro.EnsureLoadingBackground(loadingBackground);
        _storyIntro.Build(canvasGo.transform);
    }

    void BuildMenuButtons(Transform parent)
    {
        if (exitButtonSprite != null)
            _exitButton = AddSpriteButton(parent, "Exit", exitButtonSprite, 0f, QuitGame);

        if (newGameButtonSprite != null)
            _newGameButton = AddSpriteButton(parent, "NewGame", newGameButtonSprite, 0f, OnNewGame);

        if (downloadButtonSprite != null)
            _loadButton = AddSpriteButton(parent, "Load", downloadButtonSprite, 0f, OnLoad);

        if (continueButtonSprite != null)
            _continueButton = AddSpriteButton(parent, "Continue", continueButtonSprite, 0f, OnContinue);

        LayoutMenuButtons();
    }

    void LayoutMenuButtons()
    {
        float bottom = buttonBottomMargin;

        if (_exitButton != null)
        {
            SetButtonBottom(_exitButton, bottom);
            bottom += GetButtonHeight(_exitButton) + buttonSpacing;
        }

        Button[] middleButtons = GameSaveService.HasAnySave()
            ? new[] { _newGameButton, _loadButton, _continueButton }
            : new[] { _continueButton, _loadButton, _newGameButton };

        for (int i = 0; i < middleButtons.Length; i++)
        {
            Button btn = middleButtons[i];
            if (btn == null)
                continue;

            SetButtonBottom(btn, bottom);
            bottom += GetButtonHeight(btn) + buttonSpacing;
        }
    }

    static void SetButtonBottom(Button btn, float bottom)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, bottom);
    }

    static float GetButtonHeight(Button btn)
    {
        return btn.GetComponent<RectTransform>().sizeDelta.y;
    }

    void BuildSaveSlotPanel(Transform parent)
    {
        if (emptySlotSprite == null) return;

        float slotHeight = SlotHeight(emptySlotSprite);
        float rowWidth = slotWidth * 3f + slotSpacing * 2f;
        float titleBlockHeight = Mathf.Max(80f, saveSlotTitleFontSize + 36f);
        float titleY = slotsCenterYOffset + slotHeight * 0.5f + titleGapAboveSlots + titleBlockHeight * 0.5f;

        _slotsRowGo = CreateUiObject("Slots", parent);
        RectTransform rowRt = _slotsRowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(rowWidth, slotHeight);
        rowRt.anchoredPosition = new Vector2(0f, slotsCenterYOffset);

        Font font = ResolveTitleFont();
        if (font == null) return;

        GameFont.RequestGlyphs(saveSlotTitle, saveSlotTitleFontSize, 22);
        GameFont.RequestGlyphs(loadSlotTitle, saveSlotTitleFontSize, 22);

        GameObject titleGo = CreateUiObject("Title", parent);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1700f, titleBlockHeight);
        titleRt.anchoredPosition = new Vector2(0f, titleY);

        _saveSlotTitleText = titleGo.AddComponent<Text>();
        _saveSlotTitleText.text = saveSlotTitle;
        _saveSlotTitleText.font = font;
        _saveSlotTitleText.fontSize = saveSlotTitleFontSize;
        _saveSlotTitleText.fontStyle = FontStyle.Normal;
        _saveSlotTitleText.color = TitleColor;
        _saveSlotTitleText.alignment = TextAnchor.MiddleCenter;
        _saveSlotTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _saveSlotTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        _saveSlotTitleText.raycastTarget = false;
        _saveSlotTitleText.supportRichText = false;
        titleGo.transform.SetAsLastSibling();

        RefreshSaveSlotPanel();
    }

    void RefreshSaveSlotPanel()
    {
        if (_slotsRowGo == null || emptySlotSprite == null)
            return;

        for (int i = _slotsRowGo.transform.childCount - 1; i >= 0; i--)
            Destroy(_slotsRowGo.transform.GetChild(i).gameObject);

        if (_saveSlotTitleText != null)
            _saveSlotTitleText.text = _slotPanelMode == SaveSlotPanelMode.Load ? loadSlotTitle : saveSlotTitle;

        float rowWidth = slotWidth * 3f + slotSpacing * 2f;
        float startX = -rowWidth * 0.5f + slotWidth * 0.5f;
        for (int i = 0; i < GameSaveService.SlotCount; i++)
        {
            int slotIndex = i + 1;
            float x = startX + i * (slotWidth + slotSpacing);
            bool occupied = GameSaveService.HasSave(slotIndex);
            bool interactable = _slotPanelMode != SaveSlotPanelMode.Load || occupied;
            SaveProgressStage stage = GameSaveService.GetStage(slotIndex);
            AddSaveSlot(_slotsRowGo.transform, slotIndex, x, interactable, stage);
        }
    }

    void AddSaveSlot(Transform parent, int slotIndex, float localX, bool interactable, SaveProgressStage stage)
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
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        btn.colors = colors;
        btn.interactable = interactable;

        if (stage != SaveProgressStage.None)
        {
            Font font = ResolveTitleFont();
            if (font != null)
            {
                string label = GameSaveService.GetStageDisplayName(stage);
                GameFont.RequestGlyphs(label, 24, 22);

                GameObject labelGo = CreateUiObject("Label", slotGo.transform);
                RectTransform labelRt = labelGo.GetComponent<RectTransform>();
                StretchFull(labelRt);
                labelRt.offsetMin = new Vector2(12f, 12f);
                labelRt.offsetMax = new Vector2(-12f, -12f);

                Text labelText = labelGo.AddComponent<Text>();
                labelText.text = label;
                labelText.font = font;
                labelText.fontSize = 24;
                labelText.fontStyle = FontStyle.Normal;
                labelText.color = TitleColor;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
                labelText.verticalOverflow = VerticalWrapMode.Overflow;
                labelText.raycastTarget = false;
                labelText.supportRichText = false;
            }
        }

        int captured = slotIndex;
        btn.onClick.AddListener(() => OnSaveSlotClicked(captured));
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

    Button AddSpriteButton(Transform parent, string objectName, Sprite sprite, float bottomOffset, UnityAction onClick)
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
        return btn;
    }

    void RefreshSaveDependentButtons()
    {
        bool hasSave = GameSaveService.HasAnySave();
        if (_continueButton != null)
            _continueButton.interactable = hasSave;
        if (_loadButton != null)
            _loadButton.interactable = hasSave;

        LayoutMenuButtons();
    }

    void OnNewGame() => ShowSaveSlots(SaveSlotPanelMode.NewGame);

    void ShowSaveSlots(SaveSlotPanelMode mode)
    {
        _slotPanelMode = mode;
        _saveSlotsVisible = true;
        RefreshSaveSlotPanel();
        if (_menuButtonsRoot != null)
            _menuButtonsRoot.SetActive(false);
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(true);
    }

    void ShowMainMenu()
    {
        _saveSlotsVisible = false;
        _slotPanelMode = SaveSlotPanelMode.None;
        if (_saveSlotPanel != null)
            _saveSlotPanel.SetActive(false);
        if (_menuButtonsRoot != null)
            _menuButtonsRoot.SetActive(true);
        if (_menuBackgroundGo != null)
            _menuBackgroundGo.SetActive(true);
        RefreshSaveDependentButtons();
    }

    void HideMenuChrome()
    {
        if (_saveSlotPanel != null) _saveSlotPanel.SetActive(false);
        if (_menuButtonsRoot != null) _menuButtonsRoot.SetActive(false);
        if (_menuBackgroundGo != null) _menuBackgroundGo.SetActive(false);
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
        if (_slotPanelMode == SaveSlotPanelMode.Load)
        {
            LoadSaveFromSlot(slotIndex);
            return;
        }

        GameSaveService.CreateNewGame(slotIndex);
        _saveSlotsVisible = false;
        HideMenuChrome();

        if (_storyIntro != null)
            _storyIntro.Play(newGameSceneName);
        else
            LoadScene(newGameSceneName);
    }

    void LoadSaveFromSlot(int slotIndex)
    {
        if (!GameSaveService.HasSave(slotIndex))
            return;

        GameSaveService.SetActiveSlot(slotIndex);
        SaveProgressStage stage = GameSaveService.GetStage(slotIndex);
        string sceneName = GameSaveService.GetSceneForStage(stage);
        if (string.IsNullOrEmpty(sceneName))
            return;

        _saveSlotsVisible = false;
        HideMenuChrome();
        StartCoroutine(LoadSaveRoutine(sceneName, stage));
    }

    IEnumerator LoadSaveRoutine(string sceneName, SaveProgressStage stage)
    {
        yield return ShowMainPressAnyKeyScreen();

        if (GameSaveService.ShouldPlayStoryIntro(stage) && _storyIntro != null)
            _storyIntro.Play(sceneName);
        else
            LoadScene(sceneName);
    }

    IEnumerator ShowMainPressAnyKeyScreen()
    {
        _loadTransitionActive = true;

        Canvas canvas = ContinuePrompt.CreateTransitionCanvas("LoadGamePrompt", 250);
        Transform root = canvas.transform;

        GameObject imgGo = CreateUiObject("LoadingImage", root);
        StretchFull(imgGo.GetComponent<RectTransform>());
        Image bg = imgGo.AddComponent<Image>();
        bg.raycastTarget = true;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;

        Sprite loading = ResolveLoadingBackground();
        if (loading != null)
        {
            bg.sprite = loading;
            bg.color = Color.white;
            if (loading.texture != null)
                loading.texture.filterMode = FilterMode.Point;
        }
        else
        {
            bg.color = new Color(0.05f, 0.05f, 0.06f, 1f);
        }

        Text label = ContinuePrompt.CreateLabel(root);
        label.gameObject.SetActive(true);

        yield return null;

        while (!ContinuePrompt.WasAnyKeyPressed())
            yield return null;

        if (canvas != null)
            Destroy(canvas.gameObject);

        _loadTransitionActive = false;
    }

    Sprite ResolveLoadingBackground()
    {
        if (loadingBackground != null)
            return loadingBackground;

        if (_storyIntro != null)
            return _storyIntro.LoadingBackground;

        MenuStoryIntro intro = GetComponent<MenuStoryIntro>();
        return intro != null ? intro.LoadingBackground : null;
    }

    void OnContinue()
    {
        int slotIndex = GameSaveService.GetLastUsedSlot();
        if (slotIndex < 0)
            return;

        LoadSaveFromSlot(slotIndex);
    }

    void OnLoad()
    {
        if (!GameSaveService.HasAnySave())
            return;

        ShowSaveSlots(SaveSlotPanelMode.Load);
    }

    static bool WasDeleteAllSavesPressed() => GameplayCheatKeys.WasCtrlShiftSpacePressed();

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
