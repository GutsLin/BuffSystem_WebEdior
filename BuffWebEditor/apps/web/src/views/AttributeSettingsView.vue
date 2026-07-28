<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { showFailToast, showSuccessToast } from 'vant';
import EnumSelect from '@/components/EnumSelect.vue';
import PageHeader from '@/components/PageHeader.vue';
import { useSettingsStore } from '@/stores/settings';
import { primaryAttributeTypes, type AttributeFormulaConfig } from '@/types/buff';

const store = useSettingsStore();
const saving = ref(false);
const ready = ref(false);
const form = reactive<AttributeFormulaConfig>({
  primaryAttributeDefault: 'None',
  strengthToMaxHp: 22,
  strengthToHpRegen: 0.1,
  agilityToArmor: 1 / 6,
  agilityToAttackSpeed: 1,
  intelligenceToMaxMana: 12,
  intelligenceToManaRegen: 0.05,
  intelligenceToMagicResistance: 0.001,
  primaryAttributeToAttackDamage: 1,
  universalAttributeToAttackDamage: 0.45,
  minAttackSpeed: 20,
  maxAttackSpeed: 700,
  physicalArmorFactor: 0.06,
});

const groups = [
  {
    title: '力量换算',
    description: '决定力量对生存属性的贡献。',
    fields: [
      ['strengthToMaxHp', '每点力量 → 最大生命'],
      ['strengthToHpRegen', '每点力量 → 生命恢复'],
    ],
  },
  {
    title: '敏捷换算',
    description: '决定敏捷对护甲和攻击速度的贡献。',
    fields: [
      ['agilityToArmor', '每点敏捷 → 护甲'],
      ['agilityToAttackSpeed', '每点敏捷 → 攻击速度'],
    ],
  },
  {
    title: '智力换算',
    description: '魔抗使用小数表示，0.001 即 0.1%。',
    fields: [
      ['intelligenceToMaxMana', '每点智力 → 最大魔法'],
      ['intelligenceToManaRegen', '每点智力 → 魔法恢复'],
      ['intelligenceToMagicResistance', '每点智力 → 魔法抗性'],
    ],
  },
  {
    title: '攻击与护甲规则',
    description: '主属性、全才英雄、攻速边界与护甲公式参数。',
    fields: [
      ['primaryAttributeToAttackDamage', '每点主属性 → 攻击力'],
      ['universalAttributeToAttackDamage', '全才每点任意三维 → 攻击力'],
      ['minAttackSpeed', '最低攻击速度'],
      ['maxAttackSpeed', '最高攻击速度'],
      ['physicalArmorFactor', '物理护甲公式系数'],
    ],
  },
] as const;

onMounted(async () => {
  try {
    const config = await store.load();
    Object.assign(form, config);
    ready.value = true;
  } catch (error) {
    showFailToast((error as Error).message);
  }
});

async function save() {
  saving.value = true;
  try {
    await store.save({ ...form });
    showSuccessToast('三维公式已保存');
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <section class="page-container">
    <PageHeader eyebrow="属性公式" title="英雄三维公式" description="维护客户端与 Unity 共用的属性换算参数。">
      <template #actions>
        <van-button type="primary" icon="success" :loading="saving" @click="save">保存公式</van-button>
      </template>
    </PageHeader>

    <van-loading v-if="!ready" class="page-loading" vertical>读取公式...</van-loading>
    <template v-else>
      <div class="formula-hero-card">
        <div>
          <span class="eyebrow">默认配置</span>
          <h2>Dota 风格派生属性</h2>
          <p>所有数值都会写入 Unity 导出文件，实际平衡无需改动代码。</p>
        </div>
        <EnumSelect
          :model-value="form.primaryAttributeDefault"
          label="非英雄默认主属性"
          :options="primaryAttributeTypes"
          @update:model-value="form.primaryAttributeDefault = $event as AttributeFormulaConfig['primaryAttributeDefault']"
        />
      </div>

      <div class="formula-grid">
        <article v-for="group in groups" :key="group.title" class="formula-card">
          <h3>{{ group.title }}</h3>
          <p>{{ group.description }}</p>
          <van-field
            v-for="field in group.fields"
            :key="field[0]"
            v-model.number="form[field[0]]"
            type="number"
            :label="field[1]"
            input-align="right"
          />
        </article>
      </div>
    </template>
  </section>
</template>
