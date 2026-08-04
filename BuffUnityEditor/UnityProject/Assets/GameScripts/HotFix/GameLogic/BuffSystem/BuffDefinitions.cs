using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using GameplayTags;

namespace GameLogic.Buffs
{
    public static class BuffSchema
    {
        public const string CurrentVersion = "2.0.0";
        public const string DefaultConfigLocation = "buff_system_data";
    }

    public static class CombatAttributeNames
    {
        public const string Strength = "Strength";
        public const string Agility = "Agility";
        public const string Intelligence = "Intelligence";
        public const string MaxHp = "MaxHp";
        public const string HpRegen = "HpRegen";
        public const string MaxMana = "MaxMana";
        public const string ManaRegen = "ManaRegen";
        public const string AttackDamage = "AttackDamage";
        public const string AttackSpeed = "AttackSpeed";
        public const string BaseAttackTime = "BaseAttackTime";
        public const string AttackRange = "AttackRange";
        public const string Armor = "Armor";
        public const string MagicResistance = "MagicResistance";
        public const string StatusResistance = "StatusResistance";
        public const string Evasion = "Evasion";
        public const string MoveSpeed = "MoveSpeed";
        public const string CastRange = "CastRange";
        public const string CooldownReduction = "CooldownReduction";
        public const string SpellAmplification = "SpellAmplification";
        public const string CritChance = "CritChance";
        public const string CritDamage = "CritDamage";
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PrimaryAttributeType
    {
        None,
        Strength,
        Agility,
        Intelligence,
        Universal,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffModifierKind
    {
        Buff,
        Debuff,
        Neutral,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffStackPolicy
    {
        Refresh,
        Stack,
        Replace,
        Independent,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffDispelRule
    {
        NotDispellable,
        BasicDispellable,
        StrongDispellable,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffAttributeOp
    {
        Add,
        PercentAdd,
        PercentMultiply,
        Override,
        Min,
        Max,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffDamageType
    {
        Physical,
        Magical,
        Pure,
        HpRemoval,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffEffectActionType
    {
        DealDamage,
        Heal,
        ModifyAttribute,
        ApplyModifier,
        RemoveModifier,
        RefreshModifier,
        Dispel,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffTargetSelector
    {
        Self,
        Source,
        Target,
        AuraTargets,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffEffectTrigger
    {
        OnCreated,
        OnRefresh,
        OnStackChanged,
        OnIntervalThink,
        OnAttackLanded,
        OnTakeDamage,
        OnDealDamage,
        OnDestroy,
        OnCustomEvent,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuffDispelType
    {
        Basic,
        Strong,
    }

    [Serializable]
    public sealed class BuffSystemData
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = BuffSchema.CurrentVersion;

        [JsonProperty("exportedAt")]
        public string ExportedAt { get; set; } = string.Empty;

        [JsonProperty("attributeFormula")]
        public AttributeFormulaConfig AttributeFormula { get; set; } = new AttributeFormulaConfig();

        [JsonProperty("buffs")]
        public List<BuffTemplate> Buffs { get; set; } = new List<BuffTemplate>();
    }

    [Serializable]
    public sealed class AttributeFormulaConfig
    {
        [JsonProperty("primaryAttributeDefault")]
        public PrimaryAttributeType PrimaryAttributeDefault { get; set; } = PrimaryAttributeType.Strength;

        [JsonProperty("strengthToMaxHp")]
        public float StrengthToMaxHp { get; set; } = 22f;

        [JsonProperty("strengthToHpRegen")]
        public float StrengthToHpRegen { get; set; } = 0.1f;

        [JsonProperty("agilityToArmor")]
        public float AgilityToArmor { get; set; } = 1f / 6f;

        [JsonProperty("agilityToAttackSpeed")]
        public float AgilityToAttackSpeed { get; set; } = 1f;

        [JsonProperty("intelligenceToMaxMana")]
        public float IntelligenceToMaxMana { get; set; } = 12f;

        [JsonProperty("intelligenceToManaRegen")]
        public float IntelligenceToManaRegen { get; set; } = 0.05f;

        [JsonProperty("intelligenceToMagicResistance")]
        public float IntelligenceToMagicResistance { get; set; } = 0.001f;

        [JsonProperty("primaryAttributeToAttackDamage")]
        public float PrimaryAttributeToAttackDamage { get; set; } = 1f;

        [JsonProperty("universalAttributeToAttackDamage")]
        public float UniversalAttributeToAttackDamage { get; set; } = 0.45f;

        [JsonProperty("minAttackSpeed")]
        public float MinAttackSpeed { get; set; } = 20f;

        [JsonProperty("maxAttackSpeed")]
        public float MaxAttackSpeed { get; set; } = 700f;

        [JsonProperty("physicalArmorFactor")]
        public float PhysicalArmorFactor { get; set; } = 0.06f;
    }

    [Serializable]
    public sealed class BuffAttributeModifier
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("attributeType")]
        public string AttributeType { get; set; } = string.Empty;

        [JsonProperty("op")]
        public BuffAttributeOp Op { get; set; }

        [JsonProperty("value")]
        public float Value { get; set; }

        [JsonProperty("scaleByStacks")]
        public bool ScaleByStacks { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }
    }

    [Serializable]
    public sealed class BuffEffectCondition
    {
        [JsonProperty("healthPercentMin")]
        public float? HealthPercentMin { get; set; }

        [JsonProperty("healthPercentMax")]
        public float? HealthPercentMax { get; set; }

        [JsonProperty("requiredStatusEffect")]
        public string RequiredStatusEffect { get; set; }
    }

    [Serializable]
    public sealed class BuffEffectAction
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("trigger")]
        public BuffEffectTrigger Trigger { get; set; }

        [JsonProperty("actionType")]
        public BuffEffectActionType ActionType { get; set; }

        [JsonProperty("targetSelector")]
        public BuffTargetSelector TargetSelector { get; set; }

        [JsonProperty("value")]
        public float Value { get; set; }

        [JsonProperty("scaleByStacks")]
        public bool ScaleByStacks { get; set; }

        [JsonProperty("damageType")]
        public BuffDamageType? DamageType { get; set; }

        [JsonProperty("attributeType")]
        public string AttributeType { get; set; } = string.Empty;

        [JsonProperty("modifierTemplateId")]
        public string ModifierTemplateId { get; set; } = string.Empty;

        [JsonProperty("dispelType")]
        public BuffDispelType? DispelType { get; set; }

        [JsonProperty("eventName")]
        public string EventName { get; set; } = string.Empty;

        [JsonProperty("condition")]
        public BuffEffectCondition Condition { get; set; }
    }

    [Serializable]
    public sealed class BuffTemplate
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("modifierKind")]
        public BuffModifierKind ModifierKind { get; set; }

        [JsonProperty("duration")]
        public float Duration { get; set; } = 10f;

        [JsonProperty("stackPolicy")]
        public BuffStackPolicy StackPolicy { get; set; }

        [JsonProperty("maxStacks")]
        public int MaxStacks { get; set; } = 1;

        [JsonProperty("thinkInterval")]
        public float ThinkInterval { get; set; } = -1f;

        [JsonProperty("statusEffects")]
        public List<string> StatusEffects { get; set; } = new List<string>();

        [JsonProperty("dispelRule")]
        public BuffDispelRule DispelRule { get; set; }

        [JsonProperty("affectedByStatusResistance")]
        public bool AffectedByStatusResistance { get; set; }

        [JsonProperty("removeOnDeath")]
        public bool RemoveOnDeath { get; set; } = true;

        [JsonProperty("isHidden")]
        public bool IsHidden { get; set; }

        [JsonProperty("isPassive")]
        public bool IsPassive { get; set; }

        [JsonProperty("isAura")]
        public bool IsAura { get; set; }

        [JsonProperty("auraRadius")]
        public float AuraRadius { get; set; }

        [JsonProperty("lingerDuration")]
        public float LingerDuration { get; set; }

        [JsonProperty("attributeModifiers")]
        public List<BuffAttributeModifier> AttributeModifiers { get; set; } = new List<BuffAttributeModifier>();

        [JsonProperty("effectActions")]
        public List<BuffEffectAction> EffectActions { get; set; } = new List<BuffEffectAction>();

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("applyChance")]
        public float ApplyChance { get; set; } = 1f;

        [JsonProperty("applyCooldown")]
        public float ApplyCooldown { get; set; }

        [JsonProperty("requiredStatusEffects")]
        public List<string> RequiredStatusEffects { get; set; } = new List<string>();

        [JsonIgnore]
        public GameplayTagContainer GameplayTags { get; internal set; }

        [JsonIgnore]
        public bool IsPermanent => IsPassive || Duration < 0f;
    }
}
