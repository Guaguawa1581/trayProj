<script setup lang="ts">
import { RouterLink, useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <section class="card not-found">
    <h1>找不到頁面</h1>
    <p>
      <code>{{ route.fullPath }}</code> 不存在。
    </p>
    <RouterLink to="/" class="btn">回到系統狀態</RouterLink>
  </section>
</template>

<style scoped>
.not-found {
  text-align: center;
  padding: 48px 20px;
}

.not-found p {
  color: var(--text-muted);
  overflow-wrap: anywhere;
}

.btn {
  display: inline-block;
}
</style>
