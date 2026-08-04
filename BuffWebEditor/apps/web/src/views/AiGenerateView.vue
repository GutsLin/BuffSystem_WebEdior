<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { showFailToast, showSuccessToast, showToast } from 'vant';
import PageHeader from '@/components/PageHeader.vue';
import { useBuffStore } from '@/stores/buffs';
import { api } from '@/services/api';
import { getOptionLabel, type BuffPayload, type BuffTemplate } from '@/types/buff';

const router = useRouter();
const store = useBuffStore();
const prompt = ref('');
const generating = ref(false);
const saving = ref(false);
const generated = ref<BuffPayload | null>(null);
const savedId = ref<string | null>(null);
const error = ref('');

const hasResult = computed(() => generated.value !== null);

const quickTemplates = [
  {
    label: '持续伤害',
    icon: 'fire-o',
    prompt: '每秒造成30点魔法伤害，持续8秒的减益效果，最多叠加5层',
  },
  {
    label: '治疗光环',
    icon: 'award-o',
    prompt: '被动光环，900范围内友军每秒恢复15点生命，光环提供者离开范围后残留2秒',
  },
  {
    label: '硬控减益',
    icon: 'warning-o',
    prompt: '眩晕目标2.5秒并造成50点魔法伤害。可被强驱散移除，受状态抗性影响',
  },
  {
    label: '叠层增益',
    icon: 'upgrade',
    prompt: '每次攻击命中提升3点攻击力和5点攻击速度，最多叠加10层，持续6秒，刷新策略',
  },
  {
    label: '驱散净化',
    icon: 'clear',
    prompt: '创建时立即治疗自身10点生命并执行强驱散，移除所有debuff',
  },
  {
    label: '属性转换',
    icon: 'exchange',
    prompt: '提升15点力量并额外增加3点生命恢复，持续20秒的增益效果',
  },
  {
    label: '暴击爆发',
    icon: 'gem-o',
    prompt: '提升30%暴击概率和50%暴击伤害，持续5秒',
  },
  {
    label: '虚无状态',
    icon: 'eye-o',
    prompt: '使目标进入虚无状态3秒，期间缴械并降低30点魔法抗性，自身获得50%魔法伤害加成',
  },
];

function fillTemplate(tmpl: (typeof quickTemplates)[number]) {
  prompt.value = tmpl.prompt;
}

async function doGenerate() {
  if (!prompt.value.trim()) {
    showFailToast('请输入效果描述');
    return;
  }

  generating.value = true;
  error.value = '';
  generated.value = null;
  savedId.value = null;

  try {
    const result = await api.generateBuff({ prompt: prompt.value.trim() });
    generated.value = result;
    showToast('生成成功');
  } catch (err) {
    error.value = (err as Error).message;
    showFailToast(error.value);
  } finally {
    generating.value = false;
  }
}

async function saveGenerated() {
  if (!generated.value) return;
  saving.value = true;
  try {
    const saved = await store.save(generated.value);
    savedId.value = saved.id;
    showSuccessToast('已保存到效果列表');
  } catch (err) {
    showFailToast((err as Error).message);
  } finally {
    saving.value = false;
  }
}

function editGenerated() {
  if (savedId.value) {
    router.push(`/buffs/${savedId.value}`);
  }
}

function formatStackPolicy(buff: BuffPayload): string {
  return getOptionLabel(buff.stackPolicy);
}

function attributeSummary(buff: BuffPayload): string {
  if (!buff.attributeModifiers.length) return '无';
  return buff.attributeModifiers
    .slice(0, 3)
    .map((m) => `${getOptionLabel(m.attributeType)} ${m.op === 'Add' ? '+' : m.op}${m.value}`)
    .join(' / ')
    + (buff.attributeModifiers.length > 3 ? ` 等${buff.attributeModifiers.length}项` : '');
}

function actionsSummary(buff: BuffPayload): string {
  if (!buff.effectActions.length) return '无';
  return buff.effectActions
    .slice(0, 3)
    .map((a) => `${getOptionLabel(a.trigger)} → ${getOptionLabel(a.actionType)}`)
    .join(' / ')
    + (buff.effectActions.length > 3 ? ` 等${buff.effectActions.length}项` : '');
}
</script>

