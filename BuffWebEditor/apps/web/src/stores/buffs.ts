import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { api } from '@/services/api';
import type { BuffPayload, BuffTemplate } from '@/types/buff';

export const useBuffStore = defineStore('buffs', () => {
  const items = ref<BuffTemplate[]>([]);
  const loading = ref(false);
  const loaded = ref(false);

  const buffCount = computed(() => items.value.length);
  const buffKinds = computed(() => ({
    buffs: items.value.filter((item) => item.modifierKind === 'Buff').length,
    debuffs: items.value.filter((item) => item.modifierKind === 'Debuff').length,
    auras: items.value.filter((item) => item.isAura).length,
  }));

  async function load(force = false) {
    if (loaded.value && !force) return;
    loading.value = true;
    try {
      items.value = await api.listBuffs();
      loaded.value = true;
    } finally {
      loading.value = false;
    }
  }

  async function getById(id: string) {
    const existing = items.value.find((item) => item.id === id);
    return existing ?? api.getBuff(id);
  }

  async function save(payload: BuffPayload, id?: string) {
    const saved = id ? await api.updateBuff(id, payload) : await api.createBuff(payload);
    const index = items.value.findIndex((item) => item.id === saved.id);
    if (index >= 0) items.value[index] = saved;
    else items.value.unshift(saved);
    return saved;
  }

  async function remove(id: string) {
    await api.deleteBuff(id);
    items.value = items.value.filter((item) => item.id !== id);
  }

  async function duplicate(id: string) {
    const copy = await api.duplicateBuff(id);
    items.value.unshift(copy);
    return copy;
  }

  return { items, loading, loaded, buffCount, buffKinds, load, getById, save, remove, duplicate };
});
