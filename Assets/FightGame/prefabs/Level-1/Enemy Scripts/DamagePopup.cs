using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Красная надпись «-25» над персонажем при получении урона. Шрифт подбирается в рантайме (встроенный / системный).
/// </summary>
public class DamagePopup : MonoBehaviour
{
    const int kFontSize = 42;
    const float kFloatDistance = 0.75f;
    const float kDuration = 0.85f;

    static Font _cachedFont;

    CanvasGroup _group;
    Vector3 _startPos;

    public static void Show(Vector3 worldPosition, int damage)
    {
        if (damage <= 0) return;

        worldPosition += new Vector3(Random.Range(-0.12f, 0.12f), 0f, 0f);

        GameObject root = new GameObject("DamagePopup");
        root.transform.position = worldPosition;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        RectTransform canvasRt = root.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(220f, 80f);
        canvasRt.localScale = Vector3.one * 0.012f;

        GameObject textGo = new GameObject("Value");
        textGo.transform.SetParent(root.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.text = "-" + damage;
        text.color = new Color(0.95f, 0.15f, 0.12f, 1f);
        text.fontSize = 42;
        text.alignment = TextAnchor.MiddleCenter;
        Font font = ResolveFont();
        text.font = font;
        GameFont.RequestGlyphs(text.text, kFontSize, FontStyle.Bold);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;

        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        DamagePopup popup = root.AddComponent<DamagePopup>();
        popup._group = group;
        popup._startPos = worldPosition;
        text.fontSize = kFontSize;
        popup.StartCoroutine(popup.Run());
    }

    static Font ResolveFont()
    {
        if (_cachedFont != null) return _cachedFont;
        _cachedFont = GameFont.Default;
        return _cachedFont;
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < kDuration && _group != null)
        {
            t += Time.deltaTime;
            float u = t / kDuration;
            transform.position = _startPos + Vector3.up * (u * kFloatDistance);
            _group.alpha = 1f - u * u;
            yield return null;
        }
        Destroy(gameObject);
    }

    void LateUpdate()
    {
        FaceCamera();
    }

    void FaceCamera()
    {
        if (Camera.main == null) return;
        transform.rotation = Camera.main.transform.rotation;
    }
}
