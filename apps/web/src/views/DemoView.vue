<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { showFailToast, showToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { useBuffStore } from '@/stores/buffs';
import { useSettingsStore } from '@/stores/settings';
import type { AttributeModifier, BuffTemplate, DamageType, EffectTrigger, PrimaryAttributeType } from '@/types/buff';

interface ActiveBuffInstance {
  instanceId: string;
  buffId: string;
  stacks: number;
  startedAt: number;
  expiresAt: number | null;
  nextThinkAt: number | null;
}

interface CombatLogItem {
  id: string;
  text: string;
  tone: 'damage' | 'heal' | 'status';
}

const buffs = useBuffStore();
const settings = useSettingsStore();
const activeInstances = ref<ActiveBuffInstance[]>([]);
const nowMs = ref(Date.now());
const currentHp = ref(0);
const combatLog = ref<CombatLogItem[]>([]);
const heroType = ref<PrimaryAttributeType>('Strength');
const heroTypes: PrimaryAttributeType[] = ['Strength', 'Agility', 'Intelligence', 'Universal'];
const destroyingInstanceIds = new Set<string>();
let timer: ReturnType<typeof setInterval> | undefined;

const baseHero = {
  strength: 20,
  agility: 15,
  intelligence: 18,
  baseMaxHp: 120,
  baseMaxMana: 75,
  baseAttackDamage: 28,
  baseArmor: 1,
  baseAttackSpeed: 100,
  baseMoveSpeed: 300,
};

const activeEntries = computed(() =>
  activeInstances.value
    .map((instance) => ({ instance, buff: buffs.items.find((buff) => buff.id === instance.buffId) }))
    .filter((entry): entry is { instance: ActiveBuffInstance; buff: BuffTemplate } => Boolean(entry.buff)),
);

const modifiers = computed(() =>
  activeEntries.value.flatMap(({ buff, instance }) =>
    [
      ...buff.attributeModifiers.map((modifier) => ({
        ...modifier,
        value: modifier.value * (modifier.scaleByStacks ? instance.stacks : 1),
      })),
      ...buff.effectActions
        .filter(
          (action) =>
            action.actionType === 'ModifyAttribute' &&
            Boolean(action.attributeType) &&
            ['OnCreated', 'OnRefresh', 'OnStackChanged'].includes(action.trigger),
        )
        .map<AttributeModifier>((action) => ({
          id: action.id,
          attributeType: action.attributeType ?? '',
          op: 'Add',
          value: action.value * (action.scaleByStacks ? instance.stacks : 1),
          scaleByStacks: false,
          priority: 0,
        })),
    ],
  ),
);

const activeStatusEffects = computed(() => [
  ...new Set(activeEntries.value.flatMap(({ buff }) => buff.statusEffects)),
]);

const combatStates = computed(() => {
  const statuses = new Set(activeStatusEffects.value);
  const alive = currentHp.value > 0;
  return [
    { label: '移动', enabled: alive && !['Stun', 'Root', 'Hex'].some((status) => statuses.has(status)) },
    { label: '攻击', enabled: alive && !['Stun', 'Disarm', 'Hex'].some((status) => statuses.has(status)) },
    { label: '施法', enabled: alive && !['Stun', 'Silence', 'Hex'].some((status) => statuses.has(status)) },
    { label: '使用物品', enabled: alive && !['Stun', 'Muted', 'Hex'].some((status) => statuses.has(status)) },
    { label: '被动技能', enabled: alive && !statuses.has('Break') },
  ];
});

const hpPercent = computed(() => {
  const maxHp = stats.value?.MaxHp ?? 0;
  return maxHp > 0 ? Math.max(0, Math.min(100, (currentHp.value / maxHp) * 100)) : 0;
});

function calculateAttribute(name: string, base: number, list: AttributeModifier[]) {
  const matching = list.filter((item) => item.attributeType === name);
  const add = matching.filter((item) => item.op === 'Add').reduce((sum, item) => sum + item.value, 0);
  const percentAdd = matching
    .filter((item) => item.op === 'PercentAdd')
    .reduce((sum, item) => sum + item.value, 0);
  let result = (base + add) * (1 + percentAdd / 100);

  for (const modifier of matching.filter((item) => item.op === 'PercentMultiply')) {
    result *= 1 + modifier.value / 100;
  }
  for (const modifier of matching
    .filter((item) => item.op === 'Override')
    .sort((a, b) => a.priority - b.priority)) {
    result = modifier.value;
  }
  for (const modifier of matching.filter((item) => item.op === 'Min')) {
    result = Math.max(result, modifier.value);
  }
  for (const modifier of matching.filter((item) => item.op === 'Max')) {
    result = Math.min(result, modifier.value);
  }
  return result;
}

const stats = computed(() => {
  const config = settings.config;
  if (!config) return null;
  const list = modifiers.value;
  const strength = calculateAttribute('Strength', baseHero.strength, list);
  const agility = calculateAttribute('Agility', baseHero.agility, list);
  const intelligence = calculateAttribute('Intelligence', baseHero.intelligence, list);
  const primaryValue =
    heroType.value === 'Strength'
      ? strength
      : heroType.value === 'Agility'
        ? agility
        : heroType.value === 'Intelligence'
          ? intelligence
          : 0;
  const primaryDamage =
    heroType.value === 'Universal'
      ? (strength + agility + intelligence) * config.universalAttributeToAttackDamage
      : primaryValue * config.primaryAttributeToAttackDamage;

  return {
    Strength: strength,
    Agility: agility,
    Intelligence: intelligence,
    MaxHp: calculateAttribute('MaxHp', baseHero.baseMaxHp + strength * config.strengthToMaxHp, list),
    HpRegen: calculateAttribute('HpRegen', strength * config.strengthToHpRegen, list),
    MaxMana: calculateAttribute('MaxMana', baseHero.baseMaxMana + intelligence * config.intelligenceToMaxMana, list),
    ManaRegen: calculateAttribute('ManaRegen', intelligence * config.intelligenceToManaRegen, list),
    Armor: calculateAttribute('Armor', baseHero.baseArmor + agility * config.agilityToArmor, list),
    AttackSpeed: calculateAttribute('AttackSpeed', baseHero.baseAttackSpeed + agility * config.agilityToAttackSpeed, list),
    AttackDamage: calculateAttribute('AttackDamage', baseHero.baseAttackDamage + primaryDamage, list),
    MoveSpeed: calculateAttribute('MoveSpeed', baseHero.baseMoveSpeed, list),
    MagicResistance: calculateAttribute(
      'MagicResistance',
      intelligence * config.intelligenceToMagicResistance,
      list,
    ),
  };
});

const statCards = computed(() => {
  if (!stats.value) return [];
  return [
    ['当前生命', currentHp.value, 'HP'],
    ['力量', stats.value.Strength, 'STR'],
    ['敏捷', stats.value.Agility, 'AGI'],
    ['智力', stats.value.Intelligence, 'INT'],
    ['最大生命', stats.value.MaxHp, 'HP'],
    ['最大魔法', stats.value.MaxMana, 'MP'],
    ['攻击力', stats.value.AttackDamage, 'ATK'],
    ['护甲', stats.value.Armor, 'ARM'],
    ['攻击速度', stats.value.AttackSpeed, 'AS'],
    ['移动速度', stats.value.MoveSpeed, 'MS'],
    ['生命恢复', stats.value.HpRegen, 'HP/s'],
    ['魔法恢复', stats.value.ManaRegen, 'MP/s'],
    ['魔法抗性', stats.value.MagicResistance * 100, '%'],
  ];
});

function createInstance(buff: BuffTemplate, timestamp: number): ActiveBuffInstance {
  const permanent = buff.isPassive || buff.duration < 0;
  return {
    instanceId: globalThis.crypto?.randomUUID?.() ?? `${buff.id}-${timestamp}-${Math.random()}`,
    buffId: buff.id,
    stacks: 1,
    startedAt: timestamp,
    expiresAt: permanent ? null : timestamp + Math.max(0, buff.duration) * 1000,
    nextThinkAt: buff.thinkInterval > 0 ? timestamp + buff.thinkInterval * 1000 : null,
  };
}

function refreshInstance(instance: ActiveBuffInstance, buff: BuffTemplate, timestamp: number) {
  instance.startedAt = timestamp;
  instance.expiresAt = buff.isPassive || buff.duration < 0 ? null : timestamp + Math.max(0, buff.duration) * 1000;
  instance.nextThinkAt = buff.thinkInterval > 0 ? timestamp + buff.thinkInterval * 1000 : null;
}

function addCombatLog(text: string, tone: CombatLogItem['tone']) {
  combatLog.value = [
    {
      id: globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`,
      text,
      tone,
    },
    ...combatLog.value,
  ].slice(0, 6);
}

function applyDamage(value: number, damageType: DamageType = 'Magical') {
  if (!stats.value || value <= 0) return;
  let multiplier = 1;
  if (damageType === 'Magical') {
    multiplier = 1 - Math.max(-1, Math.min(0.95, stats.value.MagicResistance));
  } else if (damageType === 'Physical') {
    const factor = settings.config?.physicalArmorFactor ?? 0.06;
    const armor = stats.value.Armor;
    const reduction = (factor * armor) / (1 + factor * Math.abs(armor));
    multiplier = 1 - reduction;
  }
  const actual = Math.max(0, value * multiplier);
  currentHp.value = Math.max(0, currentHp.value - actual);
  addCombatLog(`受到 ${actual.toFixed(1)} 点${damageType}伤害`, 'damage');
}

function applyHeal(value: number) {
  if (!stats.value || value <= 0) return;
  const before = currentHp.value;
  currentHp.value = Math.min(stats.value.MaxHp, currentHp.value + value);
  addCombatLog(`恢复 ${(currentHp.value - before).toFixed(1)} 点生命`, 'heal');
}

function findModifierTemplate(templateId?: string) {
  if (!templateId) return undefined;
  return buffs.items.find((buff) => buff.id === templateId || buff.key === templateId);
}

function executeActions(buff: BuffTemplate, trigger: EffectTrigger, instance: ActiveBuffInstance) {
  for (const action of buff.effectActions.filter((item) => item.trigger === trigger)) {
    const value = action.value * (action.scaleByStacks ? instance.stacks : 1);
    if (action.actionType === 'DealDamage') {
      applyDamage(value, action.damageType);
    } else if (action.actionType === 'Heal') {
      applyHeal(value);
    } else if (action.actionType === 'ApplyModifier') {
      const target = findModifierTemplate(action.modifierTemplateId);
      if (target) addBuff(target, true);
    } else if (action.actionType === 'RemoveModifier') {
      const target = findModifierTemplate(action.modifierTemplateId);
      if (target) removeBuff(target.id);
    } else if (action.actionType === 'Dispel') {
      dispelDebuffs(action.dispelType ?? 'Basic');
    }
  }
}

function destroyInstance(instance: ActiveBuffInstance) {
  if (destroyingInstanceIds.has(instance.instanceId)) return;
  destroyingInstanceIds.add(instance.instanceId);
  try {
    const buff = buffs.items.find((item) => item.id === instance.buffId);
    if (buff) executeActions(buff, 'OnDestroy', instance);
    activeInstances.value = activeInstances.value.filter((item) => item.instanceId !== instance.instanceId);
  } finally {
    destroyingInstanceIds.delete(instance.instanceId);
  }
}

function removeBuff(buffId: string) {
  for (const instance of [...activeInstances.value].filter((item) => item.buffId === buffId)) {
    destroyInstance(instance);
  }
}

function dispelDebuffs(type: 'Basic' | 'Strong') {
  const removable = activeEntries.value.filter(({ buff }) => {
    if (buff.modifierKind !== 'Debuff' || buff.dispelRule === 'NotDispellable') return false;
    return type === 'Strong' || buff.dispelRule === 'BasicDispellable';
  });
  for (const { instance } of removable) destroyInstance(instance);
  if (removable.length) addCombatLog(`驱散了 ${removable.length} 个 Debuff`, 'status');
}

function addBuff(buff: BuffTemplate, silent = false) {
  const timestamp = Date.now();
  nowMs.value = timestamp;
  const existing = activeInstances.value.find((instance) => instance.buffId === buff.id);

  if (buff.stackPolicy === 'Independent' || !existing) {
    const instance = createInstance(buff, timestamp);
    activeInstances.value.push(instance);
    executeActions(buff, 'OnCreated', instance);
    if (buff.statusEffects.length) addCombatLog(`获得状态：${buff.statusEffects.join('、')}`, 'status');
    if (!silent) showToast(`${buff.displayName} 已添加`);
    return;
  }

  if (buff.stackPolicy === 'Stack') {
    existing.stacks = Math.min(buff.maxStacks, existing.stacks + 1);
    refreshInstance(existing, buff, timestamp);
    executeActions(buff, 'OnStackChanged', existing);
    activeInstances.value = [...activeInstances.value];
    if (!silent) showToast(`${buff.displayName}：${existing.stacks}/${buff.maxStacks} 层`);
    return;
  }

  if (buff.stackPolicy === 'Replace') {
    removeBuff(buff.id);
    const instance = createInstance(buff, timestamp);
    activeInstances.value.push(instance);
    executeActions(buff, 'OnCreated', instance);
    if (!silent) showToast(`${buff.displayName} 已重新施加`);
    return;
  }

  refreshInstance(existing, buff, timestamp);
  executeActions(buff, 'OnRefresh', existing);
  activeInstances.value = [...activeInstances.value];
  if (!silent) showToast(`${buff.displayName} 持续时间已刷新`);
}

function removeInstance(instanceId: string) {
  const instance = activeInstances.value.find((item) => item.instanceId === instanceId);
  if (instance) destroyInstance(instance);
}

function clearAll() {
  for (const instance of [...activeInstances.value]) destroyInstance(instance);
  activeInstances.value = [];
}

function resetHero() {
  currentHp.value = stats.value?.MaxHp ?? 0;
  combatLog.value = [];
}

function tick() {
  const timestamp = Date.now();
  nowMs.value = timestamp;
  if (stats.value && currentHp.value > stats.value.MaxHp) currentHp.value = stats.value.MaxHp;

  for (const instance of [...activeInstances.value]) {
    const buff = buffs.items.find((item) => item.id === instance.buffId);
    if (!buff || !instance.nextThinkAt || buff.thinkInterval <= 0) continue;
    let safety = 0;
    while (
      timestamp >= instance.nextThinkAt &&
      (instance.expiresAt === null || instance.nextThinkAt <= instance.expiresAt) &&
      safety < 10
    ) {
      executeActions(buff, 'OnIntervalThink', instance);
      instance.nextThinkAt += buff.thinkInterval * 1000;
      safety += 1;
    }
  }

  const expired = activeInstances.value.filter(
    (instance) => instance.expiresAt !== null && instance.expiresAt <= timestamp,
  );
  if (!expired.length) return;

  const names = expired
    .map((instance) => buffs.items.find((buff) => buff.id === instance.buffId)?.displayName)
    .filter(Boolean);
  for (const instance of expired) destroyInstance(instance);
  if (names.length) showToast(`${names.join('、')} 已结束`);
}

function durationLabel(buff: BuffTemplate) {
  if (buff.isPassive || buff.duration < 0) return '永久';
  return `${buff.duration} 秒`;
}

function remainingLabel(instance: ActiveBuffInstance) {
  if (instance.expiresAt === null) return '永久';
  return `${Math.max(0, (instance.expiresAt - nowMs.value) / 1000).toFixed(1)} 秒`;
}

function remainingPercent(entry: { instance: ActiveBuffInstance; buff: BuffTemplate }) {
  if (entry.instance.expiresAt === null || entry.buff.duration <= 0) return 100;
  return Math.max(0, Math.min(100, ((entry.instance.expiresAt - nowMs.value) / (entry.buff.duration * 1000)) * 100));
}

onMounted(async () => {
  try {
    await Promise.all([buffs.load(), settings.load()]);
    currentHp.value = stats.value?.MaxHp ?? 0;
    timer = setInterval(tick, 100);
  } catch (error) {
    showFailToast((error as Error).message);
  }
});

onBeforeUnmount(() => {
  if (timer) clearInterval(timer);
});
</script>

<template>
  <section class="page-container demo-page">
    <PageHeader eyebrow="Interactive Demo" title="英雄属性演示" description="添加 Buff 并观察持续时间、叠层规则和属性变化；倒计时结束后 Buff 会自动移除。">
      <template #actions>
        <van-button plain type="primary" icon="cluster-o" to="/buffs">打开编辑器</van-button>
      </template>
    </PageHeader>

    <div class="demo-layout">
      <aside class="demo-controls">
        <div class="hero-portrait">
          <div class="portrait-orb">{{ heroType.slice(0, 1) }}</div>
          <div><span class="eyebrow">Level 1</span><h2>训练场英雄</h2></div>
        </div>

        <label class="control-label">英雄主属性</label>
        <div class="hero-type-grid">
          <button v-for="type in heroTypes" :key="type" :class="{ active: heroType === type }" @click="heroType = type">
            {{ type }}
          </button>
        </div>

        <label class="control-label">添加 Buff</label>
        <div class="demo-buff-list">
          <article v-for="buff in buffs.items" :key="buff.id" class="demo-buff-option">
            <div>
              <b>{{ buff.displayName }}</b>
              <small>{{ buff.key }} · {{ durationLabel(buff) }}</small>
            </div>
            <van-button size="mini" type="primary" icon="plus" @click="addBuff(buff)">添加</van-button>
          </article>
        </div>
      </aside>

      <div class="demo-stage">
        <div class="stage-header">
          <div><span class="eyebrow">Runtime Simulation</span><h2>{{ heroType }} Hero</h2></div>
          <span class="active-count">{{ activeEntries.length }} 个 Buff 生效中</span>
        </div>
        <div class="hero-vitals">
          <div>
            <span>当前生命</span>
            <strong>{{ currentHp.toFixed(1) }} / {{ (stats?.MaxHp ?? 0).toFixed(1) }}</strong>
          </div>
          <van-progress
            :percentage="hpPercent"
            :show-pivot="false"
            stroke-width="9"
            color="#e05461"
            track-color="#f4dde0"
          />
          <van-button size="small" plain type="primary" @click="resetHero">恢复英雄</van-button>
        </div>
        <div class="stat-grid">
          <article v-for="card in statCards" :key="card[0]" class="stat-card">
            <span>{{ card[0] }}</span>
            <strong>{{ Number(card[1]).toFixed(card[2] === '%' ? 2 : 1) }}</strong>
            <small>{{ card[2] }}</small>
          </article>
        </div>
        <div class="combat-status-panel">
          <div class="combat-status-block">
            <span class="control-label">战斗状态</span>
            <div class="combat-state-grid">
              <span
                v-for="state in combatStates"
                :key="state.label"
                :class="['combat-state-chip', { disabled: !state.enabled }]"
              >
                {{ state.label }} · {{ state.enabled ? '可用' : '禁用' }}
              </span>
            </div>
          </div>
          <div class="combat-status-block">
            <span class="control-label">状态效果</span>
            <div v-if="activeStatusEffects.length" class="status-effect-cloud">
              <span v-for="status in activeStatusEffects" :key="status">{{ status }}</span>
            </div>
            <p v-else>当前没有控制或异常状态。</p>
          </div>
          <div class="combat-status-block combat-log-block">
            <span class="control-label">战斗记录</span>
            <div v-if="combatLog.length" class="combat-log-list">
              <span v-for="item in combatLog" :key="item.id" :class="item.tone">{{ item.text }}</span>
            </div>
            <p v-else>持续伤害、治疗和驱散会记录在这里。</p>
          </div>
        </div>
        <div class="active-effects">
          <div class="active-effects-header">
            <h3>生效中的 Buff</h3>
            <van-button v-if="activeEntries.length" size="mini" plain type="danger" @click="clearAll">全部移除</van-button>
          </div>
          <div v-if="activeEntries.length" class="active-effect-list">
            <article
              v-for="entry in activeEntries"
              :key="entry.instance.instanceId"
              :class="['active-effect-card', { 'is-debuff': entry.buff.modifierKind === 'Debuff' }]"
            >
              <div class="active-effect-main">
                <div>
                  <strong>{{ entry.buff.displayName }}</strong>
                  <small>{{ entry.buff.modifierKind }} · {{ entry.buff.key }}</small>
                </div>
                <div class="active-effect-meta">
                  <span v-if="entry.instance.stacks > 1">{{ entry.instance.stacks }} 层</span>
                  <b>{{ remainingLabel(entry.instance) }}</b>
                  <van-button size="mini" plain type="danger" icon="cross" @click="removeInstance(entry.instance.instanceId)" />
                </div>
              </div>
              <van-progress
                :percentage="remainingPercent(entry)"
                :show-pivot="false"
                stroke-width="5"
                :color="entry.buff.modifierKind === 'Debuff' ? '#df5b68' : '#6d5dfc'"
                track-color="#e8e6ff"
              />
            </article>
          </div>
          <p v-else>从左侧添加 Buff 后，这里会显示运行时倒计时和属性变化。</p>
        </div>
      </div>
    </div>
  </section>
</template>
