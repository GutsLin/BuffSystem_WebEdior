# Buff 与英雄属性系统完整文档

> 参考 Dota 2 Modifier、英雄属性与战斗状态体系设计的原子化 Buff 系统。核心逻辑使用纯 C# 实现，可供 Unity 客户端、服务端和单元测试共同使用。

## 文档版本与实现边界

- **V1（现有基础）**：ModifierTemplate、Modifier、ModifierSystem、属性加成、状态效果、叠加策略、周期触发与事件分发。
- **V2（本文完善的目标设计）**：英雄力量/敏捷/智力、主属性/全才类型、派生属性、伤害结算、状态抗性、驱散、光环和更完整的战斗事件。
- 文档中标记为“V2”的类型与 API 是下一阶段实现目标；在代码完成前，不应把它们视为已经可调用的接口。
- Dota 2 的具体数值会随版本调整，因此本文采用“**参考默认值 + 项目配置覆盖**”的方式，不把平衡参数硬编码进核心逻辑。

---

## 一、模块结构

### 1.1 三层架构

```
┌─────────────────────────────────────────────────────┐
│              Editor 测试工具层                         │  BuffSystem.Editor
│   BuffSystemTestWindow / ModifierTemplateSOEditor    │
│   EnumChineseDrawer (枚举中文 PropertyDrawer)          │
├─────────────────────────────────────────────────────┤
│              Unity 集成层                              │  BuffSystem.Unity
│   ModifierUnit (MonoBehaviour)                       │
│   ModifierSystemDriver (MonoBehaviour)               │
│   ModifierTemplateSO (ScriptableObject)              │
├─────────────────────────────────────────────────────┤
│              纯 C# 核心层                              │  BuffSystem.Core (noEngineReferences: true)
│   ModifierSystem / Modifier / ModifierTemplate       │
│   DataDrivenTemplate / IModifierTemplateData         │
│   AttributeModifier / StatusEffectType / StackPolicy  │
└─────────────────────────────────────────────────────┘
```

### 1.2 程序集划分

| 程序集 | 依赖 Unity 引擎 | 说明 |
|--------|:---:|------|
| `BuffSystem.Core` | ❌ `noEngineReferences: true` | 纯 C# 核心，可被服务端直接引用 |
| `BuffSystem.Unity` | ✅ | MonoBehaviour 集成 + ScriptableObject 配置 |
| `BuffSystem.Editor` | ✅ (Editor only) | 编辑器测试窗口 + SO Inspector + 枚举中文显示 |

### 1.3 目录结构

```
Assets/BuffSystem/
├── README.md                                   # 系统说明
│
├── Core/                                       # ===== 纯 C# 核心层 =====
│   ├── BuffSystem.Core.asmdef                 # 程序集定义（noEngineReferences: true）
│   │
│   ├── IModifierUnit.cs                        # 单位接口
│   ├── IModifierEventHandler.cs                 # 事件响应接口
│   ├── Modifier.cs                             # Buff 运行时实例
│   ├── ModifierTemplate.cs                     # Buff 模板基类
│   ├── ModifierSystem.cs                       # Buff 管理系统
│   ├── ModifierTemplateData.cs                 # 数据驱动接口 + 配置结构体
│   ├── DataDrivenTemplate.cs                   # 数据驱动模板实现
│   │
│   ├── Attributes/
│   │   ├── AttributeType.cs                    # 属性类型枚举
│   │   ├── AttributeOp.cs                      # 修改操作枚举
│   │   └── AttributeModifier.cs                # 属性修改器（readonly struct）
│   │
│   ├── Status/
│   │   └── StatusEffectType.cs                 # 状态效果（Flags 枚举）
│   │
│   ├── Stacking/
│   │   └── StackPolicy.cs                     # 叠加策略枚举
│   │
│   └── Templates/                              # 示例 Buff（C# 编码方式）
│       ├── AttackPowerBuff.cs                  # 攻击力提升
│       ├── PoisonDoT.cs                        # 持续掉血（毒）
│       ├── SlowDebuff.cs                       # 减速
│       └── StunDebuff.cs                       # 眩晕
│
├── Unity/                                      # ===== Unity 集成层 =====
│   ├── BuffSystem.Unity.asmdef
│   ├── ModifierUnit.cs                         # 单位 MonoBehaviour
│   ├── ModifierSystemDriver.cs                 # 系统驱动 MonoBehaviour
│   └── ModifierTemplateSO.cs                   # Buff 模板 ScriptableObject
│
└── Editor/                                     # ===== 编辑器工具层 =====
    ├── BuffSystem.Editor.asmdef
    ├── BuffSystemTestWindow.cs                 # 测试窗口（EditorWindow）
    ├── ModifierTemplateSOEditor.cs             # SO 自定义 Inspector
    └── EnumChineseDrawer.cs                    # 枚举中文 PropertyDrawer
```

#### V2 目标扩展目录

以下文件用于承载 Dota 2 风格的英雄属性与战斗结算。它们属于目标设计，不代表当前已经实现：

```text
Assets/BuffSystem/Core/
├── Attributes/
│   ├── PrimaryAttributeType.cs        # 力量/敏捷/智力/全才/无主属性
│   ├── HeroAttributeProfile.cs        # 英雄基础值、成长值与主属性
│   ├── AttributeFormulaConfig.cs      # 三维换算配置
│   ├── AttributeCalculator.cs         # 统一属性计算管线
│   └── AttributeSnapshot.cs           # 一帧或一次结算使用的只读属性快照
├── Combat/
│   ├── DamageType.cs                  # 物理/魔法/纯粹/生命移除
│   ├── DamageFlags.cs                 # 反弹、不可吸血、忽略增幅等标记
│   ├── DamageContext.cs               # 伤害输入与结算结果
│   └── DamageResolver.cs              # 护甲、魔抗、免疫与增减伤结算
├── Status/
│   ├── DispelType.cs                  # 基础驱散/强驱散
│   ├── DispelRule.cs                  # Buff 可驱散规则
│   └── StatusResistanceCalculator.cs  # 控制时长缩减
└── Aura/
    ├── AuraDefinition.cs              # 光环配置
    └── AuraSystem.cs                  # 范围检测与子 Modifier 管理
```

### 1.4 核心类型总览

