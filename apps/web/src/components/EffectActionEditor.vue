<script setup lang="ts">
import {
  attributeTypeOptions,
  createId,
  damageTypes,
  effectActionTypes,
  effectTriggers,
  targetSelectors,
  type EffectAction,
} from '@/types/buff';
import EnumSelect from './EnumSelect.vue';

const props = defineProps<{ modelValue: EffectAction[] }>();
const emit = defineEmits<{ 'update:modelValue': [value: EffectAction[]] }>();

function addItem() {
  emit('update:modelValue', [
    ...props.modelValue,
    {
      id: createId('effect'),
      trigger: 'OnIntervalThink',
      actionType: 'DealDamage',
      targetSelector: 'Target',
      value: 10,
      scaleByStacks: false,
      damageType: 'Magical',
    },
  ]);
}

function updateItem(index: number, patch: Partial<EffectAction>) {
  emit(
    'update:modelValue',
    props.modelValue.map((item, itemIndex) => (itemIndex === index ? { ...item, ...patch } : item)),
  );
}

function removeItem(index: number) {
  emit('update:modelValue', props.modelValue.filter((_, itemIndex) => itemIndex !== index));
}
</script>

<template>
  <div class="editor-list">
    <div v-if="modelValue.length === 0" class="empty-inline">暂无效果动作</div>
    <article v-for="(item, index) in modelValue" :key="item.id" class="editor-list-card">
      <div class="editor-card-title">
        <strong>效果动作 #{{ index + 1 }}</strong>
        <van-button size="mini" plain type="danger" icon="delete-o" @click="removeItem(index)">删除</van-button>
      </div>
      <div class="form-grid form-grid-3">
        <EnumSelect
          :model-value="item.trigger"
          label="触发时机"
          :options="effectTriggers"
          @update:model-value="updateItem(index, { trigger: $event as EffectAction['trigger'] })"
        />
        <EnumSelect
          :model-value="item.actionType"
          label="动作"
          :options="effectActionTypes"
          @update:model-value="updateItem(index, { actionType: $event as EffectAction['actionType'] })"
        />
        <EnumSelect
          :model-value="item.targetSelector"
          label="目标"
          :options="targetSelectors"
          @update:model-value="updateItem(index, { targetSelector: $event as EffectAction['targetSelector'] })"
        />
        <van-field
          :model-value="String(item.value)"
          type="number"
          label="基础数值"
          input-align="right"
          @update:model-value="updateItem(index, { value: Number($event) })"
        />
        <EnumSelect
          v-if="item.actionType === 'DealDamage'"
          :model-value="item.damageType ?? 'Magical'"
          label="伤害类型"
          :options="damageTypes"
          @update:model-value="updateItem(index, { damageType: $event as EffectAction['damageType'] })"
        />
        <EnumSelect
          v-if="item.actionType === 'ModifyAttribute'"
          :model-value="item.attributeType ?? 'Strength'"
          label="目标属性"
          :options="attributeTypeOptions"
          @update:model-value="updateItem(index, { attributeType: $event })"
        />
        <van-field
          v-if="item.actionType === 'ApplyModifier' || item.actionType === 'RemoveModifier'"
          :model-value="item.modifierTemplateId ?? ''"
          label="Modifier Key"
          placeholder="例如 StunDebuff"
          @update:model-value="updateItem(index, { modifierTemplateId: $event })"
        />
        <EnumSelect
          v-if="item.actionType === 'Dispel'"
          :model-value="item.dispelType ?? 'Basic'"
          label="驱散强度"
          :options="['Basic', 'Strong']"
          @update:model-value="updateItem(index, { dispelType: $event as EffectAction['dispelType'] })"
        />
        <van-cell title="按层数缩放">
          <template #right-icon>
            <van-switch
              :model-value="item.scaleByStacks"
              size="20"
              @update:model-value="updateItem(index, { scaleByStacks: $event })"
            />
          </template>
        </van-cell>
      </div>
    </article>
    <van-button block plain type="primary" icon="plus" @click="addItem">添加效果动作</van-button>
  </div>
</template>
