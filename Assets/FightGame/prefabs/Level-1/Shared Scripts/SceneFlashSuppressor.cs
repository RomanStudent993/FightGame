using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Скрывает пол/колонны на кадр при смерти, загрузке и интро боя.</summary>
public static class SceneFlashSuppressor
{
    static readonly string[] HiddenObjectNames = { "floor", "floor (1)", "Square", "Square (1)" };

    static readonly List<SpriteRenderer> HiddenRenderers = new List<SpriteRenderer>();
    static GameObject _blackoutRoot;

    public static void HideGameplayStrip()
    {
        for (int i = 0; i < HiddenRenderers.Count; i++)
        {
            SpriteRenderer renderer = HiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        HiddenRenderers.Clear();

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!ShouldHide(renderer.gameObject.name))
                continue;

            renderer.enabled = false;
            HiddenRenderers.Add(renderer);
        }
    }

    public static void RestoreGameplayStrip()
    {
        for (int i = 0; i < HiddenRenderers.Count; i++)
        {
            SpriteRenderer renderer = HiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        HiddenRenderers.Clear();
    }

    public static void ShowBlackout()
    {
        if (_blackoutRoot != null)
            return;

        _blackoutRoot = new GameObject("SceneTransitionBlackout");
        Object.DontDestroyOnLoad(_blackoutRoot);

        Canvas canvas = _blackoutRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        _blackoutRoot.AddComponent<CanvasScaler>();
        _blackoutRoot.AddComponent<GraphicRaycaster>();

        RectTransform rt = _blackoutRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image black = _blackoutRoot.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
    }

    public static void HideBlackout()
    {
        if (_blackoutRoot == null)
            return;

        Object.Destroy(_blackoutRoot);
        _blackoutRoot = null;
    }

    static bool ShouldHide(string objectName)
    {
        for (int i = 0; i < HiddenObjectNames.Length; i++)
        {
            if (objectName == HiddenObjectNames[i])
                return true;
        }

        return false;
    }
}