| 类型 | 所在层 | 职责 |
|------|--------|------|
| `AttributeType` | Core | 可计算属性枚举；V1 包含 MaxHp / Attack / Defense / MoveSpeed 等，V2 扩展三维、资源、攻防与功能属性 |
| `PrimaryAttributeType` | Core（V2） | 英雄主属性类型：Strength / Agility / Intelligence / Universal / None |
| `HeroAttributeProfile` | Core（V2） | 英雄基础三维、成长三维、主属性类型、等级与基础战斗属性 |
| `AttributeFormulaConfig` | Core（V2） | 三维到生命、魔法、护甲、攻速、攻击力等派生属性的可配置换算规则 |
| `AttributeOp` | Core | 属性修改操作：V1 为 `Add`、`PercentAdd`；V2 增加最终乘区、覆盖与上下限操作 |
| `AttributeModifier` | Core | 属性修改器原子单元（readonly struct：Type + Op + Value） |
| `StatusEffectType` | Core | 状态效果位标记（Stun/Silence/Disarm/Root/Slow/Blind/Invulnerable/MagicImmune） |
| `DispelType` | Core（V2） | 驱散强度：Basic / Strong；Buff 本身通过 DispelRule 声明是否可被驱散 |
| `DamageType` | Core（V2） | 伤害类型：Physical / Magical / Pure / HpRemoval |
| `DamageContext` | Core（V2） | 一次伤害的攻击者、目标、来源、原始值、类型、标记与结算结果 |
| `AuraDefinition` | Core（V2） | 光环范围、阵营筛选、目标筛选、子 Modifier 与离开范围残留时间 |
| `StackPolicy` | Core | 叠加策略（Refresh/Stack/Replace/Independent） |
| `IModifierUnit` | Core | 单位接口：GetBaseAttribute / ModifyBaseAttribute / IsAlive |
| `IModifierEventHandler` | Core | 事件响应接口：OnUnitTakeDamage / OnUnitDealDamage / OnUnitDeath |
| `ModifierTemplate` | Core | Buff 模板基类：配置 + 生命周期虚方法 + 动态属性/状态虚方法 |
| `Modifier` | Core | Buff 运行时实例：层数 / 剩余时间 / Think 计时器 / 激活状态 |
| `ModifierSystem` | Core | 管理系统：添加 / 移除 / 查询 / 更新 / 事件分发 |
| `IModifierTemplateData` | Core | 数据驱动接口：SO 和配置表的统一抽象 |
| `AttributeModifierEntry` | Core | 可序列化属性修改器配置项（支持 ScaleByStacks） |
| `ThinkActionEntry` | Core | 可序列化周期动作配置项（支持 ScaleByStacks） |
| `DataDrivenTemplate` | Core | 从 IModifierTemplateData 创建的模板实例 |
| `ModifierUnit` | Unity | MonoBehaviour 单位，实现 IModifierUnit |
| `ModifierSystemDriver` | Unity | MonoBehaviour 驱动器，提供全局 System 静态访问 |
| `ModifierTemplateSO` | Unity | ScriptableObject Buff 配置，实现 IModifierTemplateData |

---

## 二、核心设计

### 2.1 Template + Modifier 双层模型

参考 Dota 2 的 Lua Modifier 系统：

- **ModifierTemplate（模板）**：共享定义，类似"类"。配置 Duration / StackPolicy / AttributeModifiers 等，子类可 override 生命周期方法。
- **Modifier（实例）**：运行时实例，类似"对象"。持有层数、剩余时间、Think 计时器等独有状态。

```
ModifierTemplate（共享）           Modifier（实例 ×N）
┌──────────────────┐            ┌──────────────────────┐
│ Name: "PoisonDoT" │            │ UniqueId: 42          │
│ Duration: 5s       │  ──创建──→ │ StackCount: 3         │
│ StackPolicy: Stack │            │ RemainingDuration: 3.2│
│ ThinkInterval: 1s  │            │ ThinkTimer: 0.8      │
│ MaxStacks: 5       │            │ IsActive: true        │
└──────────────────┘            └──────────────────────┘
```

### 2.2 属性计算公式

```
最终属性 = (基础值 + Σ扁平加成) × (1 + Σ百分比加成)
```

- `Add`（扁平加成）：所有同类扁平值相加
- `PercentAdd`（百分比加成）：所有同类百分比相加后乘以总值

**示例**：基础攻击 100，Buff A 给 +20 ATK，Buff B 给 +30% ATK
```
final = (100 + 20) × (1 + 0.3) = 120 × 1.3 = 156
```

### 2.3 叠加策略

同一单位上再次施加同名 Buff 时的行为：

| 策略 | 行为 | 适用场景 |
|------|------|---------|
| `Refresh` | 重置持续时间，不增加层数 | 眩晕、攻击力提升 |
| `Stack` | 层数 +1（上限 MaxStacks），刷新持续时间 | 毒素、减速 |
| `Replace` | 移除旧实例，创建新实例 | 独特的 debuff |
| `Independent` | 允许同名 Buff 多实例共存 | 来源于不同施放者的 buff |

### 2.4 生命周期事件

```
AddModifier()
  ├─ 不存在同名 Buff → OnCreated()
  ├─ Refresh 策略    → OnRefresh()
  ├─ Stack 策略      → OnStackChanged() → OnRefresh()
  └─ Replace 策略    → OnDestroy(旧) → OnCreated(新)

Update(deltaTime)
  ├─ ThinkInterval 到期 → OnIntervalThink()
  └─ Duration 到期      → OnDestroy()

RemoveModifier()
  └─ OnDestroy()

事件分发（由游戏逻辑触发）
  ├─ DispatchUnitTakeDamage → IModifierEventHandler.OnUnitTakeDamage()
  ├─ DispatchUnitDealDamage → IModifierEventHandler.OnUnitDealDamage()
  └─ DispatchUnitDeath     → IModifierEventHandler.OnUnitDeath()
```

### 2.5 事件分发机制

`ModifierSystem` 提供三个事件分发方法，由游戏逻辑在恰当时机调用：

| 方法 | 触发时机 | 通知对象 |
|------|---------|---------|
| `DispatchUnitTakeDamage(unit, amount, source)` | 单位受到伤害时 | 该单位上所有实现了 `IModifierEventHandler` 的 Buff |
| `DispatchUnitDealDamage(unit, amount, target)` | 单位造成伤害时 | 该单位上所有实现了 `IModifierEventHandler` 的 Buff |
| `DispatchUnitDeath(unit, killer)` | 单位死亡时 | 该单位上所有实现了 `IModifierEventHandler` 的 Buff |

### 2.6 英雄三维与主属性模型（V2）

项目中的“英雄三维”统一指 **力量（Strength）/ 敏捷（Agility）/ 智力（Intelligence）**。三维既是可被 Buff 修改的普通属性，也是生命、魔法、护甲、攻速和攻击力等派生属性的输入。

每个英雄拥有一个 `PrimaryAttributeType`：

| 类型 | 中文 | 主攻击力收益 |
|------|------|-------------|
| `Strength` | 力量英雄 | 每 1 点力量提供 1 点主攻击力 |
| `Agility` | 敏捷英雄 | 每 1 点敏捷提供 1 点主攻击力 |
| `Intelligence` | 智力英雄 | 每 1 点智力提供 1 点主攻击力 |
| `Universal` | 全才英雄 | 力量、敏捷、智力每 1 点分别提供可配置的攻击力；Dota 2 参考值为 0.45 |
| `None` | 无主属性单位 | 三维不直接提供主攻击力，适用于普通小兵、建筑或特殊召唤物 |

建议的数据定义：

