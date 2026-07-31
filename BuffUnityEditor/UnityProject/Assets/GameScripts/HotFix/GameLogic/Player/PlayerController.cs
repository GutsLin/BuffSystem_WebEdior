using GameLogic.Buffs;
using GameLogic.Item;
using Spine.Unity;
using UnityEngine;

namespace GameLogic.Player
{
    /// <summary>
    /// 玩家横版移动控制器。
    /// 左右移动 + 跳跃，受重力影响，支持Buff移动速度和状态效果。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BuffItemTrigger))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float INPUT_DEAD_ZONE = 0.01f;
        private const float GROUND_CHECK_RADIUS = 0.25f;

        [Header("移动配置")]
        [Tooltip("移动速度属性为参考值时，对应的Unity世界移动速度")]
        [SerializeField, Min(0f)] private float baseWorldMoveSpeed = 4f;

        [Tooltip("Buff系统中的基础移动速度参考值")]
        [SerializeField, Min(1f)] private float referenceMoveSpeedAttribute = 300f;

        [Header("跳跃配置")]
        [Tooltip("跳跃力")]
        [SerializeField, Min(0f)] private float jumpForce = 7.5f;

        [Tooltip("空中可跳跃次数（含首次落地跳）")]
        [SerializeField, Range(1, 3)] private int maxJumpCount = 2;

        [Tooltip("地面检测点（空则用自身中心）")]
        [SerializeField] private Transform groundCheck;

        [Tooltip("地面检测半径")]
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.25f;

        [Tooltip("地面层Mask")]
        [SerializeField] private LayerMask groundLayerMask = 0;

        [Header("重力配置")]
        [Tooltip("重力缩放，1=默认物理重力")]
        [SerializeField, Min(0f)] private float gravityScale = 2.5f;

        [Tooltip("下落时额外重力倍率（更快下落手感）")]
        [SerializeField, Min(1f)] private float fallMultiplier = 1.5f;

        [Header("Spine动画")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;

        [SerializeField] private string idleAnimationName = "Relax";

        [SerializeField] private string moveAnimationName = "Move";

        [SerializeField] private string jumpAnimationName = "Jump";

        [Tooltip("Spine资源原始朝向是否面向右侧")]
        [SerializeField] private bool defaultFacingRight = true;

        private Rigidbody2D _rigidbody;
        private BuffItemTrigger _buffItemTrigger;
        private float _horizontalInput;
        private int _jumpCount;
        private bool _isGrounded;
        private bool _wasGrounded;
        private string _currentAnimationName;
        private float _absoluteSkeletonScaleX = 1f;

        private static readonly int DefaultGroundLayer = ~0;

        public bool IsMoving => Mathf.Abs(_horizontalInput) > INPUT_DEAD_ZONE;

        public bool IsGrounded => _isGrounded;

        private BuffUnit PlayerBuffUnit => _buffItemTrigger != null ? _buffItemTrigger.BuffUnit : null;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _buffItemTrigger = GetComponent<BuffItemTrigger>();

            if (groundCheck == null)
            {
                groundCheck = transform;
            }

            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            ConfigureRigidbody();
            InitializeSkeleton();
        }

        private void Update()
        {
            ReadInput();
            CheckGrounded();
            HandleJump();
            UpdateFacing();
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyFallMultiplier();
        }

        private void LateUpdate()
        {
            BuffUnit buffUnit = PlayerBuffUnit;
            if (buffUnit != null)
            {
                buffUnit.Position = transform.position;
            }
        }

        private void OnDisable()
        {
            _horizontalInput = 0f;
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
        }

