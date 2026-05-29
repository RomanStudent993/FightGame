using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Спрайты меню для рантайм-UI (пауза, смерть) — в билде только из Resources.</summary>
public static class MenuUiAssets
{
    const string MainResourcePath = "Menu/main";
    const string MainSpriteResourcePath = "Menu/main_0";
#if UNITY_EDITOR
    const string EditorMainPath = "Assets/FightGame/Resources/Menu/main.png";
    const string EditorMainFallbackPath = "Assets/FightGame/prefabs/Menu/main.png";
#endif

    static Sprite _mainBackground;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PreloadMainBackground()
    {
        GetMainBackground();
    }

    public static Sprite GetMainBackground()
    {
        if (_mainBackground != null)
            return _mainBackground;

        _mainBackground = Resources.Load<Sprite>(MainResourcePath);
        if (_mainBackground != null)
            return _mainBackground;

        _mainBackground = Resources.Load<Sprite>(MainSpriteResourcePath);
        if (_mainBackground != null)
            return _mainBackground;

        Sprite[] sprites = Resources.LoadAll<Sprite>(MainResourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                if (sprites[i].name == "main_0" || sprites[i].name == "main")
                {
                    _mainBackground = sprites[i];
                    return _mainBackground;
                }
            }

            _mainBackground = sprites[0];
            return _mainBackground;
        }

#if UNITY_EDITOR
        _mainBackground = LoadSpriteFromAssetPath(EditorMainPath);
        if (_mainBackground == null)
            _mainBackground = LoadSpriteFromAssetPath(EditorMainFallbackPath);
#endif

        return _mainBackground;
    }

#if UNITY_EDITOR
    static Sprite LoadSpriteFromAssetPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null)
            return null;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return null;
    }
#endif
}