```csharp
public enum PrimaryAttributeType
{
    None,
    Strength,
    Agility,
    Intelligence,
    Universal
}

public sealed class HeroAttributeProfile
{
    public PrimaryAttributeType PrimaryAttribute { get; init; }
    public int Level { get; set; } = 1;

    public float BaseStrength { get; init; }
    public float BaseAgility { get; init; }
    public float BaseIntelligence { get; init; }

    public float StrengthGrowth { get; init; }
    public float AgilityGrowth { get; init; }
    public float IntelligenceGrowth { get; init; }

    public float BaseMaxHp { get; init; }
    public float BaseMaxMana { get; init; }
    public float BaseAttackDamage { get; init; }
    public float BaseArmor { get; init; }
    public float BaseAttackTime { get; init; } = 1.7f;
}
```

等级成长建议统一采用：

```text
等级属性 = 初始属性 + 成长属性 × (当前等级 - 1)
```

永久成长、装备、天赋和 Buff 不直接改写 `BaseStrength` 等英雄模板数据，而是通过属性修改器进入计算管线。

### 2.7 三维派生属性默认规则（V2）

下表采用 Dota 2 属性页在本文修订时的参考值。项目必须允许通过 `AttributeFormulaConfig` 覆盖这些系数：

| 输入属性 | 派生收益 | Dota 2 参考默认值 |
|---------|---------|------------------|
| 每 1 力量 | 最大生命值 | +22 |
| 每 1 力量 | 生命恢复/秒 | +0.1 |
| 每 1 敏捷 | 基础护甲 | +0.167（约 1/6） |
| 每 1 敏捷 | 攻击速度 | +1 |
| 每 1 智力 | 最大魔法值 | +12 |
| 每 1 智力 | 魔法恢复/秒 | +0.05 |
| 每 1 智力 | 魔法抗性 | +0.1% |
| 每 1 主属性 | 主攻击力 | +1 |
| 全才英雄每 1 点任意三维 | 主攻击力 | +0.45 |

```csharp
public sealed class AttributeFormulaConfig
{
    public float StrengthToMaxHp { get; init; } = 22f;
    public float StrengthToHpRegen { get; init; } = 0.1f;
    public float AgilityToArmor { get; init; } = 1f / 6f;
    public float AgilityToAttackSpeed { get; init; } = 1f;
    public float IntelligenceToMaxMana { get; init; } = 12f;
    public float IntelligenceToManaRegen { get; init; } = 0.05f;
    public float IntelligenceToMagicResistance { get; init; } = 0.001f;
    public float PrimaryAttributeToAttackDamage { get; init; } = 1f;
    public float UniversalAttributeToAttackDamage { get; init; } = 0.45f;
}
```

> 注意：这些数值是参考配置，不应散落在 `ModifierSystem`、角色 MonoBehaviour 或具体 Buff 类中。

### 2.8 属性计算管线（V2）

属性计算必须有固定顺序，否则“+10 力量”和“+220 最大生命值”可能在不同系统中得到不一致结果。

```text
英雄模板基础值
  ↓
等级成长 + 永久成长
  ↓
三维的扁平与百分比修改
  ↓
得到最终力量/敏捷/智力
  ↓
根据 AttributeFormulaConfig 生成派生属性
  ↓
叠加直接修改派生属性的装备/Buff
  ↓
应用最终乘区、覆盖值与上下限
  ↓
生成 AttributeSnapshot
```

核心公式示例：

```text
FinalStrength = CalculateAttribute(Strength)
FinalAgility = CalculateAttribute(Agility)
FinalIntelligence = CalculateAttribute(Intelligence)

MaxHp = BaseMaxHp
      + FinalStrength × StrengthToMaxHp
      + DirectMaxHpBonus

Armor = BaseArmor
      + FinalAgility × AgilityToArmor
      + DirectArmorBonus

MaxMana = BaseMaxMana
        + FinalIntelligence × IntelligenceToMaxMana
        + DirectMaxManaBonus
```

主属性攻击力：

```text
力量/敏捷/智力英雄：
PrimaryDamage = 对应最终主属性 × PrimaryAttributeToAttackDamage

全才英雄：
PrimaryDamage = (FinalStrength + FinalAgility + FinalIntelligence)
              × UniversalAttributeToAttackDamage

无主属性单位：
PrimaryDamage = 0
```

攻击速度与攻击间隔建议拆开存储：

```text
TotalAttackSpeed = Clamp(BaseAttackSpeed + FinalAgility + BonusAttackSpeed, MinAttackSpeed, MaxAttackSpeed)
AttackInterval = BaseAttackTime × 100 / TotalAttackSpeed
```

`BaseAttackTime`、攻速上下限和是否允许突破上限均应由项目规则配置。

### 2.9 属性类型分组（V2）

不建议继续把所有属性平铺在一个缺少语义分组的短枚举中。仍可使用同一个 `AttributeType`，但应按用途分区：

| 分组 | 建议属性 |
|------|---------|
| 三维 | Strength / Agility / Intelligence |
| 资源 | MaxHp / HpRegen / MaxMana / ManaRegen |
| 普攻 | AttackDamage / AttackSpeed / BaseAttackTime / AttackRange |
| 防御 | Armor / MagicResistance / StatusResistance / Evasion |
| 移动 | MoveSpeed / MoveSpeedPercent / TurnRate |
| 技能 | CastRange / CastSpeed / CooldownReduction / ManaCostReduction / SpellAmplification |
| 恢复与吸血 | HealAmplification / Lifesteal / SpellLifesteal |
| 暴击 | CritChance / CritDamage |

命名上统一使用 `AttackDamage`、`Armor` 等明确含义，逐步替代容易混淆的 `Attack`、`Defense`。

### 2.10 属性修改层级与操作（V2）

V1 的 `Add` 和 `PercentAdd` 可以保留，但 V2 需要明确计算层级：

| 操作 | 建议枚举值 | 说明 |
|------|-----------|------|
| 扁平加成 | `Add` | +10 力量、+200 生命、+15 攻击力 |
| 百分比加成 | `PercentAdd` | 多个来源先相加，例如 +10% 与 +20% 合并为 +30% |
| 独立乘区 | `PercentMultiply` | 与其他独立来源逐项相乘，适合最终增减伤 |
| 最终覆盖 | `Override` | 将属性设为指定值，按优先级决定生效来源 |
| 最小限制 | `Min` | 属性不得低于指定值 |
| 最大限制 | `Max` | 属性不得高于指定值 |

推荐统一顺序：

```text
Result = (Base + ΣAdd) × (1 + ΣPercentAdd)
Result = Result × Π(1 + PercentMultiply)
Result = ApplyHighestPriorityOverride(Result)
Result = Clamp(Result, MinLimit, MaxLimit)
```

`AttributeModifier` 建议增加以下字段：

```csharp
public readonly struct AttributeModifier
{
    public AttributeType Type { get; init; }
    public AttributeOp Op { get; init; }
    public float Value { get; init; }
    public int Priority { get; init; }
    public string SourceKey { get; init; }
}
```

### 2.11 Modifier 身份与叠加键（V2）

仅用 `Template.Name` 判断同名 Buff，在多施法者、物品被动和光环场景中不够。建议引入 `ModifierStackKey`：

