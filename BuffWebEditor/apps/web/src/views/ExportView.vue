<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { showFailToast, showSuccessToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { api } from '@/services/api';
import type { UnityExportPayload } from '@/types/buff';

const payload = ref<UnityExportPayload | null>(null);
const loading = ref(false);
const json = computed(() => (payload.value ? JSON.stringify(payload.value, null, 2) : ''));

async function refresh() {
  loading.value = true;
  try {
    payload.value = await api.getUnityPreview();
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    loading.value = false;
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
  window.location.assign('/api/export/unity');
}

onMounted(refresh);
</script>

<template>
  <section class="page-container">
    <PageHeader eyebrow="Unity 数据导出" title="导出游戏数据" description="导出稳定、可版本化的 JSON，供 Unity 端反序列化使用。">
      <template #actions>
        <van-button plain icon="replay" :loading="loading" @click="refresh">刷新预览</van-button>
        <van-button type="primary" icon="down" @click="download">下载 JSON</van-button>
      </template>
    </PageHeader>

    <div class="export-layout">
      <aside class="export-summary">
        <span class="eyebrow">导出摘要</span>
        <h2>{{ payload?.buffs.length ?? 0 }} 个效果</h2>
        <div class="summary-row"><span>数据版本</span><b>{{ payload?.schemaVersion ?? '-' }}</b></div>
        <div class="summary-row"><span>导出时间</span><b>{{ payload ? new Date(payload.exportedAt).toLocaleString() : '-' }}</b></div>
        <div class="summary-row"><span>全才系数</span><b>{{ payload?.attributeFormula.universalAttributeToAttackDamage ?? '-' }}</b></div>
        <van-button block plain type="primary" icon="records-o" @click="copy">复制 JSON</van-button>
        <p class="help-text">Unity 端建议按 <code>schemaVersion</code> 选择对应 DTO，枚举按字符串读取。</p>
      </aside>
      <div class="json-preview">
        <div class="code-toolbar"><span>buff-system.json</span><span>{{ json.length.toLocaleString() }} 字符</span></div>
        <pre>{{ json || '正在生成预览...' }}</pre>
      </div>
    </div>
  </section>
</template>
