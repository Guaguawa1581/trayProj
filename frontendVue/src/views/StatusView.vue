<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { getSystemInfo } from '@/api/system'
import { useRequest } from '@/composables/useRequest'

const REFRESH_SECONDS = 10

const { data: info, error, loading, updatedAt, run: refresh } = useRequest(getSystemInfo)

// 去掉 +commit 後綴，完整版本號放在 title 裡。
const shortVersion = computed(() => info.value?.version?.split('+')[0] ?? '—')

// 定期更新：在管理介面儲存設定後 API 會自動重啟，這裡可以看到它斷線再恢復。
let timer: number | undefined
onMounted(() => {
  refresh()
  timer = window.setInterval(() => {
    if (document.visibilityState === 'visible') refresh()
  }, REFRESH_SECONDS * 1000)
})
onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <div class="page-header">
    <div>
      <h1>系統狀態</h1>
      <p>
        每 {{ REFRESH_SECONDS }} 秒自動更新
        <template v-if="updatedAt"> · 最後更新 {{ updatedAt.toLocaleTimeString('zh-TW', { hour12: false }) }}</template>
      </p>
    </div>
    <button class="btn" :disabled="loading" @click="refresh">{{ loading ? '更新中…' : '重新整理' }}</button>
  </div>

  <section class="card">
    <div class="card-title">
      <h2>後端 API</h2>
      <span v-if="info" class="badge ok">正常</span>
      <span v-else-if="error" class="badge error">無法連線</span>
      <span v-else class="badge muted">檢查中</span>
    </div>

    <p v-if="error" class="notice error">{{ error }}</p>

    <dl v-if="info" class="fields">
      <dt>版本</dt>
      <dd :title="info.version ?? undefined">{{ shortVersion }}</dd>

      <dt>執行環境</dt>
      <dd>{{ info.environment }}</dd>

      <dt>啟動方式</dt>
      <dd>{{ info.managedByBackendTray ? '由 BackendTray 服務管理' : '單獨執行（未經由 BackendTray 服務）' }}</dd>

      <dt>資料庫連線</dt>
      <dd>
        <code v-for="name in info.configuredConnections" :key="name" class="conn">{{ name }}</code>
        <span v-if="info.configuredConnections.length === 0" class="empty">未設定</span>
      </dd>
    </dl>

    <template v-if="info">
      <p v-if="!info.managedByBackendTray" class="notice warn">
        API 不是由 BackendTray 服務啟動的（例如用 <code>dotnet run</code>），連線字串不會自動注入，請用 user-secrets 自行設定。
      </p>
      <p v-else-if="info.configuredConnections.length === 0" class="notice warn">
        尚未設定資料庫連線。請在系統匣管理介面的「資料庫」頁設定並儲存，API 會自動重新啟動。
      </p>
    </template>
  </section>
</template>

<style scoped>
.card-title {
  display: flex;
  align-items: center;
  gap: 12px;
}

h2 {
  margin: 0;
  font-size: 1.0625rem;
}

.fields {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 10px 24px;
  margin: 16px 0 0;
}

dt {
  color: var(--text-muted);
}

dd {
  margin: 0;
  min-width: 0;
  overflow-wrap: anywhere;
}

.conn + .conn {
  margin-left: 6px;
}

.empty {
  color: var(--text-muted);
}
</style>