```text
StackKey = TemplateName
         + SourceUnitId（可选）
         + SourceAbilityId（可选）
         + CustomStackGroup（可选）
```

模板可选择以下身份规则：

| 规则 | 示例 |
|------|------|
| 全局同名唯一 | 同一种眩晕只刷新时间 |
| 同施放者唯一 | 每个施放者各自维护毒素层数 |
| 同技能唯一 | 同一英雄不同技能产生的同类减速可共存 |
| 完全独立 | 每次施加都创建独立实例 |
| 自定义叠加组 | 多件同系列装备共享唯一被动 |

### 2.12 伤害结算体系（V2）

Buff 系统不应通过直接修改 `MaxHp` 来模拟伤害。V2 引入统一的 `DamageResolver`，当前生命值与最大生命值必须分离。

| 伤害类型 | 默认结算规则 |
|---------|-------------|
| `Physical` | 受护甲影响 |
| `Magical` | 受魔法抗性影响 |
| `Pure` | 默认忽略护甲和魔抗，但仍受无敌、伤害免疫和特定标记影响 |
| `HpRemoval` | 直接改变生命资源，默认不触发吸血和部分受伤事件；具体行为由 Flags 控制 |

物理伤害的 Dota 风格护甲参考公式：

```text
ArmorReduction = (0.06 × Armor) / (1 + 0.06 × abs(Armor))
FinalPhysicalDamage = RawDamage × (1 - ArmorReduction)
```

魔法抗性来源建议采用乘法叠加，避免简单相加超过合理范围：

```text
CombinedResistance = 1 - Π(1 - ResistanceSource)
FinalMagicalDamage = RawDamage × (1 - CombinedResistance)
```

伤害结算阶段：

```text
创建 DamageContext
  → 攻击者伤害输出修正
  → 目标伤害承受修正
  → 类型抗性结算
  → 护盾/格挡/无敌检查
  → 扣减 CurrentHp
  → 分发生命变化、受伤、致死和击杀事件
```

`DamageContext` 至少应包含：攻击者、目标、来源技能/物品、原始伤害、伤害类型、伤害标记、结算前后数值、是否暴击、是否致死。

### 2.13 状态、状态抗性与驱散（V2）

建议将状态效果细化为：

| 类别 | 状态示例 |
|------|---------|
| 行动控制 | Stun / Hex / Taunt / Fear |
| 施法限制 | Silence / Muted |
| 攻击限制 | Disarm / Blind |
| 移动限制 | Root / Leash / Slow / Knockback |
| 被动限制 | Break |
| 防护状态 | Invulnerable / DebuffImmune / Untargetable / Ethereal |

为兼容 V1，可暂时保留 `MagicImmune`，但 V2 建议拆分“魔法伤害免疫”“负面状态免疫”和“不可选取”，不要用一个布尔值覆盖所有规则。

状态抗性默认缩短可受影响的负面状态持续时间：

```text
EffectiveDuration = OriginalDuration × (1 - StatusResistance)
```

模板需要声明该 Modifier 是否受状态抗性影响。例如击退位移、光环子 Buff、永久被动或项目定义的特殊控制可以选择不缩短。

驱散规则建议分为：

| Modifier 配置 | 行为 |
|---------------|------|
| `NotDispellable` | 不可驱散 |
| `BasicDispellable` | 基础驱散或强驱散均可移除 |
| `StrongDispellable` | 仅强驱散可移除 |

执行驱散时还要指定目标类型：移除负面状态、移除正面状态或两者都处理。这样可支持净化自身、驱散敌方增益等不同技能。

### 2.14 Buff、Debuff、被动与光环分类（V2）

`ModifierTemplate` 建议增加元数据：

| 字段 | 说明 |
|------|------|
| `ModifierKind` | Buff / Debuff / Neutral |
| `IsHidden` | 是否在 UI 中隐藏 |
| `IsPassive` | 是否为被动效果 |
| `IsAura` | 是否为光环提供者 |
| `RemoveOnDeath` | 死亡时是否移除 |
| `PersistThroughDeath` | 是否跨死亡保留 |
| `DispelRule` | 驱散规则 |
| `AffectedByStatusResistance` | 持续时间是否受状态抗性影响 |

光环应由一个“提供者 Modifier”持续查询范围，并给符合条件的单位添加“子 Modifier”：

```text
光环提供者
  → 范围半径
  → 阵营筛选（友方/敌方/双方）
  → 单位类型筛选（英雄/小兵/建筑/召唤物）
  → 子 ModifierTemplate
  → 离开范围后的 LingerDuration
```

子 Modifier 的来源必须保留为光环提供者，才能正确处理多个同类光环、来源死亡和离开范围。

### 2.15 战斗事件扩展（V2）

除现有受伤、造成伤害和死亡事件外，建议逐步补充：

| 事件 | 用途 |
|------|------|
| `OnAttackStart` | 攻击前摇开始、缴械检查 |
| `OnAttackLanded` | 攻击命中、法球与攻击特效 |
| `OnBeforeDealDamage` | 输出增伤、暴击、伤害类型转换 |
| `OnBeforeTakeDamage` | 护盾、格挡、承伤修正 |
| `OnAfterDamage` | 吸血、反伤、受击触发 |
| `OnAbilityExecuted` | 施法触发、沉默校验、技能类 Buff |
| `OnHealReceived` | 治疗增幅、禁止治疗 |
| `OnModifierAdded` | 驱散保护、状态联动 |
| `OnModifierRemoved` | Buff 结束后的连锁效果 |
| `OnKill` | 击杀成长、刷新技能 |
| `OnRespawn` | 跨死亡 Buff 恢复与初始化 |

事件分发必须使用快照或延迟队列，避免回调过程中直接增删当前正在遍历的 Modifier 集合。

---

## 三、系统接入方式

系统支持三种创建 Buff 的方式，可混合使用：

### 3.1 方式一：C# 继承 ModifierTemplate（适合复杂自定义逻辑）

直接继承 `ModifierTemplate`，在构造函数中配置属性，按需 override 生命周期方法。

下面的反伤示例使用 V2 的统一伤害入口；V1 迁移时不应再通过修改 `MaxHp` 表达伤害。

```csharp
using BuffSystem.Core;

// 示例：荆棘光环——受到伤害时反弹 20%
public class ThornsAura : ModifierTemplate, IModifierEventHandler
{
    private readonly float _reflectPercent;

    public ThornsAura(float reflectPercent = 0.2f, float duration = -1f)
    {
        Name = nameof(ThornsAura);
        Duration = duration;           // -1 = 永久
        _reflectPercent = reflectPercent;
    }

    public void OnUnitTakeDamage(Modifier modifier, float amount, IModifierUnit source)
    {
        // 反弹 20% 伤害给攻击者
        float reflect = amount * _reflectPercent;
        if (source != null)
        {
            modifier.System.DealDamage(new DamageContext
            {
                Attacker = modifier.Target,
                Target = source,
                RawDamage = reflect,
                DamageType = DamageType.Pure,
                Flags = DamageFlags.Reflection | DamageFlags.NoLifesteal
            });
        }
    }

    public void OnUnitDealDamage(Modifier modifier, float amount, IModifierUnit target) { }
    public void OnUnitDeath(Modifier modifier, IModifierUnit killer) { }
}
```

