<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { showConfirmDialog, showFailToast, showSuccessToast } from 'vant';
import AttributeModifierEditor from '@/components/AttributeModifierEditor.vue';
import EffectActionEditor from '@/components/EffectActionEditor.vue';
import EnumSelect from '@/components/EnumSelect.vue';
import PageHeader from '@/components/PageHeader.vue';
import { useBuffStore } from '@/stores/buffs';
import {
  createEmptyBuff,
  dispelRules,
  modifierKinds,
  stackPolicies,
  statusEffectOptions,
  type BuffPayload,
} from '@/types/buff';

const route = useRoute();
const router = useRouter();
const store = useBuffStore();
const saving = ref(false);
const loading = ref(false);
const activeTab = ref(0);
const draft = reactive<BuffPayload>(createEmptyBuff());
const tagsText = ref('');
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : undefined));
const isNew = computed(() => !id.value);

function clonePayload(payload: BuffPayload): BuffPayload {
  return JSON.parse(JSON.stringify(payload)) as BuffPayload;
}

onMounted(async () => {
  if (!id.value) return;
  loading.value = true;
  try {
    const buff = await store.getById(id.value);
    const { id: _id, createdAt: _createdAt, updatedAt: _updatedAt, ...payload } = buff;
    Object.assign(draft, clonePayload(payload));
    tagsText.value = buff.tags.join(', ');
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    loading.value = false;
  }
});

function createPayload(): BuffPayload {
  const plainDraft = clonePayload(draft);
  return {
    ...plainDraft,
    duration: Number(draft.duration),
    maxStacks: Math.max(1, Number(draft.maxStacks)),
    thinkInterval: Number(draft.thinkInterval),
    auraRadius: Math.max(0, Number(draft.auraRadius)),
    lingerDuration: Math.max(0, Number(draft.lingerDuration)),
    tags: tagsText.value
      .split(',')
      .map((tag) => tag.trim())
      .filter(Boolean),
  };
}

async function save() {
  if (!draft.key.trim() || !draft.displayName.trim()) {
    showFailToast('请填写 Buff Key 和显示名称');
    activeTab.value = 0;
    return;
  }
  saving.value = true;
  try {
    const saved = await store.save(createPayload(), id.value);
    showSuccessToast('保存成功');
    if (isNew.value) await router.replace(`/buffs/${saved.id}`);
  } catch (error) {
    showFailToast((error as Error).message);
  } finally {
    saving.value = false;
  }
}

async function remove() {
  if (!id.value) return;
  try {
    await showConfirmDialog({ title: '删除 Buff', message: '删除后无法从编辑器中恢复，确认继续吗？' });
    await store.remove(id.value);
    showSuccessToast('已删除');
    await router.replace('/buffs');
  } catch (error) {
    if ((error as string) !== 'cancel') showFailToast((error as Error).message);
  }
}
</script>

<template>
  <section class="page-container editor-page">
    <PageHeader
      eyebrow="Modifier Editor"
      :title="isNew ? '新建 Buff' : draft.displayName || '编辑 Buff'"
      description="配置运行规则、属性修改和事件动作，保存后即可导出给 Unity。"
    >
      <template #actions>
        <van-button v-if="!isNew" plain type="danger" icon="delete-o" @click="remove">删除</van-button>
        <van-button type="primary" icon="success" :loading="saving" @click="save">保存</van-button>
      </template>
    </PageHeader>

    <van-loading v-if="loading" class="page-loading" vertical>读取配置...</van-loading>

    <template v-else>
      <van-tabs v-model:active="activeTab" sticky offset-top="0" shrink>
        <van-tab title="基础信息">
          <div class="editor-panel">
            <div class="form-grid form-grid-2">
              <van-field v-model="draft.key" label="Buff Key" placeholder="例如 PoisonDoT" required />
              <van-field v-model="draft.displayName" label="显示名称" placeholder="例如 腐蚀毒素" required />
              <EnumSelect
                :model-value="draft.modifierKind"
                label="Modifier 类型"
                :options="modifierKinds"
                @update:model-value="draft.modifierKind = $event as BuffPayload['modifierKind']"
              />
              <EnumSelect
                :model-value="draft.stackPolicy"
                label="叠加策略"
                :options="stackPolicies"
                @update:model-value="draft.stackPolicy = $event as BuffPayload['stackPolicy']"
              />
              <van-field v-model.number="draft.duration" type="number" label="持续时间（秒）" input-align="right" />
              <van-field v-model.number="draft.maxStacks" type="digit" label="最大层数" input-align="right" />
              <van-field
                v-model.number="draft.thinkInterval"
                type="number"
                label="周期间隔（秒）"
                input-align="right"
              />
              <EnumSelect
                :model-value="draft.dispelRule"
                label="驱散规则"
                :options="dispelRules"
                @update:model-value="draft.dispelRule = $event as BuffPayload['dispelRule']"
              />
            </div>

            <van-field
              v-model="draft.description"
              rows="3"
              autosize
              type="textarea"
              label="说明"
              placeholder="描述用途、表现和策划备注"
            />
            <van-field v-model="tagsText" label="标签" placeholder="demo, control, hero（逗号分隔）" />

            <div class="subsection">
              <h3>状态效果</h3>
              <van-checkbox-group v-model="draft.statusEffects" direction="horizontal" class="checkbox-cloud">
                <van-checkbox v-for="status in statusEffectOptions" :key="status" :name="status" shape="square">
                  {{ status }}
                </van-checkbox>
              </van-checkbox-group>
            </div>

            <div class="boolean-grid">
              <van-cell title="受状态抗性影响"><template #right-icon><van-switch v-model="draft.affectedByStatusResistance" size="20" /></template></van-cell>
              <van-cell title="死亡时移除"><template #right-icon><van-switch v-model="draft.removeOnDeath" size="20" /></template></van-cell>
              <van-cell title="隐藏图标"><template #right-icon><van-switch v-model="draft.isHidden" size="20" /></template></van-cell>
              <van-cell title="被动效果"><template #right-icon><van-switch v-model="draft.isPassive" size="20" /></template></van-cell>
              <van-cell title="光环提供者"><template #right-icon><van-switch v-model="draft.isAura" size="20" /></template></van-cell>
            </div>

            <div v-if="draft.isAura" class="form-grid form-grid-2 subsection">
              <van-field v-model.number="draft.auraRadius" type="number" label="光环半径" input-align="right" />
              <van-field v-model.number="draft.lingerDuration" type="number" label="离开后残留（秒）" input-align="right" />
            </div>
          </div>
        </van-tab>

        <van-tab :title="`属性修改 ${draft.attributeModifiers.length}`">
          <div class="editor-panel">
            <div class="section-intro">
              <h2>Attribute Modifiers</h2>
              <p>可直接修改力量、敏捷、智力及派生战斗属性。</p>
            </div>
            <AttributeModifierEditor v-model="draft.attributeModifiers" />
          </div>
        </van-tab>

        <van-tab :title="`效果动作 ${draft.effectActions.length}`">
          <div class="editor-panel">
            <div class="section-intro">
              <h2>Effect Actions</h2>
              <p>伤害、治疗、施加 Modifier 和驱散统一通过动作描述导出。</p>
            </div>
            <EffectActionEditor v-model="draft.effectActions" />
          </div>
        </van-tab>
      </van-tabs>

      <div class="mobile-save-bar">
        <van-button block type="primary" :loading="saving" @click="save">保存配置</van-button>
      </div>
    </template>
  </section>
</template>
