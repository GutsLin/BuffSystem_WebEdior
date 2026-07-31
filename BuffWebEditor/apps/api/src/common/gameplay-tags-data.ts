import type { GameplayTagRecord, GameplayTagsStorageData } from './gameplay-tags.types';

const now = new Date().toISOString();

const systemTags: Array<Omit<GameplayTagRecord, 'id' | 'createdAt' | 'updatedAt'>> = [
  { name: 'State', displayName: '状态', description: '角色当前状态', flags: 0, source: 'system', deprecated: false },
  { name: 'State.Buff', displayName: '增益状态', description: '对角色有正向影响的状态', flags: 0, source: 'system', deprecated: false },
  { name: 'State.Debuff', displayName: '减益状态', description: '对角色有负面影响的状态', flags: 0, source: 'system', deprecated: false },
  { name: 'State.Control', displayName: '控制状态', description: '影响角色行动能力的状态', flags: 0, source: 'system', deprecated: false },
  { name: 'Ability', displayName: '技能', description: '技能和施法相关标签', flags: 0, source: 'system', deprecated: false },
  { name: 'Ability.Attack', displayName: '攻击', description: '普通攻击相关标签', flags: 0, source: 'system', deprecated: false },
  { name: 'State.Buff.MoveSpeed', displayName: '移动速度增益', description: '提升角色移动速度', flags: 0, source: 'web', deprecated: false },
  { name: 'State.Debuff.Stun', displayName: '眩晕', description: '角色无法移动、攻击和施法', flags: 0, source: 'web', deprecated: false },
];

const legacyTags = [
  'aura', 'carry', 'caster', 'control', 'damage', 'debuff', 'defense', 'delayed', 'demo',
  'dispel', 'dot', 'ethereal', 'heal', 'hero', 'hot', 'immunity', 'instant', 'intelligence',
  'link', 'modifier', 'neutral', 'offense', 'passive', 'rune', 'slow', 'speed', 'stack',
].map((name) => ({
  name,
  displayName: name,
  description: '兼容现有 Buff 配置的旧标签。',
  flags: 0,
  source: 'web' as const,
  deprecated: false,
}));

export const defaultGameplayTags: GameplayTagsStorageData = {
  version: 1,
  publishedVersion: 1,
  publishedAt: now,
  tags: [...systemTags, ...legacyTags].map((tag, index) => ({
    ...tag,
    id: `builtin-gameplay-tag-${index + 1}`,
    createdAt: now,
    updatedAt: now,
  })),
};