**使用**：
```csharp
system.AddModifier(unit, new ThornsAura(reflectPercent: 0.3f, duration: -1f));
```

### 3.2 方式二：ScriptableObject 配置（适合策划/设计师，无需写代码）

1. **创建 SO**：Project 窗口右键 → `Create → BuffSystem → Buff 模板`
2. **Inspector 中配置**：所有枚举字段均显示中文

| Inspector 字段 | 中文标签 | 说明 |
|----------------|---------|------|
| buffName | Buff 名称 | 唯一标识 |
| duration | 持续时间（秒） | -1 = 永久 |
| stackPolicy | 叠加策略 | 刷新/叠加/替换/独立 |
| maxStacks | 最大层数 | |
| thinkInterval | 触发间隔（秒） | -1 = 无周期触发 |
| statusEffects | 状态效果 | 下拉多选（眩晕/沉默/缴械...） |
| attributeModifiers | 属性修改列表 | 每项：属性类型(中文) + 操作(中文) + 值 + 是否按层数缩放 |
| thinkActions | 周期动作列表（V1） | 旧版周期属性修改配置 |
| effectActions | 效果动作列表（V2） | 伤害、治疗、属性修改、添加 Buff、移除 Buff 与驱散 |

**使用**：
```csharp
public ModifierTemplateSO buffSO;  // Inspector 中拖入

// 从 SO 创建 Template 并施加
system.AddModifier(unit, buffSO.CreateTemplate());
```

**SO 配置示例——「暴怒」Buff**（对应 `Assets/文档/暴怒.asset`）：

| 配置项 | 值 |
|--------|-----|
| 名称 | 暴怒 |
| 持续时间 | 30 秒 |
| 叠加策略 | 刷新（重置时间） |
| 属性修改 1 | 防御力 +10（加法） |
| 属性修改 2 | 攻击速度 +10%（百分比） |
| 属性修改 3 | 最大生命值 +100（加法） |

效果：使角色在 30 秒内获得 +10 防御、+10% 攻速、+100 最大生命值的综合提升。当前生命值如何随最大生命值变化，应由资源同步策略明确决定，而不是默认写死。

### 3.3 方式三：配置表（Luban / 服务端 JSON 等）

实现 `IModifierTemplateData` 接口，传入 `DataDrivenTemplate`：

```csharp
using BuffSystem.Core;

// 配置表行数据（Luban 生成的类实现 IModifierTemplateData 即可）
public class BuffConfigRow : IModifierTemplateData
{
    public string Name { get; set; }
    public float Duration { get; set; }
    public StackPolicy StackPolicy { get; set; }
    public int MaxStacks { get; set; }
    public float ThinkInterval { get; set; }
    public StatusEffectType StatusEffects { get; set; }
    public IReadOnlyList<AttributeModifierEntry> AttributeModifiers { get; set; }
    public IReadOnlyList<ThinkActionEntry> ThinkActions { get; set; }
}

// 使用
var data = LoadBuffConfigFromTable("PoisonDoT");  // 从配置表读取
system.AddModifier(unit, new DataDrivenTemplate(data));
```

### 3.4 数据驱动配置项

`AttributeModifierEntry`（属性修改器配置项）：

| 字段 | 类型 | 说明 |
|------|------|------|
| Type | AttributeType | 修改的属性类型 |
| Op | AttributeOp | 修改操作（加法/百分比） |
| Value | float | 修改值 |
| ScaleByStacks | bool | 为 true 时实际值 = Value × 当前层数 |

`ThinkActionEntry`（周期动作配置项）：

| 字段 | 类型 | 说明 |
|------|------|------|
| TargetAttribute | AttributeType | 修改的目标属性 |
| Value | float | V1 属性修改值；旧配置曾用负数模拟伤害，V2 中仅用于普通属性修改 |
| ScaleByStacks | bool | 为 true 时实际值 = Value × 当前层数 |

`ThinkActionEntry` 属于 V1 简化模型。V2 不再用“负数修改 MaxHp”表达伤害，建议替换为统一的 `EffectActionEntry`：

| 字段 | 类型 | 说明 |
|------|------|------|
| ActionType | EffectActionType | DealDamage / Heal / ModifyAttribute / ApplyModifier / RemoveModifier / Dispel |
| TargetSelector | TargetSelector | Self / Source / Target / AuraTargets 等 |
| Value | float | 基础数值 |
| ScaleByStacks | bool | 是否乘以当前层数 |
| DamageType | DamageType | DealDamage 时使用 |
| AttributeType | AttributeType | ModifyAttribute 时使用 |
| ModifierTemplateId | string | ApplyModifier / RemoveModifier 时使用 |
| DispelType | DispelType | Dispel 时使用 |

这样配置表、ScriptableObject 和 C# 模板最终都会走同一套伤害、治疗、属性与驱散管线。

---

## 四、示例 Buff

### 4.1 内置 C# 示例

| Buff | 效果 | 持续时间 | 叠加策略 | 实现方式 |
|------|------|---------|---------|---------|
| `AttackPowerBuff` | Attack +20 | 10s | Refresh | 静态 `AttributeModifier` |
| `PoisonDoT` | 每秒 10 伤害 × 层数 | 5s | Stack（最大 5 层） | `override OnIntervalThink` |
| `SlowDebuff` | 移速 -30% × 层数 | 3s | Stack（最大 3 层） | `override GetAttributeModifiers` |
| `StunDebuff` | Stun 状态（无法行动） | 2s | Refresh | `StatusEffects = Stun` |

### 4.2 各实现方式对应代码

**静态属性修改（AttackPowerBuff）**：
```csharp
public AttackPowerBuff(float attackBonus = 20f, float duration = 10f)
{
    Name = nameof(AttackPowerBuff);
    Duration = duration;
    StackPolicy = StackPolicy.Refresh;
    AttributeModifiers.Add(new AttributeModifier(
        AttributeType.Attack, AttributeOp.Add, attackBonus));
}
```

**周期触发 + 层数缩放（PoisonDoT）**：
```csharp
public PoisonDoT(float damagePerTick = 10f, float duration = 5f, int maxStacks = 5)
{
    Name = nameof(PoisonDoT);
    Duration = duration;
    StackPolicy = StackPolicy.Stack;
    MaxStacks = maxStacks;
    ThinkInterval = 1f;
    DamagePerTick = damagePerTick;
}

public override void OnIntervalThink(Modifier modifier)
{
    float damage = DamagePerTick * modifier.StackCount;  // 伤害随层数增长
    modifier.System.DealDamage(new DamageContext
    {
        Attacker = modifier.Source,
        Target = modifier.Target,
        RawDamage = damage,
        DamageType = DamageType.Magical,
        SourceModifier = modifier
    });
}
```

