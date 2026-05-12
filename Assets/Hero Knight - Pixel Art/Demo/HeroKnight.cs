using UnityEngine;
using System.Collections;

public class HeroKnight : MonoBehaviour {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 7.5f;
    [SerializeField] float      m_rollForce = 6.0f;
    [SerializeField] bool       m_noBlood = false;
    [SerializeField] GameObject m_slideDust;
    [SerializeField] LayerMask  m_environmentMask = ~0;
    [SerializeField] float      m_airWallCastDistance = 0.22f;
    [SerializeField] float      m_airStuckFallAssist = -3.5f;
    [SerializeField] float      m_airStuckTime = 0.06f;
    [SerializeField] int        m_healAmount = 65;
    [Tooltip("Сдвиг только графики вверх во время Heal (юниты мира). Коллайдер и Rigidbody не двигаются.")]
    [SerializeField] float      m_healVisualWorldOffsetY = 0.1f;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private Collider2D          m_bodyCollider;
    /// <summary>Корневой SR: сюда пишет Animator (не рисуется).</summary>
    private SpriteRenderer      m_spriteDrive;
    /// <summary>Дочерний SR: копия спрайта, видимый; сдвигается по Y только в Heal.</summary>
    private SpriteRenderer      m_spriteVisual;
    private Transform           m_spriteVisualTransform;
    private Sensor_HeroKnight   m_groundSensor;
    private bool                m_grounded = false;
    private bool                m_rolling = false;
    private int                 m_facingDirection = 1;
    private int                 m_currentAttack = 0;
    private float               m_timeSinceAttack = 0.0f;
    private float               m_delayToIdle = 0.0f;
    private float               m_rollDuration = 8.0f / 14.0f;
    private float               m_rollCurrentTime;
    private PlayerCombatKnockback m_hitKnockback;
    private SimpleHealth          m_health;
    private int                   m_healQueue;
    private bool                  m_healProcessorRunning;
    private float                 m_airBlockedHorizTimer;
    private float                 m_inputX;
    private readonly RaycastHit2D[] m_castHits = new RaycastHit2D[12];
    private ContactFilter2D       m_envContactFilter;


