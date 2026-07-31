using System.Collections.Generic;
using System.Text;
using GameLogic.Buffs;
using GameLogic.Item;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, "PlayerStatusUI")]
    public class PlayerStatusUI : UIWindow
    {
        private Text _textHp;
        private Text _textMp;
        private Text _textStats;
        private Text _textBuffs;
        private Text _textState;
        private Button _btnBag;

        private int _refreshTimerId = -1;

        private static readonly HashSet<string> StatusEffectNames = new()
        {
            "Stun", "Root", "Slow", "Silence", "Disarm", "Hex",
            "Fear", "Taunt", "Muted", "Break", "Poisoned",
            "DebuffImmune", "Invulnerable", "Ethereal",
        };

        private static readonly Dictionary<string, string> StatusDisplayNames = new()
        {
            { "Stun", "眩晕" }, { "Root", "禁锢" }, { "Slow", "减速" },
            { "Silence", "沉默" }, { "Disarm", "缴械" }, { "Hex", "妖术" },
            { "Fear", "恐惧" }, { "Taunt", "嘲讽" }, { "Muted", "禁用道具" },
            { "Break", "破坏" }, { "Poisoned", "中毒" },
            { "DebuffImmune", "减益免疫" }, { "Invulnerable", "无敌" }, { "Ethereal", "虚无" },
        };

        protected override void ScriptGenerator()
        {
            _textHp = FindChildComponent<Text>("StatsPanel/m_text_Hp");
            _textMp = FindChildComponent<Text>("StatsPanel/m_text_Mp");
            _textStats = FindChildComponent<Text>("StatsPanel/m_text_Stats");
            _textBuffs = FindChildComponent<Text>("BuffPanel/m_text_Buffs");
            _textState = FindChildComponent<Text>("BuffPanel/m_text_State");
            _btnBag = FindChildComponent<Button>("m_btn_Bag");
            if (_btnBag != null)
            {
                _btnBag.onClick.AddListener(OnBagClicked);
            }
            else
            {
                Log.Error("[PlayerStatusUI] m_btn_Bag not found in prefab!");
            }
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<BuffRuntimeEvent>(BuffEventIds.AnyChanged, OnBuffChanged);
        }

        protected override void OnCreate()
        {
            _refreshTimerId = GameModule.Timer.AddTimer(OnRefreshTimer, 0.1f, isLoop: true, isUnscaled: true);
        }

        protected override void OnRefresh()
        {
            RefreshView();
        }

        private void OnRefreshTimer(object[] args)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            BuffUnit unit = PlayerBuffUnitProvider.GetPlayerBuffUnit();
            if (unit == null)
            {
                return;
            }

            float maxHp = unit.GetAttribute(CombatAttributeNames.MaxHp);
            float maxMp = unit.GetAttribute(CombatAttributeNames.MaxMana);

            _textHp.text = $"<color=#72E39A>HP</color> {unit.CurrentHp:F0}/{maxHp:F0}";
            _textMp.text = $"<color=#69B8FF>MP</color> {unit.CurrentMana:F0}/{maxMp:F0}";

            var sb = new StringBuilder();
            sb.AppendLine($"<color=#FFD700>力量</color> {unit.GetAttribute(CombatAttributeNames.Strength):F0}");
            sb.AppendLine($"<color=#FFD700>敏捷</color> {unit.GetAttribute(CombatAttributeNames.Agility):F0}");
            sb.AppendLine($"<color=#FFD700>智力</color> {unit.GetAttribute(CombatAttributeNames.Intelligence):F0}");
            sb.AppendLine($"攻击 {unit.GetAttribute(CombatAttributeNames.AttackDamage):F0}");
            sb.AppendLine($"护甲 {unit.GetAttribute(CombatAttributeNames.Armor):F1}");
            sb.AppendLine($"魔抗 {unit.GetAttribute(CombatAttributeNames.MagicResistance):F0}%");
            sb.AppendLine($"移速 {unit.GetAttribute(CombatAttributeNames.MoveSpeed):F0}");
            sb.AppendLine($"攻速 {unit.GetAttribute(CombatAttributeNames.AttackSpeed):F0}");
            _textStats.text = sb.ToString();

            var buffSb = new StringBuilder();
            for (int i = 0; i < unit.ActiveBuffs.Count; i++)
            {
                var inst = unit.ActiveBuffs[i];
                if (inst.Template.IsHidden || inst.IsRemoved)
                {
                    continue;
                }

                string dot = inst.Template.ModifierKind == BuffModifierKind.Buff ? "<color=#72E39A>●</color>" : "<color=#FF8A82>●</color>";
                string stacks = inst.Stacks > 1 ? $" ×{inst.Stacks}" : "";
                string aura = inst.IsAuraProxy ? " [光环]" : "";
                string dur = inst.IsPermanent ? "永久" : $"{inst.RemainingDuration:F1}s";
                buffSb.AppendLine($"{dot} {inst.Template.DisplayName}{stacks} ({dur}){aura}");
            }
            _textBuffs.text = buffSb.Length > 0 ? buffSb.ToString() : "无活跃Buff";

            var stateSb = new StringBuilder();
            foreach (var status in StatusEffectNames)
            {
                if (unit.HasStatusEffect(status))
                {
                    stateSb.AppendLine(StatusDisplayNames.TryGetValue(status, out var name) ? name : status);
                }
            }
            _textState.text = stateSb.Length > 0 ? stateSb.ToString() : "正常";
        }

        private void OnBuffChanged(BuffRuntimeEvent evt)
        {
            RefreshView();
        }

        private void OnBagClicked()
        {
            if (GameModule.UI.HasWindow<BagUI>())
            {
                GameModule.UI.CloseUI<BagUI>();
            }
            else
            {
                GameModule.UI.ShowUIAsync<BagUI>();
            }
        }

        protected override void OnDestroy()
        {
            if (_btnBag != null)
            {
                _btnBag.onClick.RemoveListener(OnBagClicked);
            }
            if (_refreshTimerId >= 0)
            {
                GameModule.Timer.RemoveTimer(_refreshTimerId);
                _refreshTimerId = -1;
            }
        }
    }
}
