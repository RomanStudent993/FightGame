using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Единая надпись «продолжить» на экранах перехода между сценами.</summary>
public static class ContinuePrompt
{
    public const string Text = "- Нажмите любую клавишу, чтобы продолжить";
    public const int FontSize = 20;

    static readonly Color LabelColor = new Color(1f, 0.92f, 0.35f, 1f);
    static readonly Vector2 AnchorPosition = new Vector2(-24f, 24f);
    static readonly Vector2 Size = new Vector2(560f, 36f);

    public static bool IsLevelTransitionActive { get; private set; }

    static GameObject _blockingOverlayRoot;
    static bool _sceneLoadedHookRegistered;

    public static void SetLevelTransitionActive(bool active)
    {
        IsLevelTransitionActive = active;
    }

    public static void EnsureSceneLoadedHook()
    {
        if (_sceneLoadedHookRegistered)
            return;

        _sceneLoadedHookRegistered = true;
        SceneManager.sceneLoaded += OnSceneLoadedHideBlockingOverlay;
    }

    static void OnSceneLoadedHideBlockingOverlay(Scene scene, LoadSceneMode mode)
    {
        HideBlockingOverlay();
    }

    /// <summary>Полноэкранная заглушка на время LoadScene (не чёрный кадр Unity).</summary>
    public static void ShowBlockingOverlay(Sprite background)
    {
        EnsureSceneLoadedHook();
        HideBlockingOverlay();

        _blockingOverlayRoot = new GameObject("SceneBlockingOverlay", typeof(RectTransform));
        Object.DontDestroyOnLoad(_blockingOverlayRoot);

        Canvas canvas = _blockingOverlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;

        CanvasScaler scaler = _blockingOverlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageGo = new GameObject("Background", typeof(RectTransform));
        imageGo.transform.SetParent(_blockingOverlayRoot.transform, false);
        RectTransform rt = imageGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = imageGo.AddComponent<Image>();
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        if (background != null)
        {
            image.sprite = background;
            image.color = Color.white;
            if (background.texture != null)
                background.texture.filterMode = FilterMode.Point;
        }
        else
        {
            image.color = new Color(0.05f, 0.05f, 0.06f, 1f);
        }
    }

    public static void HideBlockingOverlay()
    {
        if (_blockingOverlayRoot == null)
            return;

        Object.Destroy(_blockingOverlayRoot);
        _blockingOverlayRoot = null;
    }

    public static Canvas CreateTransitionCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        return canvas;
    }

    public static Text CreateLabel(Transform parent)
    {
        GameObject go = new GameObject("ContinueLabel", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = AnchorPosition;
        rt.sizeDelta = Size;

        Text label = go.AddComponent<Text>();
        ApplyStyle(label);
        label.gameObject.SetActive(false);
        return label;
    }

    public static void ApplyStyle(Text label)
    {
        if (label == null)
            return;

        GameFont.RequestGlyphs(Text, FontSize, FontStyle.Normal);
        label.font = GameFont.Default;
        label.fontSize = FontSize;
        label.fontStyle = FontStyle.Normal;
        label.color = LabelColor;
        label.alignment = TextAnchor.LowerRight;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = Text;
    }

    public static bool WasAnyKeyPressed()
    {
        return WasContinueKeyPressed();
    }

    public static bool WasContinueKeyPressed()
    {
        if (WasEscapePressed())
            return false;

        if (Input.anyKeyDown) return true;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame))
            return true;
        if (Gamepad.current != null && (
            Gamepad.current.buttonSouth.wasPressedThisFrame ||
            Gamepad.current.buttonNorth.wasPressedThisFrame ||
            Gamepad.current.buttonWest.wasPressedThisFrame ||
            Gamepad.current.buttonEast.wasPressedThisFrame ||
            Gamepad.current.startButton.wasPressedThisFrame))
            return true;
#endif
        return false;
    }

    public static IEnumerator WaitForSecondsRealtimePauseAware(float seconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration)
        {
            yield return WaitForUnpausedFrame();
            if (!GamePauseController.IsPaused)
                elapsed += Time.unscaledDeltaTime;
        }
    }

    public static IEnumerator WaitUntilContinuePressedPauseAware()
    {
        while (true)
        {
            yield return WaitForUnpausedFrame();
            if (GamePauseController.IsPaused)
                continue;
            if (WasContinueKeyPressed())
                yield break;
        }
    }

    static IEnumerator WaitForUnpausedFrame()
    {
        while (GamePauseController.IsPaused)
            yield return null;
    }

    static bool WasEscapePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
        return false;
    }
}
