# Unity Buff System

Unity 运行时实现位于 TEngine 的 `GameLogic` 热更程序集，配置默认从
`Assets/AssetRaw/Configs/buff_system_data.json` 通过 YooAsset 异步加载。

## 已支持

- Web Schema `2.x` JSON 读取与字段校验
- `Refresh`、`Stack`、`Replace`、`Independent` 四种叠层策略
- 持续时间、永久效果、状态抗性、周期触发和死亡移除
- `Add`、`PercentAdd`、`PercentMultiply`、`Override`、`Min`、`Max` 属性运算
- Dota 风格力量、敏捷、智力、全才主属性换算
- 物理、魔法、纯粹、生命移除伤害
- 治疗、施加效果、移除效果、弱驱散、强驱散
- 创建、刷新、层数变化、周期、攻击命中、受伤、造成伤害、移除触发
- 状态效果聚合及眩晕、沉默、缴械、破坏、虚无、减益免疫等基础判断
- 友军光环、范围检测和离开范围后的 lingerDuration 延迟消失
- TEngine `GameEvent` 运行时通知

## 初始化

`GameApp` 已在热更入口自动初始化：

```csharp
await BuffSystemService.Instance.InitializeAsync();
```

## 创建战斗单位

```csharp
var baseAttributes = new Dictionary<string, float>
{
    [CombatAttributeNames.Strength] = 20,
    [CombatAttributeNames.Agility] = 15,
    [CombatAttributeNames.Intelligence] = 18,
    [CombatAttributeNames.MaxHp] = 120,
    [CombatAttributeNames.MaxMana] = 75,
    [CombatAttributeNames.AttackDamage] = 28,
    [CombatAttributeNames.Armor] = 1,
    [CombatAttributeNames.AttackSpeed] = 100,
    [CombatAttributeNames.MoveSpeed] = 300,
};

BuffUnit hero = BuffSystemService.Instance.CreateUnit(
    "Hero",
    PrimaryAttributeType.Strength,
    teamId: 1,
    baseAttributes: baseAttributes);
```

## 施加与移除

```csharp
hero.ApplyBuff("PoisonDoT", enemy);
hero.ApplyBuff("demo-berserker-stacks", hero);
hero.RemoveBuff("PoisonDoT");
hero.Dispel(BuffDispelType.Strong, BuffModifierKind.Debuff);
```

## 战斗接入

```csharp
BuffDamageResult result = target.TakeDamage(100, BuffDamageType.Magical, caster);
target.Heal(50, healer);
attacker.NotifyAttackLanded(target);
```

## 监听 TEngine 事件

```csharp
GameEvent.AddEventListener<BuffRuntimeEvent>(BuffEventIds.BuffApplied, OnBuffApplied);
GameEvent.AddEventListener<BuffRuntimeEvent>(BuffEventIds.DamageTaken, OnDamageTaken);
```

非 UI 系统销毁时必须移除事件；`UIWindow` 中使用 `AddUIEvent<BuffRuntimeEvent>` 自动清理。
