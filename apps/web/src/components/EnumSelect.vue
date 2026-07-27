<script setup lang="ts">
import { getOptionLabel } from '@/types/buff';

defineProps<{
  modelValue: string;
  label: string;
  options: readonly string[];
  hint?: string;
}>();

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
</script>

<template>
  <label class="select-control">
    <span>{{ label }}</span>
    <select :value="modelValue" @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)">
      <option v-for="option in options" :key="option" :value="option">{{ getOptionLabel(option) }}</option>
    </select>
    <small v-if="hint">{{ hint }}</small>
  </label>
</template>
