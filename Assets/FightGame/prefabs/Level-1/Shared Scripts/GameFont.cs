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