**动态属性修改（SlowDebuff）**：
```csharp
public override void GetAttributeModifiers(Modifier modifier, List<AttributeModifier> results)
{
    float totalSlow = SlowPercentPerStack * modifier.StackCount;  // 减速随层数增长
    results.Add(new AttributeModifier(
        AttributeType.MoveSpeed, AttributeOp.PercentAdd, -totalSlow));
}
```

**状态控制（StunDebuff）**：
```csharp
public StunDebuff(float duration = 2f)
{
    Name = nameof(StunDebuff);
    Duration = duration;
    StackPolicy = StackPolicy.Refresh;
    StatusEffects = StatusEffectType.Stun;
}
```

---

## 五、测试使用方式

### 5.1 Editor 测试窗口（无需场景）

**打开方式**：菜单栏 → `Tools → Buff 系统测试窗口`

**窗口功能**：

| 区域 | 功能 |
|------|------|
| **工具栏** | 自动更新开关 / 推进 0.1 秒 / 推进 1 秒 / 重置 / 模拟时间显示 |
| **单位列表** | V1 显示生命、攻击、防御、速度；V2 增加等级、主属性、力量/敏捷/智力、魔法、护甲、攻速、恢复、魔抗与状态抗性 |
| **Buff 列表** | 每个单位下方显示所有生效中的 Buff：名称 × 层数（剩余时间）[状态效果]，可逐个移除 |
| **快捷按钮** | 每个单位下：+攻击力 / +中毒 / +减速 / +眩晕 / 清除全部 |
| **SO 配置 Buff** | 拖入 ModifierTemplateSO 资产，可施加给英雄/哥布林/全体 |
| **批量操作** | 全体 +攻击力 / 全体 +中毒 / 全体 +减速 / 全体 +眩晕 / 全体清除 |
| **战斗模拟** | 选择攻击者 → 目标，攻击一次 / 攻击 ×5 / 全员复活 |
| **添加单位** | 输入名称添加自定义测试单位 |

**预置单位**：
- 英雄：HP 200 / ATK 30 / DEF 15 / SPD 6
- 哥布林：HP 100 / ATK 15 / DEF 5 / SPD 4

**V2 建议新增预置英雄**：

- 力量英雄：用于验证力量生命收益、主属性攻击力和控制承受
- 敏捷英雄：用于验证护甲、攻击速度和攻击间隔
- 智力英雄：用于验证魔法值、魔法恢复和魔抗
- 全才英雄：用于验证三维总和按全才系数转换攻击力
- 无主属性单位：用于验证小兵、建筑不会从三维获得主攻击力

### 5.2 Play Mode 场景测试

**步骤**：

1. 在场景中创建空 GameObject，挂载 `ModifierSystemDriver` 组件
2. 在角色 GameObject 上挂载 `ModifierUnit` 组件，在 Inspector 配置基础属性
3. 在脚本中调用 API：

```csharp
using BuffSystem.Core;
using BuffSystem.Core.Templates;
using BuffSystem.Unity;

public class BuffExample : MonoBehaviour
{
    void Start()
    {
        // 获取系统（ModifierSystemDriver 自动创建）
        ModifierSystem system = ModifierSystemDriver.System;

        // 获取挂载了 ModifierUnit 的角色
        ModifierUnit unit = GetComponent<ModifierUnit>();

        // ===== 添加 Buff =====
        // 方式一：C# 模板
        system.AddModifier(unit, new AttackPowerBuff(attackBonus: 50, duration: 15f));
        system.AddModifier(unit, new PoisonDoT(damagePerTick: 10, duration: 5f, maxStacks: 5));

        // 方式二：SO 配置（需在 Inspector 拖入 buffSO）
        // system.AddModifier(unit, buffSO.CreateTemplate());

        // ===== 查询 =====
        // 最终属性（含 Buff 修正）
        float finalAtk = system.GetFinalAttribute(unit, AttributeType.Attack);
        float finalMaxHp = system.GetFinalAttribute(unit, AttributeType.MaxHp);

        // 状态检查
        bool isStunned = system.HasStatusEffect(unit, StatusEffectType.Stun);
        bool isSlowed = system.HasStatusEffect(unit, StatusEffectType.Slow);

        // Buff 查询
        bool hasPoison = system.HasModifier(unit, "PoisonDoT");
        Modifier poison = system.GetModifier(unit, "PoisonDoT");
        if (poison != null)
            Debug.Log($"毒素层数：{poison.StackCount}，剩余：{poison.RemainingDuration:F1}s");

        // ===== 移除 =====
        system.RemoveModifier(unit, nameof(AttackPowerBuff));
        // system.RemoveAllModifiers(unit);
    }

    void Update()
    {
        ModifierSystem system = ModifierSystemDriver.System;
        ModifierUnit unit = GetComponent<ModifierUnit>();

        // 每帧查询最终属性用于战斗逻辑
        float atk = system.GetFinalAttribute(unit, AttributeType.Attack);
        float def = system.GetFinalAttribute(unit, AttributeType.Defense);

        // 检查眩晕状态，眩晕时无法行动
        if (system.HasStatusEffect(unit, StatusEffectType.Stun))
            return; // 跳过本帧行动

        // 正常逻辑...
    }
}
```

### 5.3 纯 C# 环境（服务端 / 单元测试）

无需 Unity，直接使用 `BuffSystem.Core` 程序集：

```csharp
using BuffSystem.Core;
using BuffSystem.Core.Templates;

// 创建系统
var system = new ModifierSystem();

// 创建单位（实现 IModifierUnit）
var hero = new MyUnit("Hero", maxHp: 200, attack: 30);

// 添加 Buff
system.AddModifier(hero, new AttackPowerBuff(attackBonus: 30, duration: 10f));

// 查询最终属性
float finalAtk = system.GetFinalAttribute(hero, AttributeType.Attack);
// finalAtk = 30 + 30 = 60

// 每帧更新
system.Update(deltaTime);

// 移除
system.RemoveModifier(hero, "AttackPowerBuff");
```

---

## 六、测试案例

以下是 V1 系统的 8 个核心测试用例，以及 V2 属性与战斗体系必须新增的测试清单。

### 测试 1：攻击力提升

```
基础 ATK = 20
施加 AttackPowerBuff(+30 ATK, 10s)
期望最终 ATK = 50
结果：✅ 通过
```

### 测试 2：毒素叠加

```
对哥布林连续施加 3 次 PoisonDoT
期望 StackCount = 3
结果：✅ 通过
```

### 测试 3：毒素周期伤害

```
毒素 3 层，每层每秒 10 伤害
推进 1 秒后
期望伤害 = 10 × 3 = 30
哥布林 CurrentHp：80 → 50，MaxHp 保持不变
结果：✅ 通过
```

### 测试 4：眩晕状态

```
施加 StunDebuff(2s)
HasStatusEffect(Stun) = true
结果：✅ 通过
```

### 测试 5：减速动态属性

```
基础 Speed = 5
施加 2 层 SlowDebuff（每层 -30%）
期望最终 Speed = 5 × (1 - 0.6) = 2.0
结果：✅ 通过
```

