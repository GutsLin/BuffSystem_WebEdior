export const primaryAttributeTypes = ['None', 'Strength', 'Agility', 'Intelligence', 'Universal'] as const;
export const modifierKinds = ['Buff', 'Debuff', 'Neutral'] as const;
export const stackPolicies = ['Refresh', 'Stack', 'Replace', 'Independent'] as const;
export const dispelRules = ['NotDispellable', 'BasicDispellable', 'StrongDispellable'] as const;
export const attributeOps = ['Add', 'PercentAdd', 'PercentMultiply', 'Override', 'Min', 'Max'] as const;
export const damageTypes = ['Physical', 'Magical', 'Pure', 'HpRemoval'] as const;
export const effectActionTypes = [
  'DealDamage',
  'Heal',
  'ModifyAttribute',
  'ApplyModifier',
  'RemoveModifier',
  'RefreshModifier',
  'Dispel',
] as const;
export const targetSelectors = ['Self', 'Source', 'Target', 'AuraTargets'] as const;
export const effectTriggers = [
  'OnCreated',
  'OnRefresh',
  'OnStackChanged',
  'OnIntervalThink',
  'OnAttackLanded',
  'OnTakeDamage',
  'OnDealDamage',
  'OnDestroy',
  'OnCustomEvent',
] as const;

export const attributeTypeOptions = [
  'Strength',
  'Agility',
  'Intelligence',
  'MaxHp',
  'HpRegen',
  'MaxMana',
  'ManaRegen',
  'AttackDamage',
  'AttackSpeed',
  'BaseAttackTime',
  'AttackRange',
  'Armor',
  'MagicResistance',
  'StatusResistance',
  'Evasion',
  'MoveSpeed',
  'CastRange',
  'CooldownReduction',
  'SpellAmplification',
  'CritChance',
  'CritDamage',
] as const;

export const statusEffectOptions = [
  'Stun',
  'Hex',
  'Taunt',
  'Fear',
  'Silence',
  'Muted',
  'Disarm',
  'Blind',
  'Root',
  'Leash',
  'Slow',
  'Break',
  'Invulnerable',
  'DebuffImmune',
  'Untargetable',
  'Ethereal',
] as const;

export const optionLabelMap: Record<string, string> = {
  None: '无',
  Strength: '力量',
  Agility: '敏捷',
  Intelligence: '智力',
  Universal: '全才',
  Buff: '增益',
  Debuff: '减益',
  Neutral: '中性',
  Refresh: '刷新持续时间',
  Stack: '叠加层数',
  Replace: '替换旧效果',
  Independent: '独立实例',
  NotDispellable: '不可驱散',
  BasicDispellable: '可弱驱散',
  StrongDispellable: '可强驱散',
  Add: '固定加法',
  PercentAdd: '百分比加法',
  PercentMultiply: '百分比乘算',
  Override: '覆盖数值',
  Min: '最小值限制',
  Max: '最大值限制',
  Physical: '物理',
  Magical: '魔法',
  Pure: '纯粹',
  HpRemoval: '生命移除',
  DealDamage: '造成伤害',
  Heal: '治疗',
  ModifyAttribute: '修改属性',
  ApplyModifier: '施加效果',
  RemoveModifier: '移除效果',
  RefreshModifier: '刷新效果',
  Dispel: '驱散',
  Self: '自身',
  Source: '来源单位',
  Target: '目标单位',
  AuraTargets: '光环目标',
  OnCreated: '创建时',
  OnRefresh: '刷新时',
  OnStackChanged: '层数变化时',
  OnIntervalThink: '周期触发',
  OnAttackLanded: '攻击命中时',
  OnTakeDamage: '受到伤害时',
  OnDealDamage: '造成伤害时',
  OnDestroy: '移除时',
  OnCustomEvent: '自定义事件',
  Basic: '弱驱散',
  Strong: '强驱散',
  MaxHp: '最大生命',
  HpRegen: '生命恢复',
  MaxMana: '最大魔法',
  ManaRegen: '魔法恢复',
  AttackDamage: '攻击力',
  AttackSpeed: '攻击速度',
  BaseAttackTime: '基础攻击间隔',
  AttackRange: '攻击距离',
  Armor: '护甲',
  MagicResistance: '魔法抗性',
  StatusResistance: '状态抗性',
  Evasion: '闪避',
  MoveSpeed: '移动速度',
  CastRange: '施法距离',
  CooldownReduction: '冷却缩减',
  SpellAmplification: '技能增强',
  CritChance: '暴击概率',
  CritDamage: '暴击伤害',
  Stun: '眩晕',
  Hex: '妖术',
  Taunt: '嘲讽',
  Fear: '恐惧',
  Silence: '沉默',
  Muted: '禁用物品',
  Disarm: '缴械',
  Blind: '致盲',
  Root: '缠绕',
  Leash: '束缚',
  Slow: '减速',
  Break: '破坏',
  Invulnerable: '无敌',
  DebuffImmune: '减益免疫',
  Untargetable: '无法选中',
  Ethereal: '虚无',
  Poisoned: '中毒',
};

