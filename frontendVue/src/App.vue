<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header class="topbar">
    <div class="topbar-inner">
      <RouterLink to="/" class="brand">
        <img src="/favicon.svg" alt="" width="22" height="22" />
        BackendTray
      </RouterLink>
      <nav>
        <RouterLink to="/">系統狀態</RouterLink>
        <RouterLink to="/weather">天氣預報範例</RouterLink>
      </nav>
    </div>
  </header>

  <main class="page">
    <RouterView />
  </main>
</template>

<style scoped>
.topbar {
  background: var(--surface);
  border-bottom: 1px solid var(--border);
}

.topbar-inner {
  max-width: 960px;
  margin: 0 auto;
  padding: 0 16px;
  display: flex;
  align-items: center;
  gap: 24px;
  height: 56px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: var(--text);
}

nav {
  display: flex;
  gap: 4px;
  overflow-x: auto;
}

nav a {
  padding: 6px 12px;
  border-radius: 6px;
  color: var(--text-muted);
  white-space: nowrap;
}

nav a:hover {
  background: var(--hover);
  color: var(--text);
}

nav a.router-link-exact-active {
  background: var(--accent-soft);
  color: var(--accent);
}

.page {
  max-width: 960px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
</style>
