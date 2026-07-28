using GameLogic.Buffs;
using GameLogic.Item;
using Spine.Unity;
using UnityEngine;

namespace GameLogic.Player
{
    /// <summary>
    /// 玩家二维移动控制器。
    /// 负责读取输入、应用Buff移动速度、处理移动限制并驱动Spine动画和朝向。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BuffItemTrigger))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float INPUT_DEAD_ZONE = 0.01f;

        [Header("移动配置")]
        [Tooltip("移动速度属性为参考值时，对应的Unity世界移动速度")]
        [SerializeField, Min(0f)] private float baseWorldMoveSpeed = 4f;

        [Tooltip("Buff系统中的基础移动速度参考值")]
        [SerializeField, Min(1f)] private float referenceMoveSpeedAttribute = 300f;

        [Tooltip("开启后斜向移动不会比水平或垂直移动更快")]
        [SerializeField] private bool normalizeDiagonalMovement = true;

        [Header("Spine动画")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;

        [SerializeField] private string idleAnimationName = "Relax";

        [SerializeField] private string moveAnimationName = "Move";

        [Tooltip("Spine资源原始朝向是否面向右侧")]
        [SerializeField] private bool defaultFacingRight = true;

        private Rigidbody2D _rigidbody;
        private BuffItemTrigger _buffItemTrigger;
        private Vector2 _moveInput;
        private string _currentAnimationName;
        private float _absoluteSkeletonScaleX = 1f;

        public Vector2 MoveInput => _moveInput;

        public bool IsMoving => _moveInput.sqrMagnitude > INPUT_DEAD_ZONE * INPUT_DEAD_ZONE;

        private BuffUnit PlayerBuffUnit => _buffItemTrigger != null ? _buffItemTrigger.BuffUnit : null;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _buffItemTrigger = GetComponent<BuffItemTrigger>();

            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            ConfigureRigidbody();
            InitializeSkeleton();
        }

        private void Update()
        {
            ReadMoveInput();
            UpdateFacing();
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            _rigidbody.velocity = _moveInput * CalculateWorldMoveSpeed();
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
            _moveInput = Vector2.zero;
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
        }

        private void ConfigureRigidbody()
        {
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
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

        private void ReadMoveInput()
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (normalizeDiagonalMovement && input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            BuffUnit buffUnit = PlayerBuffUnit;
            _moveInput = buffUnit == null || buffUnit.CanMove ? input : Vector2.zero;
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
            if (skeletonAnimation == null || skeletonAnimation.Skeleton == null ||
                Mathf.Abs(_moveInput.x) <= INPUT_DEAD_ZONE)
            {
                return;
            }

            float inputDirection = _moveInput.x > 0f ? 1f : -1f;
            float defaultDirection = defaultFacingRight ? 1f : -1f;
            skeletonAnimation.Skeleton.ScaleX =
                _absoluteSkeletonScaleX * inputDirection * defaultDirection;
        }

        private void UpdateAnimation()
        {
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