### 测试 6：持续时间过期

```
眩晕 2 秒，推进 2.1 秒后
HasStatusEffect(Stun) = false（Buff 已过期移除）
结果：✅ 通过
```

### 测试 7：手动移除 Buff

```
施加 AttackPowerBuff 后移除
HasModifier = false
最终 ATK 恢复为基础值 20
结果：✅ 通过
```

### 测试 8：SO 配置 Buff（暴怒）

```
SO 配置：
  - 防御力 +10（加法）
  - 攻击速度 +10%（百分比）
  - 最大生命值 +100（加法）

施加后：
  - DEF：base + 10 ✅
  - AttackSpeed：base × (1 + 0.1) ✅
  - MaxHp：base + 100 ✅
  - CurrentHp：按该 Buff 配置的资源同步策略处理 ✅
```

### V2 测试 9：力量派生生命与生命恢复

```text
英雄基础力量 = 20
配置：每点力量 +22 MaxHp，+0.1 HpRegen
期望力量派生：MaxHp +440，HpRegen +2
施加 +10 力量 Buff 后：额外 MaxHp +220，HpRegen +1
```

### V2 测试 10：敏捷派生护甲与攻击速度

```text
英雄最终敏捷 = 30
期望敏捷派生：Armor +5，AttackSpeed +30
修改敏捷后必须同步刷新 AttackInterval
```

### V2 测试 11：智力派生魔法与魔抗

```text
英雄最终智力 = 25
期望智力派生：MaxMana +300，ManaRegen +1.25，MagicResistance +2.5%
魔抗与其他来源按配置的乘法规则组合
```

### V2 测试 12：主属性与全才英雄攻击力

```text
力量英雄最终力量 = 40 → 主属性攻击力 +40
全才英雄 STR/AGI/INT = 30/20/10
全才系数 = 0.45
期望主属性攻击力 = (30 + 20 + 10) × 0.45 = 27
```

### V2 测试 13：三维 Buff 与直接生命 Buff 的计算顺序

```text
基础力量 20，+50% 力量，直接 +200 MaxHp
先得到最终力量 30，再生成力量派生生命 660，最后叠加直接生命 200
不得先生成派生属性再对力量做百分比修改
```

### V2 测试 14：伤害类型与抗性

```text
同一目标分别受到 100 物理、100 魔法、100 纯粹伤害
物理伤害仅经过护甲
魔法伤害仅经过魔抗
纯粹伤害默认忽略护甲与魔抗
CurrentHp 改变，但 MaxHp 不得改变
```

### V2 测试 15：状态抗性与强驱散

```text
目标状态抗性 = 30%
施加 2 秒可缩短眩晕 → 实际持续 1.4 秒
基础驱散不能移除 StrongDispellable 眩晕
强驱散可以移除
```

### V2 测试 16：多来源独立叠加与光环

```text
两个施放者对同一目标施加同名毒素
按 SourceUnitId 分别维护层数与持续时间
移除其中一个来源时，不影响另一个来源的实例
光环来源离开或死亡时，仅移除对应来源的子 Modifier
```

---

## 七、API 速查

### ModifierSystem

| 方法 | 说明 |
|------|------|
| `AddModifier(target, template, source?, durationOverride?)` | 添加 Buff，返回 Modifier 实例 |
| `RemoveModifier(target, templateName)` | 按名称移除，返回是否成功 |
| `RemoveAllModifiers(target)` | 移除目标所有 Buff |
| `HasModifier(target, templateName)` | 是否拥有指定 Buff |
| `GetModifier(target, templateName)` | 获取指定 Buff（null = 不存在） |
| `GetModifiers(target)` | 获取所有 Buff 列表 |
| `HasStatusEffect(target, effect)` | 是否处于指定状态效果下 |
| `GetFinalAttribute(target, type)` | 获取最终属性值（含 Buff 修正） |
| `Update(deltaTime)` | 每帧更新（持续时间 / 周期触发 / 过期移除） |
| `DispatchUnitTakeDamage(unit, amount, source)` | 分发受到伤害事件 |
| `DispatchUnitDealDamage(unit, amount, target)` | 分发造成伤害事件 |
| `DispatchUnitDeath(unit, killer)` | 分发死亡事件 |
| `Clear()` | 清理系统所有数据 |

### ModifierSystem V2 扩展 API

| 方法 | 说明 |
|------|------|
| `GetAttributeSnapshot(target)` | 一次性获得最终三维、资源上限、攻防与功能属性，避免同一逻辑重复计算 |
| `GetPrimaryAttributeDamage(target)` | 获取主属性或全才属性提供的主攻击力 |
| `SetUnitLevel(target, level)` | 更新等级并使成长属性与派生属性失效重算 |
| `DealDamage(context)` | 通过 DamageResolver 统一结算伤害 |
| `Heal(context)` | 统一结算治疗、治疗增幅和禁止治疗效果 |
| `Dispel(target, dispelType, targetKind)` | 执行基础/强驱散，并指定移除正面或负面 Modifier |
| `AddAuraProvider(target, definition)` | 注册光环提供者 |
| `InvalidateAttributes(target)` | 标记属性缓存失效；添加、移除、叠层或刷新相关 Modifier 时自动调用 |

### 单位资源接口（V2）

`MaxHp`、`MaxMana` 属于可计算属性，`CurrentHp`、`CurrentMana` 属于运行时资源，二者不能混用：

```csharp
public interface ICombatUnit : IModifierUnit
{
    float CurrentHp { get; }
    float CurrentMana { get; }

    void SetCurrentHp(float value);
    void SetCurrentMana(float value);
}
```

最大生命值发生变化时，需要明确项目策略：保持当前生命数值、保持生命百分比，或仅对特定来源同步增加当前生命。该策略不能隐藏在 `ModifyBaseAttribute()` 中。

### ModifierTemplate 配置项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Name` | string | - | Buff 唯一标识 |
| `Duration` | float | -1 | 持续时间（秒），-1 = 永久 |
| `StackPolicy` | StackPolicy | Refresh | 叠加策略 |
| `MaxStacks` | int | 1 | 最大叠加层数 |
| `ThinkInterval` | float | -1 | 周期触发间隔（秒），-1 = 不触发 |
| `StatusEffects` | StatusEffectType | None | 静态状态效果 |
| `AttributeModifiers` | List\<AttributeModifier\> | 空 | 静态属性修改器列表 |

### ModifierTemplate 生命周期虚方法

| 方法 | 触发时机 |
|------|---------|
| `OnCreated(modifier)` | Buff 首次创建 |
| `OnRefresh(modifier)` | 重新施放（Refresh/Stack 策略） |
| `OnStackChanged(modifier)` | 层数变化 |
| `OnIntervalThink(modifier)` | ThinkInterval 周期到达 |
| `OnDestroy(modifier)` | 过期或手动移除 |
| `GetAttributeModifiers(modifier, results)` | 查询属性修改器（可 override 实现动态逻辑） |
| `GetStatusEffects(modifier)` | 查询状态效果（可 override 实现条件性效果） |