export function getOptionLabel(value: string): string {
  return optionLabelMap[value] ?? value;
}

export type PrimaryAttributeType = (typeof primaryAttributeTypes)[number];
export type ModifierKind = (typeof modifierKinds)[number];
export type StackPolicy = (typeof stackPolicies)[number];
export type DispelRule = (typeof dispelRules)[number];
export type AttributeOp = (typeof attributeOps)[number];
export type DamageType = (typeof damageTypes)[number];
export type EffectActionType = (typeof effectActionTypes)[number];
export type TargetSelector = (typeof targetSelectors)[number];
export type EffectTrigger = (typeof effectTriggers)[number];

export interface AttributeModifier {
  id: string;
  attributeType: string;
  op: AttributeOp;
  value: number;
  scaleByStacks: boolean;
  priority: number;
}

export interface EffectCondition {
  healthPercentMin?: number;
  healthPercentMax?: number;
  requiredStatusEffect?: string;
}

export interface EffectAction {
  id: string;
  trigger: EffectTrigger;
  actionType: EffectActionType;
  targetSelector: TargetSelector;
  value: number;
  scaleByStacks: boolean;
  damageType?: DamageType;
  attributeType?: string;
  modifierTemplateId?: string;
  dispelType?: 'Basic' | 'Strong';
  eventName?: string;
  condition?: EffectCondition;
}

export interface BuffPayload {
  key: string;
  displayName: string;
  description: string;
  modifierKind: ModifierKind;
  duration: number;
  stackPolicy: StackPolicy;
  maxStacks: number;
  thinkInterval: number;
  statusEffects: string[];
  dispelRule: DispelRule;
  affectedByStatusResistance: boolean;
  removeOnDeath: boolean;
  isHidden: boolean;
  isPassive: boolean;
  isAura: boolean;
  auraRadius: number;
  lingerDuration: number;
  attributeModifiers: AttributeModifier[];
  effectActions: EffectAction[];
  tags: string[];
  applyChance: number;
  applyCooldown: number;
  requiredStatusEffects: string[];
}

export interface BuffTemplate extends BuffPayload {
  id: string;
  createdAt: string;
  updatedAt: string;
}

export interface AttributeFormulaConfig {
  primaryAttributeDefault: PrimaryAttributeType;
  strengthToMaxHp: number;
  strengthToHpRegen: number;
  agilityToArmor: number;
  agilityToAttackSpeed: number;
  intelligenceToMaxMana: number;
  intelligenceToManaRegen: number;
  intelligenceToMagicResistance: number;
  primaryAttributeToAttackDamage: number;
  universalAttributeToAttackDamage: number;
  minAttackSpeed: number;
  maxAttackSpeed: number;
  physicalArmorFactor: number;
}

export interface UnityExportPayload {
  schemaVersion: string;
  exportedAt: string;
  attributeFormula: AttributeFormulaConfig;
  buffs: Array<BuffPayload & { id: string }>;
}

export type GameplayTagSource = 'system' | 'web';

export interface GameplayTag {
  id: string;
  name: string;
  displayName: string;
  description: string;
  flags: number;
  source: GameplayTagSource;
  deprecated: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface GameplayTagsVersion {
  schemaVersion: string;
  version: number;
  publishedVersion: number;
  publishedAt: string | null;
  tagCount: number;
}

export interface GameplayTagsExportPayload {
  schemaVersion: string;
  version: number;
  exportedAt: string;
  tags: Array<{
    name: string;
    displayName: string;
    description: string;
    flags: number;
    source: GameplayTagSource;
    deprecated: boolean;
  }>;
}

export function createId(prefix: string): string {
  const value = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  return `${prefix}-${value}`;
}

export function createEmptyBuff(): BuffPayload {
  return {
    key: '',
    displayName: '',
    description: '',
    modifierKind: 'Buff',
    duration: 10,
    stackPolicy: 'Refresh',
    maxStacks: 1,
    thinkInterval: -1,
    statusEffects: [],
    dispelRule: 'BasicDispellable',
    affectedByStatusResistance: false,
    removeOnDeath: true,
    isHidden: false,
    isPassive: false,
    isAura: false,
    auraRadius: 0,
    lingerDuration: 0,
    attributeModifiers: [],
    effectActions: [],
    tags: [],
    applyChance: 1,
    applyCooldown: 0,
    requiredStatusEffects: [],
  };
}
