using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Собирает экран «Начало»: фон из Sprite (Menu/start) или из StreamingAssets, кнопки «Начать» / «Выйти».</summary>
[DefaultExecutionOrder(-100)]
public class StartMenuBootstrap : MonoBehaviour
{
    [SerializeField] string nextSceneName = "battle";
    [Tooltip("Если задан — фон меню из ассета (например Assets/.../Menu/start.png). Иначе грузится файл ниже.")]
    [SerializeField] Sprite menuBackgroundSprite;
    [Tooltip("Путь относительно StreamingAssets, если спрайт не задан (сырой PNG).")]
    [SerializeField] string streamingAssetRelativePath = "Menu/start.png";

    void Awake()
    {
        EnsureEventSystem();
        BuildUi();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
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
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject bgGo = CreateUiObject("Background", canvasGo.transform);
        StretchFull(bgGo.GetComponent<RectTransform>());
        if (menuBackgroundSprite != null)
        {
            Image img = bgGo.AddComponent<Image>();
            img.sprite = menuBackgroundSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            if (menuBackgroundSprite.texture != null)
                menuBackgroundSprite.texture.filterMode = FilterMode.Point;
        }
        else
        {
            RawImage raw = bgGo.AddComponent<RawImage>();
            raw.color = Color.white;
            LoadSplash(raw);
        }

        Vector2 btnSize = new Vector2(340, 80);
        AddMenuButton(canvasGo.transform, "StartButton", "Начать", 0.36f, btnSize, LoadNextLevel);
        AddMenuButton(canvasGo.transform, "QuitButton", "Выйти", 0.24f, btnSize, QuitGame);
    }

    void AddMenuButton(Transform parent, string objectName, string label, float anchorYFromBottom, Vector2 size, UnityAction onClick)
    {
        GameObject btnGo = CreateUiObject(objectName, parent);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, anchorYFromBottom);
        btnRt.anchorMax = new Vector2(0.5f, anchorYFromBottom);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = size;
        btnRt.anchoredPosition = Vector2.zero;

        Image btnBg = btnGo.AddComponent<Image>();
        btnBg.color = new Color(0.22f, 0.14f, 0.09f, 1f);

        GameObject rimGo = CreateUiObject("Rim", btnGo.transform);
        RectTransform rimRt = rimGo.GetComponent<RectTransform>();
        StretchFull(rimRt);
        rimRt.offsetMin = new Vector2(5, 5);
        rimRt.offsetMax = new Vector2(-5, -5);
        Image rim = rimGo.AddComponent<Image>();
        rim.color = new Color(0.5f, 0.38f, 0.24f, 1f);
        rim.raycastTarget = false;

        Button btn = btnGo.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.62f, 0.48f, 0.32f, 1f);
        colors.pressedColor = new Color(0.4f, 0.3f, 0.18f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        GameObject textGo = CreateUiObject("Label", btnGo.transform);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        StretchFull(textRt);
        textRt.offsetMin = new Vector2(10, 8);
        textRt.offsetMax = new Vector2(-10, -8);
        Text txt = textGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null)
        {
            try
            {
                txt.font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Calibri" }, 64);
            }
            catch
            {
                // ignored
            }
        }
        txt.fontSize = 34;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(0.94f, 0.86f, 0.68f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
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

    void LoadSplash(RawImage target)
    {
        string path = Path.Combine(Application.streamingAssetsPath, streamingAssetRelativePath);
        if (!File.Exists(path))
        {
            target.color = new Color(0.1f, 0.09f, 0.12f, 1f);
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length == 0)
        {
            target.color = new Color(0.1f, 0.09f, 0.12f, 1f);
            return;
        }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        if (!tex.LoadImage(bytes))
        {
            Destroy(tex);
            target.color = new Color(0.1f, 0.09f, 0.12f, 1f);
            return;
        }

        tex.Apply();
        target.texture = tex;
    }

    void LoadNextLevel()
    {
        SceneManager.LoadScene(nextSceneName);
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
