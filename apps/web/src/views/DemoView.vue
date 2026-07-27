<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { showFailToast, showToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { useBuffStore } from '@/stores/buffs';
import { useSettingsStore } from '@/stores/settings';
import type { AttributeModifier, BuffTemplate, PrimaryAttributeType } from '@/types/buff';

interface ActiveBuffInstance {
  instanceId: string;
  buffId: string;
  stacks: number;
  startedAt: number;
  expiresAt: number | null;
}

const buffs = useBuffStore();
const settings = useSettingsStore();
const activeInstances = ref<ActiveBuffInstance[]>([]);
const nowMs = ref(Date.now());
const heroType = ref<PrimaryAttributeType>('Strength');
const heroTypes: PrimaryAttributeType[] = ['Strength', 'Agility', 'Intelligence', 'Universal'];
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
};

const activeEntries = computed(() =>
  activeInstances.value
    .map((instance) => ({ instance, buff: buffs.items.find((buff) => buff.id === instance.buffId) }))
    .filter((entry): entry is { instance: ActiveBuffInstance; buff: BuffTemplate } => Boolean(entry.buff)),
);

const modifiers = computed(() =>
  activeEntries.value.flatMap(({ buff, instance }) =>
    buff.attributeModifiers.map((modifier) => ({
      ...modifier,
      value: modifier.value * (modifier.scaleByStacks ? instance.stacks : 1),
    })),
  ),
);

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
    ['力量', stats.value.Strength, 'STR'],
    ['敏捷', stats.value.Agility, 'AGI'],
    ['智力', stats.value.Intelligence, 'INT'],
    ['最大生命', stats.value.MaxHp, 'HP'],
    ['最大魔法', stats.value.MaxMana, 'MP'],
    ['攻击力', stats.value.AttackDamage, 'ATK'],
    ['护甲', stats.value.Armor, 'ARM'],
    ['攻击速度', stats.value.AttackSpeed, 'AS'],
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
  };
}

function refreshInstance(instance: ActiveBuffInstance, buff: BuffTemplate, timestamp: number) {
  instance.startedAt = timestamp;
  instance.expiresAt = buff.isPassive || buff.duration < 0 ? null : timestamp + Math.max(0, buff.duration) * 1000;
}

function addBuff(buff: BuffTemplate) {
  const timestamp = Date.now();
  nowMs.value = timestamp;
  const existing = activeInstances.value.find((instance) => instance.buffId === buff.id);

  if (buff.stackPolicy === 'Independent' || !existing) {
    activeInstances.value.push(createInstance(buff, timestamp));
    showToast(`${buff.displayName} 已添加`);
    return;
  }

  if (buff.stackPolicy === 'Stack') {
    existing.stacks = Math.min(buff.maxStacks, existing.stacks + 1);
    refreshInstance(existing, buff, timestamp);
    activeInstances.value = [...activeInstances.value];
    showToast(`${buff.displayName}：${existing.stacks}/${buff.maxStacks} 层`);
    return;
  }

  if (buff.stackPolicy === 'Replace') {
    activeInstances.value = [
      ...activeInstances.value.filter((instance) => instance.buffId !== buff.id),
      createInstance(buff, timestamp),
    ];
    showToast(`${buff.displayName} 已重新施加`);
    return;
  }

  refreshInstance(existing, buff, timestamp);
  activeInstances.value = [...activeInstances.value];
  showToast(`${buff.displayName} 持续时间已刷新`);
}

function removeInstance(instanceId: string) {
  activeInstances.value = activeInstances.value.filter((instance) => instance.instanceId !== instanceId);
}

function clearAll() {
  activeInstances.value = [];
}

function tick() {
  const timestamp = Date.now();
  nowMs.value = timestamp;
  const expired = activeInstances.value.filter(
    (instance) => instance.expiresAt !== null && instance.expiresAt <= timestamp,
  );
  if (!expired.length) return;

  const expiredIds = new Set(expired.map((instance) => instance.instanceId));
  activeInstances.value = activeInstances.value.filter((instance) => !expiredIds.has(instance.instanceId));
  const names = expired
    .map((instance) => buffs.items.find((buff) => buff.id === instance.buffId)?.displayName)
    .filter(Boolean);
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
  timer = setInterval(tick, 100);
  try {
    await Promise.all([buffs.load(), settings.load()]);
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
        <div class="stat-grid">
          <article v-for="card in statCards" :key="card[0]" class="stat-card">
            <span>{{ card[0] }}</span>
            <strong>{{ Number(card[1]).toFixed(card[2] === '%' ? 2 : 1) }}</strong>
            <small>{{ card[2] }}</small>
          </article>
        </div>
        <div class="active-effects">
          <div class="active-effects-header">
            <h3>生效中的 Buff</h3>
            <van-button v-if="activeEntries.length" size="mini" plain type="danger" @click="clearAll">全部移除</van-button>
          </div>
          <div v-if="activeEntries.length" class="active-effect-list">
            <article v-for="entry in activeEntries" :key="entry.instance.instanceId" class="active-effect-card">
              <div class="active-effect-main">
                <div>
                  <strong>{{ entry.buff.displayName }}</strong>
                  <small>{{ entry.buff.key }}</small>
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
                color="#6d5dfc"
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
