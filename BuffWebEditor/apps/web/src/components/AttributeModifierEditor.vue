<script setup lang="ts">
import { attributeOps, attributeTypeOptions, createId, type AttributeModifier } from '@/types/buff';
import EnumSelect from './EnumSelect.vue';

const props = defineProps<{ modelValue: AttributeModifier[] }>();
const emit = defineEmits<{ 'update:modelValue': [value: AttributeModifier[]] }>();

function addItem() {
  emit('update:modelValue', [
    ...props.modelValue,
    {
      id: createId('attribute'),
      attributeType: 'Strength',
      op: 'Add',
      value: 1,
      scaleByStacks: false,
      priority: 0,
    },
  ]);
}

function updateItem(index: number, patch: Partial<AttributeModifier>) {
  const next = props.modelValue.map((item, itemIndex) => (itemIndex === index ? { ...item, ...patch } : item));
  emit('update:modelValue', next);
}

function removeItem(index: number) {
  emit('update:modelValue', props.modelValue.filter((_, itemIndex) => itemIndex !== index));
}
</script>

<template>
  <div class="editor-list">
    <div v-if="modelValue.length === 0" class="empty-inline">暂无属性修改器</div>
    <article v-for="(item, index) in modelValue" :key="item.id" class="editor-list-card">
      <div class="editor-card-title">
        <strong>属性修改 #{{ index + 1 }}</strong>
        <van-button size="mini" plain type="danger" icon="delete-o" @click="removeItem(index)">删除</van-button>
      </div>
      <div class="form-grid form-grid-3">
        <EnumSelect
          :model-value="item.attributeType"
          label="属性"
          :options="attributeTypeOptions"
          @update:model-value="updateItem(index, { attributeType: $event })"
        />
        <EnumSelect
          :model-value="item.op"
          label="操作"
          :options="attributeOps"
          @update:model-value="updateItem(index, { op: $event as AttributeModifier['op'] })"
        />
        <van-field
          :model-value="String(item.value)"
          type="number"
          label="数值"
          input-align="right"
          @update:model-value="updateItem(index, { value: Number($event) })"
        />
        <van-field
          :model-value="String(item.priority)"
          type="digit"
          label="优先级"
          input-align="right"
          @update:model-value="updateItem(index, { priority: Number($event) })"
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
    <van-button block plain type="primary" icon="plus" @click="addItem">添加属性修改器</van-button>
  </div>
</template>
