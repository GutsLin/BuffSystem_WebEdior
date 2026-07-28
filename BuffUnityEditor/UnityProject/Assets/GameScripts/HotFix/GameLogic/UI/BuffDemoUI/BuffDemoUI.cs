using System;
using System.Collections.Generic;
using System.Text;
using GameLogic.Buffs;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// Buff 系统运行时演示面板。
    /// </summary>
    [Window(UILayer.UI, location: "BuffDemoUI", fullScreen: true)]
    public sealed class BuffDemoUI : UIWindow
    {
        private const string HERO_UNIT_ID = "buff-demo-hero";
        private const string ENEMY_UNIT_ID = "buff-demo-enemy";
        private const int MAX_LOG_COUNT = 18;

        private readonly List<string> _logs = new List<string>();
        private readonly HashSet<string> _statusCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _builder = new StringBuilder(1024);

        // 界面中的动态文本组件
        private Text _textConfigStatus;
        private Text _textHeroStats;
        private Text _textHeroState;
        private Text _textHeroBuffs;
        private Text _textEnemyStats;
        private Text _textEnemyBuffs;
        private Text _textLog;

        // 界面中的操作按钮
        private Button _btnBattleFury;
        private Button _btnBerserker;
        private Button _btnGuardianAura;
        private Button _btnSpellShield;
        private Button _btnPoison;
        private Button _btnFrostbite;
        private Button _btnDetonation;
        private Button _btnPurify;
        private Button _btnAttackEnemy;
        private Button _btnEnemyMagic;
        private Button _btnHealHero;
        private Button _btnDispelHero;
        private Button _btnClearAll;
        private Button _btnReset;

        // 演示使用的英雄和敌方单位
        private BuffUnit _hero;
        private BuffUnit _enemy;

        // 定时刷新剩余时间和实时属性
        private int _refreshTimerId = -1;

        // 重置和销毁过程中禁止处理运行时事件
        private bool _suppressEvents;
        private bool _isDestroying;

        // 绑定预制体中的文本和按钮节点
        protected override void ScriptGenerator()
        {
            _textConfigStatus = FindChildComponent<Text>("Header/m_text_ConfigStatus");
            _textHeroStats = FindChildComponent<Text>("HeroPanel/m_text_HeroStats");
            _textHeroState = FindChildComponent<Text>("HeroPanel/m_text_HeroState");
            _textHeroBuffs = FindChildComponent<Text>("HeroPanel/m_text_HeroBuffs");
            _textEnemyStats = FindChildComponent<Text>("EnemyPanel/m_text_EnemyStats");
            _textEnemyBuffs = FindChildComponent<Text>("EnemyPanel/m_text_EnemyBuffs");
            _textLog = FindChildComponent<Text>("EnemyPanel/m_text_Log");

            _btnBattleFury = FindChildComponent<Button>("ActionPanel/m_btn_BattleFury");
            _btnBerserker = FindChildComponent<Button>("ActionPanel/m_btn_Berserker");
            _btnGuardianAura = FindChildComponent<Button>("ActionPanel/m_btn_GuardianAura");
            _btnSpellShield = FindChildComponent<Button>("ActionPanel/m_btn_SpellShield");
            _btnPoison = FindChildComponent<Button>("ActionPanel/m_btn_Poison");
            _btnFrostbite = FindChildComponent<Button>("ActionPanel/m_btn_Frostbite");
            _btnDetonation = FindChildComponent<Button>("ActionPanel/m_btn_Detonation");
            _btnPurify = FindChildComponent<Button>("ActionPanel/m_btn_Purify");
            _btnAttackEnemy = FindChildComponent<Button>("ActionPanel/m_btn_AttackEnemy");
            _btnEnemyMagic = FindChildComponent<Button>("ActionPanel/m_btn_EnemyMagic");
            _btnHealHero = FindChildComponent<Button>("ActionPanel/m_btn_HealHero");
            _btnDispelHero = FindChildComponent<Button>("ActionPanel/m_btn_DispelHero");
            _btnClearAll = FindChildComponent<Button>("ActionPanel/m_btn_ClearAll");
            _btnReset = FindChildComponent<Button>("ActionPanel/m_btn_Reset");

            _btnBattleFury.onClick.AddListener(OnBattleFuryClicked);
            _btnBerserker.onClick.AddListener(OnBerserkerClicked);
            _btnGuardianAura.onClick.AddListener(OnGuardianAuraClicked);
            _btnSpellShield.onClick.AddListener(OnSpellShieldClicked);
            _btnPoison.onClick.AddListener(OnPoisonClicked);
            _btnFrostbite.onClick.AddListener(OnFrostbiteClicked);
            _btnDetonation.onClick.AddListener(OnDetonationClicked);
            _btnPurify.onClick.AddListener(OnPurifyClicked);
            _btnAttackEnemy.onClick.AddListener(OnAttackEnemyClicked);
            _btnEnemyMagic.onClick.AddListener(OnEnemyMagicClicked);
            _btnHealHero.onClick.AddListener(OnHealHeroClicked);
            _btnDispelHero.onClick.AddListener(OnDispelHeroClicked);
            _btnClearAll.onClick.AddListener(OnClearAllClicked);
            _btnReset.onClick.AddListener(OnResetClicked);
        }

        // 监听 Buff 系统中的所有运行时变化
        protected override void RegisterEvent()
        {
            AddUIEvent<BuffRuntimeEvent>(BuffEventIds.AnyChanged, OnBuffRuntimeEvent);
        }

        // 创建演示单位并启动界面刷新计时器
        protected override void OnCreate()
        {
            ResetDemo();
            _refreshTimerId = GameModule.Timer.AddTimer(OnRefreshTimer, 0.1f, isLoop: true, isUnscaled: true);
        }

        // 每次显示窗口时刷新当前数据
        protected override void OnRefresh()
        {
            RefreshView();
        }

        // 销毁窗口时清理计时器事件和演示单位
        protected override void OnDestroy()
        {
            _isDestroying = true;
            if (_refreshTimerId >= 0)
            {
                GameModule.Timer.RemoveTimer(_refreshTimerId);
                _refreshTimerId = -1;
            }

            RemoveButtonListeners();
            RemoveDemoUnits();
            _hero = null;
            _enemy = null;
        }

        // 重新创建演示单位并恢复初始状态
        private void ResetDemo()
        {
            _suppressEvents = true;
            try
            {
                RemoveDemoUnits();

                _hero = BuffSystemService.Instance.CreateUnit(
                    HERO_UNIT_ID,
                    PrimaryAttributeType.Strength,
                    teamId: 1,
                    baseAttributes: CreateHeroAttributes());
                _hero.Position = Vector3.zero;

                _enemy = BuffSystemService.Instance.CreateUnit(
                    ENEMY_UNIT_ID,
                    PrimaryAttributeType.Intelligence,
                    teamId: 2,
                    baseAttributes: CreateEnemyAttributes());
                _enemy.Position = new Vector3(350f, 0f, 0f);
            }
            finally
            {
                _suppressEvents = false;
            }

            _logs.Clear();
            AppendLog("演示单位已重置，可从中间按钮添加 Buff 或 Debuff。");
            RefreshView();
        }

        // 创建我方英雄的基础属性
        private static Dictionary<string, float> CreateHeroAttributes()
        {
            return new Dictionary<string, float>
            {
                [CombatAttributeNames.Strength] = 28f,
                [CombatAttributeNames.Agility] = 22f,
                [CombatAttributeNames.Intelligence] = 18f,
                [CombatAttributeNames.MaxHp] = 300f,
                [CombatAttributeNames.MaxMana] = 100f,
                [CombatAttributeNames.AttackDamage] = 30f,
                [CombatAttributeNames.AttackSpeed] = 100f,
                [CombatAttributeNames.Armor] = 2f,
                [CombatAttributeNames.MagicResistance] = 0.25f,
                [CombatAttributeNames.MoveSpeed] = 305f,
                [CombatAttributeNames.StatusResistance] = 0f,
            };
        }

        // 创建敌方目标的基础属性
        private static Dictionary<string, float> CreateEnemyAttributes()
        {
            return new Dictionary<string, float>
            {
                [CombatAttributeNames.Strength] = 34f,
                [CombatAttributeNames.Agility] = 18f,
                [CombatAttributeNames.Intelligence] = 26f,
                [CombatAttributeNames.MaxHp] = 500f,
                [CombatAttributeNames.MaxMana] = 160f,
                [CombatAttributeNames.AttackDamage] = 38f,
                [CombatAttributeNames.AttackSpeed] = 95f,
                [CombatAttributeNames.Armor] = 3f,
                [CombatAttributeNames.MagicResistance] = 0.25f,
                [CombatAttributeNames.MoveSpeed] = 290f,
                [CombatAttributeNames.SpellAmplification] = 10f,
            };
        }

        // 从 Buff 世界中移除当前演示单位
        private void RemoveDemoUnits()
        {
            BuffWorld world = BuffSystemService.Instance.World;
            if (world == null)
            {
                return;
            }

            world.RemoveUnit(HERO_UNIT_ID);
            world.RemoveUnit(ENEMY_UNIT_ID);
        }

        // 我方增益按钮事件
        private void OnBattleFuryClicked()
        {
            ApplyBuff(_hero, "BattleFury", _hero);
        }

        private void OnBerserkerClicked()
        {
            ApplyBuff(_hero, "BerserkerStacks", _hero);
        }

        private void OnGuardianAuraClicked()
        {
            ApplyBuff(_hero, "GuardianAura", _hero);
        }

        private void OnSpellShieldClicked()
        {
            ApplyBuff(_hero, "SpellShield", _hero);
        }

        // 敌方减益按钮事件
        private void OnPoisonClicked()
        {
            ApplyBuff(_hero, "PoisonDoT", _enemy);
        }

        private void OnFrostbiteClicked()
        {
            ApplyBuff(_hero, "Frostbite", _enemy);
        }

        private void OnDetonationClicked()
        {
            ApplyBuff(_hero, "DetonationMark", _enemy);
        }

        private void OnPurifyClicked()
        {
            ApplyBuff(_hero, "PurifyingLight", _hero);
        }

        // 使用英雄当前攻击力对敌方造成物理伤害
        private void OnAttackEnemyClicked()
        {
            if (!EnsureUnitsAlive())
            {
                return;
            }

            float damage = Mathf.Max(1f, _hero.GetAttribute(CombatAttributeNames.AttackDamage));
            BuffDamageResult result = _enemy.TakeDamage(damage, BuffDamageType.Physical, _hero);
            if (result.WasBlocked)
            {
                AppendLog("敌方目标阻挡了本次物理攻击。");
            }
        }

        // 对英雄造成固定数值的魔法伤害
        private void OnEnemyMagicClicked()
        {
            if (!EnsureUnitsAlive())
            {
                return;
            }

            BuffDamageResult result = _hero.TakeDamage(120f, BuffDamageType.Magical, _enemy);
            if (result.WasBlocked)
            {
                AppendLog("英雄阻挡了本次魔法伤害。");
            }
        }

        // 治疗存活英雄或者复活已阵亡英雄
        private void OnHealHeroClicked()
        {
            if (_hero == null)
            {
                return;
            }

            if (!_hero.IsAlive)
            {
                _hero.Revive(0.5f, 0.5f);
                return;
            }

            float healed = _hero.Heal(200f, _hero);
            if (healed <= 0f)
            {
                AppendLog("英雄生命值已满，无需治疗。");
            }
        }

        // 对英雄执行强驱散并只移除减益效果
        private void OnDispelHeroClicked()
        {
            if (_hero == null)
            {
                return;
            }

            int count = _hero.Dispel(BuffDispelType.Strong, BuffModifierKind.Debuff);
            AppendLog(count > 0 ? $"对英雄执行强驱散，移除了 {count} 个减益。" : "英雄当前没有可驱散的减益。");
            RefreshView();
        }

        // 移除双方全部效果并保留销毁触发逻辑
        private void OnClearAllClicked()
        {
            if (_hero == null || _enemy == null)
            {
                return;
            }

            _hero.Clear(BuffRemovalReason.Manual);
            _enemy.Clear(BuffRemovalReason.Manual);
            AppendLog("已移除双方全部效果；带销毁触发器的效果仍会正常结算。");
            RefreshView();
        }

        private void OnResetClicked()
        {
            ResetDemo();
        }

        // 向目标添加指定配置的 Buff
        private void ApplyBuff(BuffUnit target, string buffKey, BuffUnit source)
        {
            if (target == null || !target.IsAlive)
            {
                AppendLog("目标已经阵亡，请先点击“重置演示”或“治疗/复活英雄”。");
                return;
            }

            try
            {
                BuffInstance instance = target.ApplyBuff(buffKey, source);
                if (instance == null)
                {
                    AppendLog($"{GetUnitName(target)} 拒绝了 {GetBuffName(buffKey)}（可能处于减益免疫状态）。");
                    RefreshView();
                }
            }
            catch (Exception exception)
            {
                AppendLog($"添加效果失败：{exception.Message}");
                Log.Error($"BuffDemoUI 添加 Buff 失败：{exception}");
            }
        }

        // 检查双方单位是否可以继续执行战斗操作
        private bool EnsureUnitsAlive()
        {
            if (_hero == null || _enemy == null)
            {
                return false;
            }

            if (!_hero.IsAlive || !_enemy.IsAlive)
            {
                AppendLog("存在已阵亡单位，请点击“重置演示”。");
                return false;
            }

            return true;
        }

        // 将 Buff 运行时事件转换为中文战斗日志
        private void OnBuffRuntimeEvent(BuffRuntimeEvent runtimeEvent)
        {
            if (_isDestroying || _suppressEvents || runtimeEvent == null || runtimeEvent.Unit == null)
            {
                return;
            }

            string unitName = GetUnitName(runtimeEvent.Unit);
            string buffName = runtimeEvent.Instance?.Template?.DisplayName ?? "未知效果";
            switch (runtimeEvent.EventType)
            {
                case BuffRuntimeEventType.BuffApplied:
                    AppendLog($"{unitName} 获得 {buffName}。");
                    break;
                case BuffRuntimeEventType.BuffRefreshed:
                    AppendLog($"{unitName} 刷新了 {buffName} 的持续时间。");
                    break;
                case BuffRuntimeEventType.BuffStackChanged:
                    AppendLog($"{unitName} 的 {buffName} 叠加至 {runtimeEvent.Instance.Stacks} 层。");
                    break;
                case BuffRuntimeEventType.BuffRemoved:
                    AppendLog($"{unitName} 的 {buffName} 已移除（{GetRemovalReason(runtimeEvent.RemovalReason)}）。");
                    break;
                case BuffRuntimeEventType.DamageTaken:
                    AppendLog($"{unitName} 受到 {runtimeEvent.Value:0.#} 点{GetDamageType(runtimeEvent.DamageType)}伤害。");
                    break;
                case BuffRuntimeEventType.Healed:
                    AppendLog($"{unitName} 恢复 {runtimeEvent.Value:0.#} 点生命值。");
                    break;
                case BuffRuntimeEventType.UnitDied:
                    AppendLog($"{unitName} 已阵亡。");
                    break;
                case BuffRuntimeEventType.UnitRevived:
                    AppendLog($"{unitName} 已复活。");
                    break;
            }

            RefreshView();
        }

        // 定时刷新属性和 Buff 剩余时间
        private void OnRefreshTimer(object[] args)
        {
            RefreshView();
        }

        // 刷新面板中的全部动态数据
        private void RefreshView()
        {
            if (_hero == null || _enemy == null || _textHeroStats == null)
            {
                return;
            }

            BuffSystemService service = BuffSystemService.Instance;
            _textConfigStatus.text = service.Database == null
                ? "Buff 配置尚未加载"
                : $"Schema {service.Database.Data.SchemaVersion}  ·  已加载 {service.Database.Templates.Count} 个效果  ·  运行时自动计时";

            _textHeroStats.text = BuildHeroStats(_hero);
            _textHeroState.text = BuildUnitState(_hero);
            _textHeroBuffs.text = BuildBuffList(_hero);
            _textEnemyStats.text = BuildEnemyStats(_enemy);
            _textEnemyBuffs.text = BuildBuffList(_enemy);
            RefreshLogText();
        }

        // 生成英雄属性面板文本
        private string BuildHeroStats(BuffUnit unit)
        {
            _builder.Clear();
            AppendResourceLine(_builder, "生命", unit.CurrentHp, unit.GetAttribute(CombatAttributeNames.MaxHp), "#72E39A");
            AppendResourceLine(_builder, "魔法", unit.CurrentMana, unit.GetAttribute(CombatAttributeNames.MaxMana), "#69B8FF");
            _builder.AppendLine();
            AppendAttributeLine(_builder, "力量", unit.GetAttribute(CombatAttributeNames.Strength));
            AppendAttributeLine(_builder, "敏捷", unit.GetAttribute(CombatAttributeNames.Agility));
            AppendAttributeLine(_builder, "智力", unit.GetAttribute(CombatAttributeNames.Intelligence));
            AppendAttributeLine(_builder, "攻击力", unit.GetAttribute(CombatAttributeNames.AttackDamage));
            AppendAttributeLine(_builder, "攻击速度", unit.GetAttribute(CombatAttributeNames.AttackSpeed));
            AppendAttributeLine(_builder, "护甲", unit.GetAttribute(CombatAttributeNames.Armor));
            AppendPercentLine(_builder, "魔法抗性", unit.GetAttribute(CombatAttributeNames.MagicResistance));
            AppendPercentLine(_builder, "状态抗性", unit.GetAttribute(CombatAttributeNames.StatusResistance));
            AppendAttributeLine(_builder, "移动速度", unit.GetAttribute(CombatAttributeNames.MoveSpeed));
            return _builder.ToString();
        }

        // 生成敌方属性面板文本
        private string BuildEnemyStats(BuffUnit unit)
        {
            _builder.Clear();
            AppendResourceLine(_builder, "生命", unit.CurrentHp, unit.GetAttribute(CombatAttributeNames.MaxHp), "#FF8A82");
            AppendResourceLine(_builder, "魔法", unit.CurrentMana, unit.GetAttribute(CombatAttributeNames.MaxMana), "#69B8FF");
            AppendAttributeLine(_builder, "攻击力", unit.GetAttribute(CombatAttributeNames.AttackDamage));
            AppendAttributeLine(_builder, "护甲", unit.GetAttribute(CombatAttributeNames.Armor));
            AppendPercentLine(_builder, "魔法抗性", unit.GetAttribute(CombatAttributeNames.MagicResistance));
            return _builder.ToString();
        }

        // 生成单位行为权限和状态效果文本
        private string BuildUnitState(BuffUnit unit)
        {
            _builder.Clear();
            _builder.Append("移动 ").Append(FormatPermission(unit.CanMove));
            _builder.Append("   攻击 ").Append(FormatPermission(unit.CanAttack));
            _builder.Append("\n施法 ").Append(FormatPermission(unit.CanCast));
            _builder.Append("   道具 ").Append(FormatPermission(unit.CanUseItems));

            _statusCache.Clear();
            IReadOnlyList<BuffInstance> buffs = unit.ActiveBuffs;
            for (int buffIndex = 0; buffIndex < buffs.Count; buffIndex++)
            {
                List<string> statuses = buffs[buffIndex].Template.StatusEffects;
                for (int statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
                {
                    _statusCache.Add(GetStatusName(statuses[statusIndex]));
                }
            }

            _builder.Append("\n状态：");
            if (_statusCache.Count == 0)
            {
                _builder.Append("正常");
            }
            else
            {
                bool first = true;
                foreach (string status in _statusCache)
                {
                    if (!first)
                    {
                        _builder.Append("、");
                    }

                    _builder.Append(status);
                    first = false;
                }
            }

            return _builder.ToString();
        }

        // 生成当前生效效果的层数和剩余时间文本
        private string BuildBuffList(BuffUnit unit)
        {
            _builder.Clear();
            IReadOnlyList<BuffInstance> buffs = unit.ActiveBuffs;
            int visibleCount = 0;
            for (int index = 0; index < buffs.Count; index++)
            {
                BuffInstance instance = buffs[index];
                if (instance.IsRemoved || instance.Template.IsHidden)
                {
                    continue;
                }

                visibleCount++;
                string color = instance.Template.ModifierKind == BuffModifierKind.Debuff ? "#FF7E75" : "#73DFA3";
                string kind = instance.Template.ModifierKind == BuffModifierKind.Debuff ? "减益" : "增益";
                string duration = instance.IsPermanent ? "永久" : $"{Mathf.Max(0f, instance.RemainingDuration):0.0}s";
                string stack = instance.Stacks > 1 ? $" ×{instance.Stacks}" : string.Empty;
                string aura = instance.IsAuraProxy ? "（光环作用）" : instance.Template.IsAura ? "（光环源）" : string.Empty;

                _builder.Append("<color=").Append(color).Append(">● [").Append(kind).Append("]</color> ")
                    .Append(instance.Template.DisplayName).Append(stack).Append(aura)
                    .Append("  <color=#E8C56A>").Append(duration).AppendLine("</color>");
            }

            return visibleCount == 0 ? "<color=#788393>当前没有生效中的效果</color>" : _builder.ToString();
        }

        // 添加日志并限制日志保存数量
        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _logs.Add($"[{Time.realtimeSinceStartup:0.0}] {message}");
            if (_logs.Count > MAX_LOG_COUNT)
            {
                _logs.RemoveAt(0);
            }

            RefreshLogText();
        }

        // 按照最新日志优先的顺序刷新日志文本
        private void RefreshLogText()
        {
            if (_textLog == null)
            {
                return;
            }

            _builder.Clear();
            for (int index = _logs.Count - 1; index >= 0; index--)
            {
                _builder.AppendLine(_logs[index]);
            }

            _textLog.text = _builder.ToString();
        }

        // 获取 Buff 配置中的中文名称
        private string GetBuffName(string buffKey)
        {
            return BuffSystemService.Instance.TryGetTemplate(buffKey, out BuffTemplate template)
                ? template.DisplayName
                : buffKey;
        }

        // 获取演示单位的中文名称
        private string GetUnitName(BuffUnit unit)
        {
            if (ReferenceEquals(unit, _hero) || string.Equals(unit?.Id, HERO_UNIT_ID, StringComparison.Ordinal))
            {
                return "英雄";
            }

            if (ReferenceEquals(unit, _enemy) || string.Equals(unit?.Id, ENEMY_UNIT_ID, StringComparison.Ordinal))
            {
                return "敌方目标";
            }

            return unit?.Id ?? "未知单位";
        }

        private static void AppendResourceLine(StringBuilder builder, string label, float current, float maximum, string color)
        {
            builder.Append(label).Append("  <color=").Append(color).Append(">")
                .Append(current.ToString("0.#")).Append(" / ").Append(maximum.ToString("0.#"))
                .AppendLine("</color>");
        }

        private static void AppendAttributeLine(StringBuilder builder, string label, float value)
        {
            builder.Append(label.PadRight(6)).Append("  ").AppendLine(value.ToString("0.#"));
        }

        private static void AppendPercentLine(StringBuilder builder, string label, float value)
        {
            float percent = Mathf.Abs(value) > 1f ? value : value * 100f;
            builder.Append(label.PadRight(6)).Append("  ").Append(percent.ToString("0.#")).AppendLine("%");
        }

        private static string FormatPermission(bool allowed)
        {
            return allowed ? "<color=#73DFA3>可用</color>" : "<color=#FF7E75>禁用</color>";
        }

        private static string GetDamageType(BuffDamageType? damageType)
        {
            switch (damageType)
            {
                case BuffDamageType.Physical:
                    return "物理";
                case BuffDamageType.Magical:
                    return "魔法";
                case BuffDamageType.Pure:
                    return "纯粹";
                case BuffDamageType.HpRemoval:
                    return "生命移除";
                default:
                    return string.Empty;
            }
        }

        private static string GetRemovalReason(BuffRemovalReason? reason)
        {
            switch (reason)
            {
                case BuffRemovalReason.Expired:
                    return "持续时间结束";
                case BuffRemovalReason.Dispelled:
                    return "被驱散";
                case BuffRemovalReason.Replaced:
                    return "被替换";
                case BuffRemovalReason.Death:
                    return "单位阵亡";
                case BuffRemovalReason.AuraLost:
                    return "离开光环";
                case BuffRemovalReason.WorldCleared:
                    return "世界清理";
                default:
                    return "手动移除";
            }
        }

        private static string GetStatusName(string status)
        {
            switch (status)
            {
                case "Stun": return "眩晕";
                case "Root": return "禁锢";
                case "Slow": return "减速";
                case "Silence": return "沉默";
                case "Disarm": return "缴械";
                case "Hex": return "妖术";
                case "Fear": return "恐惧";
                case "Taunt": return "嘲讽";
                case "Muted": return "禁用道具";
                case "Break": return "破坏被动";
                case "Poisoned": return "中毒";
                case "DebuffImmune": return "减益免疫";
                case "Invulnerable": return "无敌";
                case "Ethereal": return "虚无";
                default: return status;
            }
        }

        // 移除当前窗口注册的所有按钮事件
        private void RemoveButtonListeners()
        {
            RemoveListener(_btnBattleFury, OnBattleFuryClicked);
            RemoveListener(_btnBerserker, OnBerserkerClicked);
            RemoveListener(_btnGuardianAura, OnGuardianAuraClicked);
            RemoveListener(_btnSpellShield, OnSpellShieldClicked);
            RemoveListener(_btnPoison, OnPoisonClicked);
            RemoveListener(_btnFrostbite, OnFrostbiteClicked);
            RemoveListener(_btnDetonation, OnDetonationClicked);
            RemoveListener(_btnPurify, OnPurifyClicked);
            RemoveListener(_btnAttackEnemy, OnAttackEnemyClicked);
            RemoveListener(_btnEnemyMagic, OnEnemyMagicClicked);
            RemoveListener(_btnHealHero, OnHealHeroClicked);
            RemoveListener(_btnDispelHero, OnDispelHeroClicked);
            RemoveListener(_btnClearAll, OnClearAllClicked);
            RemoveListener(_btnReset, OnResetClicked);
        }

        // 安全移除单个按钮事件
        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(callback);
            }
        }
    }
}
