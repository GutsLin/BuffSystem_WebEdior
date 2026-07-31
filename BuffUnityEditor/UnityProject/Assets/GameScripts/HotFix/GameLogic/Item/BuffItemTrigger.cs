using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Buffs;
using TEngine;
using UnityEngine;

namespace GameLogic.Item
{
    [RequireComponent(typeof(Collider2D))]
    public class BuffItemTrigger : MonoBehaviour
    {
        [Tooltip("角色队伍ID（用于光环等友军判定）")]
        [SerializeField] private int teamId = 1;

        [Tooltip("主属性类型")]
        [SerializeField] private PrimaryAttributeType primaryAttribute = PrimaryAttributeType.Strength;

        private BuffUnit _buffUnit;
        private bool _initialized;

        public BuffUnit BuffUnit => _buffUnit;

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

            PlayerBuffUnitProvider.SetPlayerBuffUnit(_buffUnit);
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

            BagSystem.Instance.AddItem(item.BuffKey);
            item.Pickup();
        }

        private void OnDestroy()
        {
            if (_buffUnit != null)
            {
                PlayerBuffUnitProvider.SetPlayerBuffUnit(null);
            }
            _buffUnit = null;
        }
    }
}
