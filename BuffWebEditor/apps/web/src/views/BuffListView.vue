<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { showFailToast, showSuccessToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { useBuffStore } from '@/stores/buffs';
import { getOptionLabel } from '@/types/buff';

const router = useRouter();
const store = useBuffStore();
const query = ref('');
const kind = ref('All');
const kinds = ['All', 'Buff', 'Debuff', 'Neutral'];

const filtered = computed(() => {
  const keyword = query.value.trim().toLowerCase();
  return store.items.filter((item) => {
    const matchesKind = kind.value === 'All' || item.modifierKind === kind.value;
    const matchesKeyword =
      !keyword ||
      item.key.toLowerCase().includes(keyword) ||
      item.displayName.toLowerCase().includes(keyword) ||
      item.tags.some((tag) => tag.toLowerCase().includes(keyword));
    return matchesKind && matchesKeyword;
  });
});

onMounted(async () => {
  try {
    await store.load();
  } catch (error) {
    showFailToast((error as Error).message);
  }
});

async function duplicate(id: string) {
  try {
    const copy = await store.duplicate(id);
    showSuccessToast('已创建副本');
    await router.push(`/buffs/${copy.id}`);
  } catch (error) {
    showFailToast((error as Error).message);
  }
}
</script>

<template>
  <section class="page-container">
    <PageHeader eyebrow="效果配置库" title="效果配置管理" description="管理增益、减益、被动与光环模板。">
      <template #actions>
        <van-button type="primary" icon="plus" @click="router.push('/buffs/new')">新建效果</van-button>
      </template>
    </PageHeader>

    <div class="toolbar-card">
      <van-search v-model="query" placeholder="搜索名称、标识或标签" shape="round" />
      <div class="filter-chips">
        <button v-for="item in kinds" :key="item" :class="{ active: kind === item }" @click="kind = item">
          {{ item === 'All' ? '全部' : getOptionLabel(item) }}
        </button>
      </div>
    </div>

    <van-skeleton v-if="store.loading" title :row="6" />

    <div v-else-if="filtered.length" class="buff-grid">
      <article v-for="buff in filtered" :key="buff.id" class="buff-card" @click="router.push(`/buffs/${buff.id}`)">
        <div class="buff-card-top">
          <span class="kind-badge" :class="buff.modifierKind.toLowerCase()">{{ getOptionLabel(buff.modifierKind) }}</span>
          <span v-if="buff.isAura" class="soft-badge">光环</span>
          <span v-if="buff.isPassive" class="soft-badge">被动</span>
          <button class="icon-action" title="创建副本" @click.stop="duplicate(buff.id)">
            <van-icon name="description" />
          </button>
        </div>
        <h3>{{ buff.displayName }}</h3>
        <code>{{ buff.key }}</code>
        <p>{{ buff.description || '暂无说明' }}</p>
        <div class="buff-metrics">
          <span><b>{{ buff.duration < 0 ? '永久' : `${buff.duration}秒` }}</b>持续</span>
          <span><b>{{ buff.maxStacks }}</b>层</span>
          <span><b>{{ buff.attributeModifiers.length }}</b>属性</span>
          <span><b>{{ buff.effectActions.length }}</b>动作</span>
        </div>
      </article>
    </div>

    <van-empty v-else description="没有符合条件的效果">
      <van-button type="primary" size="small" @click="router.push('/buffs/new')">创建第一个</van-button>
    </van-empty>
  </section>
</template>
