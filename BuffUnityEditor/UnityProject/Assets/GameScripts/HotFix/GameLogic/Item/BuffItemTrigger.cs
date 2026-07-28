using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Buffs;
using TEngine;
using UnityEngine;

namespace GameLogic.Item
{
    /// <summary>
    /// 通用Buff道具触发器，挂在Player上。
    /// 碰撞到带有BuffItem组件的道具时，自动施加对应Buff。
    /// 要求：Player需有Collider2D（非Trigger），道具需有Collider2D（Trigger）。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BuffItemTrigger : MonoBehaviour
    {
        [Tooltip("角色队伍ID（用于光环等友军判定）")]
        [SerializeField] private int teamId = 1;

        [Tooltip("主属性类型")]
        [SerializeField] private PrimaryAttributeType primaryAttribute = PrimaryAttributeType.Strength;

        private BuffUnit _buffUnit;
        private bool _initialized;

        /// <summary>
        /// 当前玩家对应的Buff运行时单位。
        /// </summary>
        public BuffUnit BuffUnit => _buffUnit;

        /// <summary>
        /// Buff运行时单位是否已经创建完成。
        /// </summary>
        public bool IsInitialized => _initialized && _buffUnit != null;

        private async UniTaskVoid Start()
        {
            await BuffSystemService.Instance.InitializeAsync();

            var baseAttributes = new Dictionary<string, float>
            {
                [CombatAttributeNames.Strength] = 20f,
                [CombatAttributeNames.Agility] = 15f,
                [CombatAttributeNames.Intelligence] = 18f,
                [CombatAttributeNames.MaxHp] = 120f,
                [CombatAttributeNames.MaxMana] = 75f,
                [CombatAttributeNames.AttackDamage] = 28f,
                [CombatAttributeNames.Armor] = 1f,
                [CombatAttributeNames.AttackSpeed] = 100f,
                [CombatAttributeNames.MoveSpeed] = 300f,
            };

            _buffUnit = BuffSystemService.Instance.CreateUnit(
                gameObject.name,
                primaryAttribute,
                teamId,
                baseAttributes);

            _initialized = true;
            Log.Info($"[BuffItemTrigger] Player BuffUnit已创建: {_buffUnit.Id}");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _buffUnit == null)
            {
                return;
            }

            if (!other.TryGetComponent<BuffItem>(out var item))
            {
                return;
            }

            var instance = _buffUnit.ApplyBuff(item.BuffKey, _buffUnit);
            if (instance != null)
            {
                Log.Info($"[BuffItemTrigger] 拾取道具，施加Buff: {item.BuffKey}");
            }
            else
            {
                Log.Warning($"[BuffItemTrigger] Buff施加失败（可能被减益免疫阻挡）: {item.BuffKey}");
            }

            item.Pickup();
        }

        private void OnDestroy()
        {
            _buffUnit = null;
        }
    }
}
