<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { showFailToast, showSuccessToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { api } from '@/services/api';
import type { GameplayTag, GameplayTagsExportPayload } from '@/types/buff';

type TagDraft = Omit<GameplayTag, 'id' | 'createdAt' | 'updatedAt'>;

const tags = ref<GameplayTag[]>([]);
const version = ref<{ version: number; publishedVersion: number; tagCount: number } | null>(null);
const exportPayload = ref<GameplayTagsExportPayload | null>(null);
const query = ref('');
const loading = ref(false);
const saving = ref(false);
const publishing = ref(false);
const selectedId = ref<string | null>(null);
const draft = reactive<TagDraft>(createDraft());

function createDraft(): TagDraft {
  return {
    name: '',
    displayName: '',
    description: '',
    flags: 0,
    source: 'web',
    deprecated: false,
  };
}

const filteredTags = computed(() => {
  const keyword = query.value.trim().toLowerCase();
  return tags.value.filter((tag) => {
    if (!keyword) return true;
    return [tag.name, tag.displayName, tag.description].some((value) => value.toLowerCase().includes(keyword));
  });
});

const sortedTags = computed(() =>
  [...filteredTags.value].sort((left, right) => left.name.localeCompare(right.name)),
);

const json = computed(() => (exportPayload.value ? JSON.stringify(exportPayload.value, null, 2) : ''));

onMounted(refresh);

async function refresh() {
  loading.value = true;
  try {
    const [tagList, tagVersion, exported] = await Promise.all([
      api.listGameplayTags(),
      api.getGameplayTagsVersion(),
      api.getGameplayTagsExport(),
    ]);
    tags.value = tagList;
    version.value = tagVersion;
    exportPayload.value = exported;
    if (!selectedId.value) resetDraft();
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    loading.value = false;
  }
}

function resetDraft() {
  selectedId.value = null;
  Object.assign(draft, createDraft());
}

function edit(tag: GameplayTag) {
  selectedId.value = tag.id;
  Object.assign(draft, {
    name: tag.name,
    displayName: tag.displayName,
    description: tag.description,
    flags: tag.flags,
    source: tag.source,
    deprecated: tag.deprecated,
  });
}

async function save() {
  if (!draft.name.trim() || !draft.displayName.trim()) {
    showFailToast('请填写标签名称和显示名称');
    return;
  }
  saving.value = true;
  try {
    const saved = selectedId.value
      ? await api.updateGameplayTag(selectedId.value, { ...draft })
      : await api.createGameplayTag({ ...draft });
    const index = tags.value.findIndex((tag) => tag.id === saved.id);
    if (index >= 0) tags.value[index] = saved;
    else tags.value.push(saved);
    showSuccessToast('标签已保存');
    resetDraft();
    await refreshExport();
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    saving.value = false;
  }
}

async function refreshExport() {
  const [tagVersion, exported] = await Promise.all([api.getGameplayTagsVersion(), api.getGameplayTagsExport()]);
  version.value = tagVersion;
  exportPayload.value = exported;
}

async function publish() {
  publishing.value = true;
  try {
    exportPayload.value = await api.publishGameplayTags();
    await refreshExport();
    showSuccessToast(`已发布标签版本 ${version.value?.publishedVersion ?? exportPayload.value.version}`);
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    publishing.value = false;
  }
}

async function copy() {
  try {
    await navigator.clipboard.writeText(json.value);
    showSuccessToast('JSON 已复制');
  } catch {
    showFailToast('浏览器未允许复制，请手动选择文本');
  }
}

function download() {
  window.location.assign('/api/gameplay-tags/export');
}
</script>

<template>
  <section class="page-container gameplay-tags-page">
    <PageHeader
      eyebrow="GameplayTags 配置"
      title="标签管理"
      description="维护 Web 业务标签，发布后由 Unity 在启动阶段加载；标签名称发布后保持稳定。"
    >
      <template #actions>
        <van-button plain icon="replay" :loading="loading" @click="refresh">刷新</van-button>
        <van-button plain icon="plus" @click="resetDraft">新建标签</van-button>
        <van-button plain icon="down" @click="download">下载 JSON</van-button>
        <van-button type="primary" icon="upgrade" :loading="publishing" @click="publish">发布标签</van-button>
      </template>
    </PageHeader>

    <div class="gameplay-tags-layout">
      <div class="tag-editor-column">
        <div class="editor-panel tag-editor-card">
          <div class="section-intro">
            <h2>{{ selectedId ? '编辑标签' : '新建标签' }}</h2>
            <p>使用点号表达层级，例如 <code>State.Buff.MoveSpeed</code>。缺失父级会自动补齐。</p>
          </div>
          <van-field v-model="draft.name" label="完整名称" placeholder="State.Buff.MoveSpeed" :disabled="Boolean(selectedId)" />
          <van-field v-model="draft.displayName" label="显示名称" placeholder="移动速度增益" />
          <van-field v-model="draft.description" rows="3" autosize type="textarea" label="描述" placeholder="标签用途说明" />
          <div class="form-grid form-grid-2 tag-form-grid">
            <van-field v-model.number="draft.flags" type="digit" label="Flags" input-align="right" />
            <van-cell title="废弃标签">
              <template #right-icon><van-switch v-model="draft.deprecated" size="20" /></template>
            </van-cell>
          </div>
          <van-button block type="primary" :loading="saving" @click="save">保存标签</van-button>
        </div>

        <div class="json-preview tag-json-preview">
          <div class="code-toolbar">
            <span>gameplay-tags.json</span>
            <van-button size="small" plain @click="copy">复制</van-button>
          </div>
          <pre>{{ json || '正在生成预览...' }}</pre>
        </div>
      </div>

      <div class="tag-list-column">
        <div class="toolbar-card tag-toolbar">
          <van-search v-model="query" placeholder="搜索标签名称、显示名或描述" shape="round" />
          <span class="tag-count">{{ sortedTags.length }} / {{ tags.length }}</span>
        </div>

        <div class="tag-summary-card">
          <div><span>当前版本</span><strong>{{ version?.version ?? '-' }}</strong></div>
          <div><span>已发布</span><strong>{{ version?.publishedVersion ?? '-' }}</strong></div>
          <div><span>有效标签</span><strong>{{ version?.tagCount ?? 0 }}</strong></div>
        </div>

        <div v-if="loading" class="page-loading"><van-loading vertical>加载标签...</van-loading></div>
        <div v-else class="tag-tree-card">
          <button v-for="tag in sortedTags" :key="tag.id" class="tag-tree-row" :class="{ selected: tag.id === selectedId, deprecated: tag.deprecated }" @click="edit(tag)">
            <span class="tag-tree-indent" :style="{ width: `${Math.max(0, tag.name.split('.').length - 1) * 18}px` }" />
            <span class="tag-tree-content">
              <strong>{{ tag.name }}</strong>
              <small>{{ tag.displayName }} · {{ tag.source }}</small>
            </span>
            <van-tag v-if="tag.deprecated" type="warning" plain>废弃</van-tag>
          </button>
          <div v-if="!sortedTags.length" class="empty-inline">没有匹配的标签</div>
        </div>
      </div>
    </div>
  </section>
</template>
