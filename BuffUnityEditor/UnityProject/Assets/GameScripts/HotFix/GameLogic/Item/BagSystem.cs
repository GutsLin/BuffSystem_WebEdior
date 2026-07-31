using System.Collections.Generic;
using GameLogic.Buffs;
using TEngine;

namespace GameLogic.Item
{
    public sealed class BagSystem : Singleton<BagSystem>
    {
        private readonly List<BagItemData> _items = new();
        private readonly Dictionary<string, int> _indexByKey = new();

        private static readonly string[] IconLocations =
        {
            "Icon_PoisonDoT",
            "Icon_StunDebuff",
            "Icon_BattleFury",
            "Icon_GuardianAura",
            "Icon_SpellShield",
            "Icon_Frostbite",
            "Icon_HasteRune",
            "Icon_ArcaneInsight",
            "Icon_RegenerationRune",
            "Icon_BerserkerStacks",
            "Icon_EtherealForm",
            "Icon_PurifyingLight",
            "Icon_DetonationMark",
            "Icon_ProtectiveLink",
            "Icon_BigDick",
        };

        private static readonly string[] DefaultBuffKeys =
        {
            "PoisonDoT",
            "StunDebuff",
            "BattleFury",
            "GuardianAura",
            "SpellShield",
            "Frostbite",
            "HasteRune",
            "ArcaneInsight",
            "RegenerationRune",
            "BerserkerStacks",
            "EtherealForm",
            "PurifyingLight",
            "DetonationMark",
            "ProtectiveLink",
            "BigDick",
        };

        public IReadOnlyList<BagItemData> Items => _items;

        public void InitDefaultItems(int count = 100)
        {
            _items.Clear();
            _indexByKey.Clear();

            foreach (var buffKey in DefaultBuffKeys)
            {
                if (!BuffSystemService.Instance.TryGetTemplate(buffKey, out BuffTemplate template))
                {
                    Log.Warning($"[BagSystem] Buff模板未找到: {buffKey}");
                    continue;
                }

                var data = new BagItemData
                {
                    BuffKey = buffKey,
                    DisplayName = template.DisplayName,
                    Description = template.Description,
                    IconLocation = GetIconLocation(buffKey),
                    Count = count,
                };
                _indexByKey[buffKey] = _items.Count;
                _items.Add(data);
            }

            Log.Info($"[BagSystem] 默认道具初始化完成: {_items.Count} 种 x{count}");
        }

        public void AddItem(string buffKey)
        {
            if (string.IsNullOrWhiteSpace(buffKey))
            {
                return;
            }

            if (!BuffSystemService.Instance.TryGetTemplate(buffKey, out BuffTemplate template))
            {
                Log.Warning($"[BagSystem] Buff模板未找到: {buffKey}");
                return;
            }

            if (_indexByKey.TryGetValue(buffKey, out int idx))
            {
                _items[idx].Count++;
            }
            else
            {
                var data = new BagItemData
                {
                    BuffKey = buffKey,
                    DisplayName = template.DisplayName,
                    Description = template.Description,
                    IconLocation = GetIconLocation(buffKey),
                    Count = 1,
                };
                _indexByKey[buffKey] = _items.Count;
                _items.Add(data);
            }

            int c = _items[_indexByKey[buffKey]].Count;
            GameEvent.Get<IBagEvent>().OnItemAdded(buffKey, c);
            Log.Info($"[BagSystem] 道具加入背包: {template.DisplayName} x{c}");
        }

        public bool UseItem(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            var data = _items[index];
            BuffUnit playerUnit = PlayerBuffUnitProvider.GetPlayerBuffUnit();
            if (playerUnit == null)
            {
                Log.Warning("[BagSystem] Player BuffUnit未就绪");
                return false;
            }

            var instance = playerUnit.ApplyBuff(data.BuffKey, playerUnit);
            if (instance == null)
            {
                Log.Warning($"[BagSystem] Buff施加失败: {data.BuffKey}");
                return false;
            }

            GameEvent.Get<IBagEvent>().OnItemUsed(data.BuffKey);
            Log.Info($"[BagSystem] 使用道具: {data.DisplayName} -> Buff: {data.BuffKey}");

            data.Count--;
            if (data.Count <= 0)
            {
                _items.RemoveAt(index);
                RebuildIndex();
            }

            return true;
        }

        public void Clear()
        {
            _items.Clear();
            _indexByKey.Clear();
        }

        private void RebuildIndex()
        {
            _indexByKey.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                _indexByKey[_items[i].BuffKey] = i;
            }
        }

        private static string GetIconLocation(string buffKey)
        {
            foreach (var loc in IconLocations)
            {
                if (string.Equals(loc, $"Icon_{buffKey}", System.StringComparison.OrdinalIgnoreCase))
                {
                    return loc;
                }
            }

            return "Icon_BigDick";
        }
    }
}
