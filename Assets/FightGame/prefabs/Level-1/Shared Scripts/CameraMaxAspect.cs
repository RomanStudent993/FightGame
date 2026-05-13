using UnityEngine;

/// <summary>
/// Ограничивает горизонтальное поле зрения: при окне шире, чем maxAspect, включается pillarbox
/// (как при фиксированном 16:9), чтобы по краям не «подтягивалась» лишняя сцена.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraMaxAspect : MonoBehaviour
{
    [SerializeField] float maxWidthOverHeight = 16f / 9f;

    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void OnDisable()
    {
        if (_cam != null)
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    void LateUpdate()
    {
        if (_cam == null) return;

        float h = Mathf.Max(1, Screen.height);
        float aspect = Screen.width / h;
        if (aspect <= maxWidthOverHeight)
        {
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        float wNorm = maxWidthOverHeight / aspect;
        float x = (1f - wNorm) * 0.5f;
        _cam.rect = new Rect(x, 0f, wNorm, 1f);
    }
}
