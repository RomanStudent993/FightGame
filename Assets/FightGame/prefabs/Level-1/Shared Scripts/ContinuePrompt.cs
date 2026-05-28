using UnityEngine;
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
}
