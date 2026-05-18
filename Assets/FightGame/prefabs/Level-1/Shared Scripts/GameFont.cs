using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Шрифт Aventura — только из Resources/Fonts/Aventura.ttf (без LegacyRuntime).</summary>
public static class GameFont
{
    const string ResourcesPath = "Fonts/Aventura";
#if UNITY_EDITOR
    const string EditorAssetPath = "Assets/FightGame/Resources/Fonts/Aventura.ttf";
    const string EditorMenuAssetPath = "Assets/FightGame/prefabs/Menu/ofont.ru_Aventura.ttf";
#endif

    static Font _aventura;

    public static Font Aventura
    {
        get
        {
            if (_aventura != null) return _aventura;

            _aventura = Resources.Load<Font>(ResourcesPath);
#if UNITY_EDITOR
            if (_aventura == null)
                _aventura = AssetDatabase.LoadAssetAtPath<Font>(EditorAssetPath);
            if (_aventura == null)
                _aventura = AssetDatabase.LoadAssetAtPath<Font>(EditorMenuAssetPath);
#endif
            return _aventura;
        }
    }

    public static Font Default => Aventura;

    public static void Reload()
    {
        _aventura = null;
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