### Modifier 运行时属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `UniqueId` | int | 全局唯一 ID |
| `System` | ModifierSystem | 所属系统引用，用于统一伤害、治疗、驱散与事件分发 |
| `Template` | ModifierTemplate | 模板引用 |
| `Target` | IModifierUnit | 挂载目标 |
| `Source` | IModifierUnit | 施放来源 |
| `StackCount` | int | 当前层数 |
| `RemainingDuration` | float | 剩余时间（-1 = 永久） |
| `ThinkTimer` | float | Think 计时器 |
| `IsExpired` | bool | 是否已过期 |
| `IsActive` | bool | 是否激活 |
| `Name` | string | 便捷访问 = Template.Name |
| `IsPermanent` | bool | 是否永久 Buff |

---

## 八、扩展指南

### 添加新属性类型

V1 仍可按以下方式扩展：

1. 在 `AttributeType` 枚举中添加新值
2. 在 `ModifierUnit.GetBaseAttribute()` 中增加对应分支
3. 在 `ModifierUnit.ModifyBaseAttribute()` 中增加对应处理
4. 在 `EnumChineseDrawer.cs` 的 `AttributeTypeDrawer._labels` 中添加中文名

V2 应逐步改为数据字典或属性存储容器，并由 `AttributeCalculator` 注册计算规则，避免每增加一个属性就在多个 `switch` 中同步修改。三维派生属性必须在 `AttributeFormulaConfig` 中增加系数，而不是写入单位类。

### 添加新状态效果

1. 在 `StatusEffectType` Flags 枚举中添加新值（`1 << N`）
2. 在 `EnumChineseDrawer.cs` 的 `StatusEffectTypeDrawer._labels` 中添加中文名
3. 在游戏逻辑中通过 `system.HasStatusEffect(unit, effect)` 检查

### 添加新叠加策略

1. 在 `StackPolicy` 枚举中添加新值
2. 在 `ModifierSystem.AddModifier()` 的 `switch` 语句中增加处理逻辑
3. 在 `EnumChineseDrawer.cs` 的 `StackPolicyDrawer._labels` 中添加中文名

### 性能设计

- `ModifierSystem` 使用 `Dictionary<IModifierUnit, List<Modifier>>` 实现 O(1) 单位查找
- `GetFinalAttribute` 使用可复用的 `List<AttributeModifier>` 缓冲区，避免 GC
- 过期 Buff 在遍历结束后统一移除，避免遍历中修改集合
- `AttributeModifier` 为 `readonly struct`，零堆分配
- V2 为每个单位维护 `AttributeSnapshot` 与 Dirty Flag；仅在等级、装备、Modifier、层数或配置变化时重算
- 同一帧的技能、UI 和 AI 查询共享只读快照，避免重复遍历所有 Modifier
- 光环系统使用空间分区或分帧查询，不应让每个光环在每帧遍历全部单位
- 伤害与事件回调使用对象池或值类型上下文，减少高频战斗中的临时分配

---

## 九、V2 实施路线

### 9.1 第一阶段：三维与属性快照

1. 新增 `PrimaryAttributeType`、三维属性与 `HeroAttributeProfile`
2. 新增 `AttributeFormulaConfig`
3. 实现 `AttributeCalculator` 和 `AttributeSnapshot`
4. 将 `Attack`、`Defense` 逐步迁移为 `AttackDamage`、`Armor`
5. 为 Editor 测试窗口增加力量、敏捷、智力、主属性和派生属性显示

**完成标准**：力量、敏捷、智力 Buff 能实时正确影响生命、恢复、护甲、攻速、魔法与攻击力。

### 9.2 第二阶段：当前资源与伤害系统

1. 分离 `CurrentHp` / `MaxHp`、`CurrentMana` / `MaxMana`
2. 新增 `DamageContext`、`DamageType`、`DamageFlags` 和 `DamageResolver`
3. 把 PoisonDoT、反伤和测试窗口攻击全部迁移到 `DealDamage()`
4. 增加物理、魔法、纯粹伤害与抗性测试

**完成标准**：任何伤害都不会修改最大生命值，所有伤害事件具有统一来源和结算结果。

### 9.3 第三阶段：完整 Modifier 规则

1. 增加 `ModifierStackKey` 和多来源叠加规则
2. 增加 Buff/Debuff/Passive/Aura 元数据
3. 增加状态抗性、基础驱散、强驱散和死亡清理规则
4. 引入延迟增删队列，保证事件回调期间集合安全

**完成标准**：多施放者毒素、净化、强驱散、跨死亡 Buff 和唯一被动均可自动化测试。

### 9.4 第四阶段：光环与数据驱动动作

1. 实现 `AuraDefinition` 与 `AuraSystem`
2. 用 `EffectActionEntry` 替换“负数修改 MaxHp”的旧周期动作
3. 让 C#、SO、Luban/JSON 共用同一套 Effect Action
4. 增加光环范围、阵营筛选、残留时间与来源移除测试

**完成标准**：策划无需写代码即可配置周期伤害、治疗、属性修改、施加 Buff 和驱散效果。

### 9.5 兼容与迁移原则

- 保留 V1 公共 API 一段迁移期，并为废弃接口添加 `[Obsolete]` 提示
- 旧 `Attack` 映射到 `AttackDamage`，旧 `Defense` 映射到 `Armor`
- 旧 `ThinkActionEntry` 读取时转换为 `EffectActionEntry`，但禁止继续创建“修改 MaxHp 作为伤害”的新配置
- ScriptableObject 资产升级需要版本号和迁移脚本，避免字段改名导致已有资源丢失
- 客户端与服务端共享公式配置版本；战斗回放或同步包中记录配置版本号

### 9.6 总体验收清单

- [ ] 英雄拥有等级、力量、敏捷、智力和主属性类型
- [ ] 三维派生系数可配置，客户端与服务端计算一致
- [ ] 全才英雄攻击力按三维总和计算
- [ ] CurrentHp/CurrentMana 与 MaxHp/MaxMana 完全分离
- [ ] 物理、魔法、纯粹与生命移除拥有统一结算入口
- [ ] Modifier 支持多来源叠加、驱散、状态抗性、死亡规则与光环来源
- [ ] 所有属性变化通过 Dirty Flag 刷新快照
- [ ] Editor 测试窗口可以观察三维、派生属性、伤害明细与状态剩余时间
- [ ] V1 和 V2 测试均可在纯 C# 环境运行，不依赖 Unity 场景

---

## 十、参考与数值版本说明

- Dota 2 属性体系参考：[Liquipedia Dota 2 - Attributes](https://liquipedia.net/dota2/Attributes)
- 本文参考值包括：力量提供生命与生命恢复、敏捷提供护甲与攻速、智力提供魔法与魔抗，以及全才英雄的三维攻击力系数。
- 参考页面和游戏版本可能继续调整。项目实际运行时以 `AttributeFormulaConfig` 和服务端发布的平衡配置为准。
