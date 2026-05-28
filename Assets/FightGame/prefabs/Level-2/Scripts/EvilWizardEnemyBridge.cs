using UnityEngine;

/// <summary>
/// Связка Evil Wizard с EnemyAI: поворот через flipX (не scale), дублирование бега в IsMoving.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EvilWizardEnemyBridge : MonoBehaviour
{
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    [SerializeField] float moveThreshold = 0.12f;

    EnemyAI _enemyAi;
    Rigidbody2D _rb;
    SpriteRenderer _sprite;
    Animator _animator;
    bool _hasIsMovingParam;

    void Awake()
    {
        _enemyAi = GetComponent<EnemyAI>();
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();

        if (_enemyAi != null)
            _enemyAi.flipByScale = false;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.nameHash == IsMovingHash)
                {
                    _hasIsMovingParam = true;
                    break;
                }
            }
        }
    }

    void LateUpdate()
    {
        if (_rb == null || _sprite == null) return;

        float vx = _rb.linearVelocity.x;
        bool moving = Mathf.Abs(vx) > moveThreshold;

        if (_hasIsMovingParam && _animator != null)
            _animator.SetBool(IsMovingHash, moving);

        if (Mathf.Abs(vx) > 0.02f)
            _sprite.flipX = vx < 0f;
    }
}
