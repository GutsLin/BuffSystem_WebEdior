<script setup lang="ts">
import { useRoute } from 'vue-router';

const route = useRoute();
const navigation = [
  { to: '/demo', label: '演示', icon: 'play-circle-o' },
  { to: '/buffs', label: '效果', icon: 'cluster-o' },
  { to: '/attributes', label: '三维', icon: 'chart-trending-o' },
  { to: '/export', label: '导出', icon: 'down' },
];
</script>

<template>
  <div class="app-shell">
    <aside class="desktop-sidebar">
      <RouterLink class="brand" to="/demo">
        <span class="brand-mark">BW</span>
        <span>
          <strong>BuffWork</strong>
          <small>Unity 数据编辑器</small>
        </span>
      </RouterLink>

      <nav class="sidebar-nav">
        <RouterLink
          v-for="item in navigation"
          :key="item.to"
          :to="item.to"
          :class="{ active: route.path.startsWith(item.to) }"
        >
          <van-icon :name="item.icon" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>

      <div class="sidebar-note">
        <span class="online-dot" />
        <div>
          <strong>JSON 文件存储</strong>
          <small>Docker 数据卷持久化</small>
        </div>
      </div>
    </aside>

    <main class="app-main">
      <slot />
    </main>

    <van-tabbar route class="mobile-tabbar" safe-area-inset-bottom>
      <van-tabbar-item v-for="item in navigation" :key="item.to" :to="item.to" :icon="item.icon">
        {{ item.label }}
      </van-tabbar-item>
    </van-tabbar>
  </div>
</template>
