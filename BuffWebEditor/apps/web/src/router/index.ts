import { createRouter, createWebHistory } from 'vue-router';
import AttributeSettingsView from '@/views/AttributeSettingsView.vue';
import BuffEditorView from '@/views/BuffEditorView.vue';
import BuffListView from '@/views/BuffListView.vue';
import DemoView from '@/views/DemoView.vue';
import ExportView from '@/views/ExportView.vue';
import GameplayTagsView from '@/views/GameplayTagsView.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/demo' },
    { path: '/demo', name: 'demo', component: DemoView, meta: { title: '演示' } },
    { path: '/buffs', name: 'buffs', component: BuffListView, meta: { title: '效果列表' } },
    { path: '/buffs/new', name: 'buff-new', component: BuffEditorView, meta: { title: '新建效果' } },
    { path: '/buffs/:id', name: 'buff-edit', component: BuffEditorView, meta: { title: '编辑效果' } },
    {
      path: '/attributes',
      name: 'attributes',
      component: AttributeSettingsView,
      meta: { title: '三维公式' },
    },
    { path: '/export', name: 'export', component: ExportView, meta: { title: 'Unity 导出' } },
    { path: '/gameplay-tags', name: 'gameplay-tags', component: GameplayTagsView, meta: { title: 'GameplayTags' } },
  ],
});

router.afterEach((to) => {
  document.title = `${String(to.meta.title ?? 'BuffWork')} · BuffWork`;
});

export default router;