    void Start ()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_bodyCollider = GetComponent<Collider2D>();
        m_spriteDrive = GetComponent<SpriteRenderer>();
        SetupSpriteVisualCopy();
        m_hitKnockback = GetComponent<PlayerCombatKnockback>();
        m_health = GetComponent<SimpleHealth>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
        m_animator.SetBool("WallSlide", false);
        m_body2d.sleepMode = RigidbodySleepMode2D.NeverSleep;
        m_envContactFilter.useTriggers = false;
        m_envContactFilter.useLayerMask = true;
        m_envContactFilter.SetLayerMask(m_environmentMask);
    }

    void Update ()
    {
        m_inputX = Input.GetAxis("Horizontal");

        m_timeSinceAttack += Time.deltaTime;

        if(m_rolling)
            m_rollCurrentTime += Time.deltaTime;

        if(m_rollCurrentTime > m_rollDuration)
            m_rolling = false;

        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        if (m_inputX > 0)
        {
            if (m_spriteDrive != null) m_spriteDrive.flipX = false;
            m_facingDirection = 1;
        }
        else if (m_inputX < 0)
        {
            if (m_spriteDrive != null) m_spriteDrive.flipX = true;
            m_facingDirection = -1;
        }

        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);
        m_animator.SetBool("WallSlide", false);

        if (Input.GetKeyDown("e") && !m_rolling)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            if (m_health != null && !m_health.IsDead)
            {
                m_healQueue++;
                if (!m_healProcessorRunning)
                    StartCoroutine(ProcessHealQueue());
            }
        }
        else if(Input.GetMouseButtonDown(0) && m_timeSinceAttack > 0.25f && !m_rolling)
        {
            m_currentAttack++;
            if (m_currentAttack > 3)
                m_currentAttack = 1;
            if (m_timeSinceAttack > 1.0f)
                m_currentAttack = 1;
            m_animator.SetTrigger("Attack" + m_currentAttack);
            m_timeSinceAttack = 0.0f;
        }
        else if (Input.GetMouseButtonDown(1) && !m_rolling)
        {
            m_animator.SetTrigger("Block");
            m_animator.SetBool("IdleBlock", true);
        }
        else if (Input.GetMouseButtonUp(1))
            m_animator.SetBool("IdleBlock", false);
        else if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) && !m_rolling)
        {
            m_facingDirection = FacingSignFromSprite();
            m_rolling = true;
            m_rollCurrentTime = 0f;
            m_animator.SetTrigger("Roll");
            m_body2d.linearVelocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.linearVelocity.y);
        }
        else if (Input.GetKeyDown("space") && m_grounded && !m_rolling)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }
        else if (Mathf.Abs(m_inputX) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if(m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }

    void FixedUpdate()
    {
        if (m_rolling)
            return;
        if (m_hitKnockback != null && m_hitKnockback.IsHorizontalInputSuppressed)
            return;

        m_body2d.WakeUp();

        float targetVx = m_inputX * m_speed;
        float vy = m_body2d.linearVelocity.y;

        if (!m_grounded)
        {
            if (m_bodyCollider != null && Mathf.Abs(targetVx) > 0.02f)
            {
                float sx = Mathf.Sign(targetVx);
                if (HorizontalCastBlocks(sx))
                    targetVx = 0f;
            }

            if (Mathf.Abs(m_inputX) > 0.18f && Mathf.Abs(targetVx) < 0.01f)
            {
                m_airBlockedHorizTimer += Time.fixedDeltaTime;
                if (m_airBlockedHorizTimer >= m_airStuckTime)
                    vy = Mathf.Min(vy, m_airStuckFallAssist);
            }
            else
                m_airBlockedHorizTimer = 0f;
        }
        else
            m_airBlockedHorizTimer = 0f;

        m_body2d.linearVelocity = new Vector2(targetVx, vy);
    }

    bool HorizontalCastBlocks(float signX)
    {
        if (m_bodyCollider == null) return false;
        Vector2 dir = new Vector2(signX, 0f);
        int n = m_bodyCollider.Cast(dir, m_envContactFilter, m_castHits, m_airWallCastDistance);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = m_castHits[i].collider;
            if (c == null) continue;
            if (c.transform.root == transform.root) continue;
            return true;
        }
        return false;
    }

    /// <summary>Горизонтальное направление «взгляда» персонажа: +1 вправо, −1 влево (как у PlayerShieldDefense).</summary>
    int FacingSignFromSprite()
    {
        if (m_spriteDrive != null)
            return m_spriteDrive.flipX ? -1 : 1;
        return m_facingDirection;
    }

    void Awake()
    {
        DisableWallSlideOnThisHierarchy();
    }

    void DisableWallSlideOnThisHierarchy()
    {
        DisableIfPresent(transform, "WallSensor_R1");
        DisableIfPresent(transform, "WallSensor_R2");
        DisableIfPresent(transform, "WallSensor_L1");
        DisableIfPresent(transform, "WallSensor_L2");
    }

    static void DisableIfPresent(Transform root, string childName)
    {
        Transform c = root.Find(childName);
        if (c != null)
            c.gameObject.SetActive(false);
    }

    void SetupSpriteVisualCopy()
    {
        if (m_spriteDrive == null) return;

        GameObject visGo = new GameObject("SpriteDraw");
        visGo.transform.SetParent(transform, false);
        visGo.transform.localPosition = Vector3.zero;
        visGo.transform.localRotation = Quaternion.identity;
        visGo.transform.localScale = Vector3.one;
        m_spriteVisualTransform = visGo.transform;
        m_spriteVisual = visGo.AddComponent<SpriteRenderer>();
        m_spriteVisual.sharedMaterial = m_spriteDrive.sharedMaterial;
        m_spriteVisual.sortingLayerID = m_spriteDrive.sortingLayerID;
        m_spriteVisual.sortingOrder = m_spriteDrive.sortingOrder;
        m_spriteVisual.maskInteraction = m_spriteDrive.maskInteraction;
        m_spriteVisual.spriteSortPoint = m_spriteDrive.spriteSortPoint;
        m_spriteVisual.drawMode = m_spriteDrive.drawMode;
        m_spriteVisual.size = m_spriteDrive.size;
        m_spriteVisual.color = m_spriteDrive.color;
        m_spriteVisual.sprite = m_spriteDrive.sprite;
        m_spriteVisual.flipX = m_spriteDrive.flipX;
        m_spriteDrive.forceRenderingOff = true;
    }

    void LateUpdate()
    {
        if (m_spriteDrive == null || m_spriteVisual == null) return;

        m_spriteVisual.sprite = m_spriteDrive.sprite;
        m_spriteVisual.flipX = m_spriteDrive.flipX;
        m_spriteVisual.color = m_spriteDrive.color;
        m_spriteVisual.drawMode = m_spriteDrive.drawMode;
        m_spriteVisual.size = m_spriteDrive.size;

        float y = 0f;
        if (m_animator != null)
        {
            AnimatorStateInfo s = m_animator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName("Heal"))
                y = m_healVisualWorldOffsetY;
        }

        m_spriteVisualTransform.localPosition = new Vector3(0f, y, 0f);
    }

    IEnumerator ProcessHealQueue()
    {
        m_healProcessorRunning = true;
        while (m_healQueue > 0)
        {
            if (m_health == null || m_health.IsDead)
            {
                m_healQueue = 0;
                break;
            }

            while (m_rolling)
                yield return null;

            m_healQueue--;
            yield return CoHealOne();
        }

        m_healProcessorRunning = false;
    }

    IEnumerator CoHealOne()
    {
        m_animator.ResetTrigger("Heal");
        m_animator.SetTrigger("Heal");
        yield return null;

        const float timeout = 20f;
        float elapsed = 0f;
        bool sawHeal = false;

        while (elapsed < timeout)
        {
            if (m_health == null || m_health.IsDead)
                break;

            AnimatorStateInfo s = m_animator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName("Heal"))
            {
                sawHeal = true;
                if (s.normalizedTime >= 0.99f)
                    break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sawHeal && m_health != null && !m_health.IsDead)
            m_health.RestoreHp(m_healAmount);

        m_animator.ResetTrigger("Heal");
    }

    void AE_SlideDust()
    {
        if (m_slideDust == null) return;
        Vector3 spawnPosition = transform.position + new Vector3(0.25f * m_facingDirection, 0.4f, 0f);
        GameObject dust = Instantiate(m_slideDust, spawnPosition, gameObject.transform.localRotation) as GameObject;
        dust.transform.localScale = new Vector3(m_facingDirection, 1, 1);
    }
}
