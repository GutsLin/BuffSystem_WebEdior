import { defineStore } from 'pinia';
import { ref } from 'vue';
import { api } from '@/services/api';
import type { AttributeFormulaConfig } from '@/types/buff';

export const useSettingsStore = defineStore('settings', () => {
  const config = ref<AttributeFormulaConfig | null>(null);
  const loading = ref(false);

  async function load(force = false) {
    if (config.value && !force) return config.value;
    loading.value = true;
    try {
      config.value = await api.getAttributeFormula();
      return config.value;
    } finally {
      loading.value = false;
    }
  }

  async function save(value: AttributeFormulaConfig) {
    config.value = await api.updateAttributeFormula(value);
    return config.value;
  }

  return { config, loading, load, save };
});
