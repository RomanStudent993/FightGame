using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Шрифт Aventura — только ofont.ru_Aventura.ttf (скачанный с ofont.ru).</summary>
public static class GameFont
{
    const string ResourcesPath = "Fonts/ofont.ru_Aventura";
#if UNITY_EDITOR
    const string EditorSourcePath = "Assets/FightGame/prefabs/Menu/ofont.ru_Aventura.ttf";
    const string EditorResourcesPath = "Assets/FightGame/Resources/Fonts/ofont.ru_Aventura.ttf";
#endif

    static Font _aventura;
    static Font _builtInFallback;

    public static Font Aventura
    {
        get
        {
            if (_aventura != null) return _aventura;

#if UNITY_EDITOR
            _aventura = AssetDatabase.LoadAssetAtPath<Font>(EditorSourcePath);
            if (_aventura == null)
                _aventura = AssetDatabase.LoadAssetAtPath<Font>(EditorResourcesPath);
#endif

            if (_aventura == null)
                _aventura = Resources.Load<Font>(ResourcesPath);

            return _aventura;
        }
    }

    public static Font Default => Aventura;

    /// <summary>Unity built-in с кириллицей, если Aventura недоступен или без нужных глифов.</summary>
    public static Font BuiltInFallback
    {
        get
        {
            if (_builtInFallback != null) return _builtInFallback;
            _builtInFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtInFallback;
        }
    }

    /// <summary>Aventura, если в шрифте есть все символы строки; иначе встроенный fallback.</summary>
    public static Font ResolveForText(string text, int fontSize, FontStyle style = FontStyle.Normal)
    {
        if (string.IsNullOrEmpty(text))
            return Aventura ?? BuiltInFallback;

        Font primary = Aventura;
        if (primary != null)
        {
            RequestGlyphs(text, fontSize, style);
            bool allPresent = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;
                if (!primary.HasCharacter(c))
                {
                    allPresent = false;
                    break;
                }
            }

            if (allPresent) return primary;
        }

        Font fallback = BuiltInFallback;
        if (fallback != null)
            fallback.RequestCharactersInTexture(text, fontSize, style);
        return fallback;
    }

    public static void Reload()
    {
        _aventura = null;
        _builtInFallback = null;
    }

    public static void RequestGlyphs(string text, int fontSize, FontStyle style = FontStyle.Normal)
    {
        Font font = Aventura;
        if (font == null || string.IsNullOrEmpty(text)) return;

        const int chunkSize = 120;
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            int len = Mathf.Min(chunkSize, text.Length - i);
            font.RequestCharactersInTexture(text.Substring(i, len), fontSize, style);
        }
    }

    public static void RequestGlyphs(string text, int fontSizeA, int fontSizeB, FontStyle style = FontStyle.Normal)
    {
        RequestGlyphs(text, fontSizeA, style);
        if (fontSizeB != fontSizeA)
            RequestGlyphs(text, fontSizeB, style);
    }
}
