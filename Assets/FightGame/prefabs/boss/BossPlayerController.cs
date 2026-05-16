using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossPlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float runAnimSpeed = 2f;
    [SerializeField] bool useADKeys = true;
    [SerializeField] bool flipByNegativeScaleX = true;
    [SerializeField] float groundSnapRayDistance = 3f;
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] bool stabilizeRunSprites = true;

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");

    Rigidbody2D _rb;
    Animator _animator;
    SpriteRenderer _animSprite;
    SpriteRenderer _sprite;
    Transform _visual;
    BoxCollider2D _collider;
    float _inputX;
    bool _wasMoving;
    bool _runAnchorReady;
    float _anchorCenterX;
    float _anchorFeetY;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _animSprite = GetComponent<SpriteRenderer>();
        _collider = GetComponent<BoxCollider2D>();
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        SetupVisualRenderer();
        ApplyRunAnimSpeed();
    }

    void Start()
    {
        AlignColliderToSprite();
        SnapFeetToGround();
        CacheAnchorFromSprite(_animSprite.sprite);
    }

    void OnValidate()
    {
        ApplyRunAnimSpeed();
    }

    void ApplyRunAnimSpeed()
    {
        if (_animator != null)
            _animator.SetFloat(RunSpeedHash, Mathf.Max(0.1f, runAnimSpeed));
    }

    void FixedUpdate()
    {
        _inputX = ReadHorizontalInput();
        bool moving = Mathf.Abs(_inputX) > 0.01f;
        _animator.SetBool(IsMovingHash, moving);

        float vx = _inputX * moveSpeed;
        _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
    }

    void Update()
    {
        ApplyFacing(_inputX);
    }

    void LateUpdate()
    {
        SyncVisibleSprite();

        bool moving = _animator.GetBool(IsMovingHash);
        if (moving != _wasMoving)
        {
            _wasMoving = moving;
            _runAnchorReady = false;
            if (!moving)
                CacheAnchorFromSprite(_animSprite.sprite);
        }

        if (stabilizeRunSprites && moving)
        {
            if (!_runAnchorReady)
            {
                CacheAnchorFromSprite(_animSprite.sprite);
                _runAnchorReady = true;
            }

            StabilizeVisual();
        }
        else if (_visual != null)
        {
            _visual.localPosition = Vector3.zero;
        }
    }

    void SetupVisualRenderer()
    {
        if (_animSprite == null)
            return;

        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(transform, false);
        _visual = visualGo.transform;
        _sprite = visualGo.AddComponent<SpriteRenderer>();

        _sprite.color = _animSprite.color;
        _sprite.sortingLayerID = _animSprite.sortingLayerID;
        _sprite.sortingOrder = _animSprite.sortingOrder;
        _sprite.material = _animSprite.sharedMaterial;
        _sprite.drawMode = _animSprite.drawMode;
        _sprite.maskInteraction = _animSprite.maskInteraction;
        _sprite.spriteSortPoint = _animSprite.spriteSortPoint;

        _animSprite.enabled = false;
    }

    void SyncVisibleSprite()
    {
        if (_sprite == null || _animSprite == null)
            return;

        _sprite.sprite = _animSprite.sprite;
        _sprite.flipX = _animSprite.flipX;
        _sprite.flipY = _animSprite.flipY;
    }

    void CacheAnchorFromSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        Bounds b = sprite.bounds;
        _anchorCenterX = b.center.x;
        _anchorFeetY = b.min.y;
    }

    void StabilizeVisual()
    {
        if (_visual == null || _sprite.sprite == null)
            return;

        Bounds b = _sprite.sprite.bounds;
        float dx = _anchorCenterX - b.center.x;
        float dy = _anchorFeetY - b.min.y;
        _visual.localPosition = new Vector3(dx, dy, 0f);
    }

    float ReadHorizontalInput()
    {
        if (useADKeys)
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.A))
                x -= 1f;
            if (Input.GetKey(KeyCode.D))
                x += 1f;
            return x;
        }

        return Input.GetAxisRaw("Horizontal");
    }

    void ApplyFacing(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f)
            return;

        if (_animSprite != null)
        {
            _animSprite.flipX = dirX < 0f;
            return;
        }

        if (!flipByNegativeScaleX)
        {
            Vector3 s = transform.localScale;
            float mag = Mathf.Abs(s.x);
            if (mag < 1e-3f)
                mag = 1f;
            transform.localScale = new Vector3(dirX > 0f ? mag : -mag, s.y, s.z);
            return;
        }

        Vector3 scale = transform.localScale;
        float magX = Mathf.Abs(scale.x);
        if (magX < 1e-3f)
            magX = 1f;
        float magY = Mathf.Abs(scale.y);
        float magZ = Mathf.Abs(scale.z);
        if (magY < 1e-3f) magY = 1f;
        if (magZ < 1e-3f) magZ = 1f;
        transform.localScale = new Vector3(dirX > 0f ? -magX : magX, magY, magZ);
    }

    void AlignColliderToSprite()
    {
        Sprite sprite = _animSprite != null ? _animSprite.sprite : null;
        if (_collider == null || sprite == null)
            return;

        Bounds b = sprite.bounds;
        _collider.offset = new Vector2(b.center.x, b.extents.y);
        _collider.size = new Vector2(b.size.x * 0.45f, b.size.y * 0.95f);
    }

    void SnapFeetToGround()
    {
        if (_sprite == null || _sprite.sprite == null)
            return;

        Bounds b = _sprite.bounds;
        var origin = new Vector2(b.center.x, b.min.y + 0.05f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundSnapRayDistance, groundLayers);
        if (!hit.collider)
            return;

        float deltaY = hit.point.y - b.min.y;
        if (Mathf.Abs(deltaY) < 0.0001f)
            return;

        var pos = transform.position;
        pos.y += deltaY;
        transform.position = pos;
        _rb.position = new Vector2(_rb.position.x, pos.y);
    }
}
