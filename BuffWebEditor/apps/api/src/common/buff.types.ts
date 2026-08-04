export const primaryAttributeTypes = [
  'None',
  'Strength',
  'Agility',
  'Intelligence',
  'Universal',
] as const;

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
  condition?: {
    healthPercentMin?: number;
    healthPercentMax?: number;
    requiredStatusEffect?: string;
  };
}

export interface BuffTemplate {
  id: string;
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
  schemaVersion: '2.0.0';
  exportedAt: string;
  attributeFormula: AttributeFormulaConfig;
  buffs: Array<Omit<BuffTemplate, 'createdAt' | 'updatedAt'>>;
}