<template>
  <section class="page-container ai-page">
    <PageHeader
      eyebrow="AI 生成"
      title="智能生成 Buff"
      description="用自然语言描述效果，AI 自动生成完整配置。支持持续伤害、光环、控制、叠层等多种效果类型。"
    >
      <template #actions>
        <van-button plain type="primary" icon="cluster-o" to="/buffs">效果列表</van-button>
      </template>
    </PageHeader>

    <div class="ai-layout">
      <aside class="ai-input-panel">
        <label class="control-label">快捷模板</label>
        <div class="template-grid">
          <button
            v-for="tmpl in quickTemplates"
            :key="tmpl.label"
            class="template-chip"
            :class="{ active: prompt === tmpl.prompt }"
            @click="fillTemplate(tmpl)"
          >
            <van-icon :name="tmpl.icon" />
            <span>{{ tmpl.label }}</span>
          </button>
        </div>

        <label class="control-label">效果描述</label>
        <div class="prompt-input-wrap">
          <textarea
            v-model="prompt"
            class="prompt-textarea"
            placeholder="用自然语言描述你想要的效果，例如：&#10;&#10;每秒造成 20 点魔法伤害，持续 6 秒，最多叠 5 层，可被强驱散移除&#10;&#10;或者点击上方快捷模板填充描述"
            rows="6"
          ></textarea>
        </div>

        <van-button
          block
          type="primary"
          size="large"
          icon="gem-o"
          :loading="generating"
          loading-text="AI 生成中..."
          @click="doGenerate"
        >
          生成效果配置
        </van-button>

        <p class="ai-hint">使用 DeepSeek 模型，生成约需 3~5 秒。需配置 DEEPSEEK_API_KEY。</p>
      </aside>

      <div class="ai-result-panel">
        <template v-if="!hasResult && !error">
          <div class="empty-ai">
            <van-icon name="smile-comment-o" size="48" />
            <h3>等待生成</h3>
            <p>在左侧输入效果描述后点击生成，AI 会自动为你创建 Buff 配置。</p>
          </div>
        </template>

        <template v-if="error">
          <div class="empty-ai error-state">
            <van-icon name="warning-o" size="48" color="#e05461" />
            <h3>生成失败</h3>
            <p>{{ error }}</p>
            <van-button size="small" plain type="danger" @click="doGenerate">重新生成</van-button>
          </div>
        </template>

        <template v-if="generated">
          <div class="result-header">
            <h2>生成结果</h2>
            <div class="result-actions">
              <van-button size="small" plain icon="replay" @click="doGenerate" :loading="generating">重新生成</van-button>
              <van-button
                size="small"
                type="primary"
                icon="success"
                :loading="saving"
                :disabled="Boolean(savedId)"
                @click="saveGenerated"
              >
                {{ savedId ? '已保存' : '保存到列表' }}
              </van-button>
              <van-button v-if="savedId" size="small" plain icon="arrow" @click="editGenerated">去编辑</van-button>
            </div>
          </div>

          <article class="buff-preview-card">
            <div class="preview-head">
              <div>
                <h3>{{ generated.displayName || '未命名' }}</h3>
                <span class="preview-key">{{ generated.key }}</span>
              </div>
              <span :class="['kind-badge', generated.modifierKind.toLowerCase()]">
                {{ getOptionLabel(generated.modifierKind) }}
              </span>
            </div>

            <p class="preview-desc">{{ generated.description || '无描述' }}</p>

            <div class="preview-grid">
              <div class="preview-item">
                <small>持续时间</small>
                <b>{{ generated.duration < 0 ? '永久' : generated.duration + ' 秒' }}</b>
              </div>
              <div class="preview-item">
                <small>叠加策略</small>
                <b>{{ formatStackPolicy(generated) }}</b>
              </div>
              <div class="preview-item">
                <small>最大层数</small>
                <b>{{ generated.maxStacks }}</b>
              </div>
              <div class="preview-item">
                <small>周期间隔</small>
                <b>{{ generated.thinkInterval > 0 ? generated.thinkInterval + ' 秒' : '无' }}</b>
              </div>
              <div class="preview-item">
                <small>驱散规则</small>
                <b>{{ getOptionLabel(generated.dispelRule) }}</b>
              </div>
              <div class="preview-item">
                <small>施加概率</small>
                <b>{{ generated.applyChance < 1 ? (generated.applyChance * 100).toFixed(0) + '%' : '必定' }}</b>
              </div>
            </div>

            <div class="preview-section" v-if="generated.statusEffects.length">
              <small>状态效果</small>
              <span class="preview-tags">
                <span v-for="s in generated.statusEffects" :key="s" class="tag">{{ getOptionLabel(s) }}</span>
              </span>
            </div>

            <div class="preview-section">
              <small>属性修改</small>
              <p>{{ attributeSummary(generated) }}</p>
            </div>

            <div class="preview-section">
              <small>效果动作</small>
              <p>{{ actionsSummary(generated) }}</p>
            </div>

            <div class="preview-section" v-if="generated.tags.length">
              <small>标签</small>
              <span class="preview-tags">
                <span v-for="t in generated.tags" :key="t" class="tag">{{ t }}</span>
              </span>
            </div>
          </article>
        </template>
      </div>
    </div>
  </section>
</template>
