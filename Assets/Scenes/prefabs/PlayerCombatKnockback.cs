using UnityEngine;

/// <summary>
/// Толчок в физике: часть сдвига через MovePosition, остаток через скорость за время suppress (меньше «телепорта»).
/// </summary>
public class PlayerCombatKnockback : MonoBehaviour
{
    Rigidbody2D rb;
    float horizontalInputSuppressedUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public bool IsHorizontalInputSuppressed => Time.time < horizontalInputSuppressedUntil;

    /// <param name="velocityX">Горизонтальная скорость (уже со знаком).</param>
    /// <param name="instantSlideDistance">Полный сдвиг по X; при portion &lt; 1 только часть мгновенно, остальное даётся скоростью за suppress.</param>
    /// <param name="instantSlidePortion">1 = весь сдвиг за один кадр; 0.2 ≈ 20% рывок, 80% — плавнее за время suppress.</param>
    public void ApplyKnockback(float velocityX, float suppressHorizontalInputDuration, float instantSlideDistance, float instantSlidePortion = 1f)
    {
        if (rb == null) return;

        float sign = Mathf.Abs(velocityX) > 0.001f ? Mathf.Sign(velocityX) : 1f;
        instantSlidePortion = Mathf.Clamp01(instantSlidePortion);
        float instant = instantSlideDistance * instantSlidePortion;
        float deferred = instantSlideDistance * (1f - instantSlidePortion);
        float t = Mathf.Max(suppressHorizontalInputDuration, 0.02f);
        float addV = deferred / t;
        float vx = sign * (Mathf.Abs(velocityX) + addV);

        if (instant > 0f)
            rb.MovePosition(rb.position + new Vector2(sign * instant, 0f));

        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);

        float until = Time.time + suppressHorizontalInputDuration;
        if (until > horizontalInputSuppressedUntil)
            horizontalInputSuppressedUntil = until;
    }
}