        private void ConfigureRigidbody()
        {
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.gravityScale = gravityScale;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void InitializeSkeleton()
        {
            if (skeletonAnimation == null)
            {
                Debug.LogWarning("PlayerController未找到SkeletonAnimation组件。", this);
                return;
            }

            skeletonAnimation.Initialize(false);
            if (skeletonAnimation.Skeleton != null)
            {
                float scaleX = Mathf.Abs(skeletonAnimation.Skeleton.ScaleX);
                if (scaleX > INPUT_DEAD_ZONE)
                {
                    _absoluteSkeletonScaleX = scaleX;
                }
            }

            PlayAnimation(idleAnimationName);
        }

        private void ReadInput()
        {
            _horizontalInput = Input.GetAxisRaw("Horizontal");
        }

        private void CheckGrounded()
        {
            LayerMask mask = groundLayerMask != 0 ? groundLayerMask : DefaultGroundLayer;
            _wasGrounded = _isGrounded;
            _isGrounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius > 0.01f ? groundCheckRadius : GROUND_CHECK_RADIUS,
                mask);

            // 落地重置跳跃次数
            if (_isGrounded && !_wasGrounded)
            {
                _jumpCount = 0;
            }
        }

        private void HandleJump()
        {
            BuffUnit buffUnit = PlayerBuffUnit;
            bool canMove = buffUnit == null || buffUnit.CanMove;

            if (!canMove)
            {
                return;
            }

            // 跳跃键按下检测（GetButtonDown = 按下瞬间）
            if (Input.GetButtonDown("Jump"))
            {
                if (_isGrounded || _jumpCount < maxJumpCount)
                {
                    // 重置Y速度后施加跳跃力
                    Vector2 vel = _rigidbody.velocity;
                    vel.y = jumpForce;
                    _rigidbody.velocity = vel;
                    _jumpCount++;
                }
            }

            // 短跳：松开跳跃键时削减上升速度
            if (Input.GetButtonUp("Jump") && _rigidbody.velocity.y > 0f)
            {
                Vector2 vel = _rigidbody.velocity;
                vel.y *= 0.5f;
                _rigidbody.velocity = vel;
            }
        }

        private void ApplyMovement()
        {
            BuffUnit buffUnit = PlayerBuffUnit;
            bool canMove = buffUnit != null && !buffUnit.CanMove;

            float worldSpeed = CalculateWorldMoveSpeed();
            float targetVx = canMove ? 0f : _horizontalInput * worldSpeed;

            // 保持Y轴速度不变（重力/跳跃），仅覆盖X
            Vector2 vel = _rigidbody.velocity;
            vel.x = targetVx;
            _rigidbody.velocity = vel;
        }

        private void ApplyFallMultiplier()
        {
            // 下落时施加额外重力，提升手感
            if (_rigidbody.velocity.y < 0f)
            {
                _rigidbody.velocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private float CalculateWorldMoveSpeed()
        {
            BuffUnit buffUnit = PlayerBuffUnit;
            if (buffUnit == null)
            {
                return baseWorldMoveSpeed;
            }

            float moveSpeedAttribute = Mathf.Max(
                0f,
                buffUnit.GetAttribute(CombatAttributeNames.MoveSpeed));

            return baseWorldMoveSpeed * moveSpeedAttribute / referenceMoveSpeedAttribute;
        }

        private void UpdateFacing()
        {
            if (skeletonAnimation == null || skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (Mathf.Abs(_horizontalInput) <= INPUT_DEAD_ZONE)
            {
                return;
            }

            float inputDirection = _horizontalInput > 0f ? 1f : -1f;
            float defaultDirection = defaultFacingRight ? 1f : -1f;
            skeletonAnimation.Skeleton.ScaleX =
                _absoluteSkeletonScaleX * inputDirection * defaultDirection;
        }

        private void UpdateAnimation()
        {
            if (!_isGrounded)
            {
                PlayAnimation(jumpAnimationName);
                return;
            }

            PlayAnimation(IsMoving ? moveAnimationName : idleAnimationName);
        }

        private void PlayAnimation(string animationName)
        {
            if (skeletonAnimation == null || skeletonAnimation.AnimationState == null ||
                string.IsNullOrWhiteSpace(animationName) ||
                string.Equals(_currentAnimationName, animationName, System.StringComparison.Ordinal))
            {
                return;
            }

            skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
            _currentAnimationName = animationName;
        }
    }
}
