<script setup lang="ts">
import { onMounted } from 'vue'
import { getWeatherForecast } from '@/api/weather'
import { useRequest } from '@/composables/useRequest'

// 後端回傳英文描述，這裡轉成中文；沒有對應的就原樣顯示。
const SUMMARIES: Record<string, string> = {
  Freezing: '酷寒',
  Bracing: '凜冽',
  Chilly: '寒冷',
  Cool: '涼爽',
  Mild: '溫和',
  Warm: '溫暖',
  Balmy: '宜人',
  Hot: '炎熱',
  Sweltering: '悶熱',
  Scorching: '酷熱',
}

const { data: forecasts, error, loading, run: refresh } = useRequest(getWeatherForecast)

function formatDate(date: string) {
  // DateOnly 沒有時區，直接拆字串，避免 new Date() 以 UTC 解析後在部分時區差一天。
  const [year, month, day] = date.split('-').map(Number)
  return new Date(year!, month! - 1, day!).toLocaleDateString('zh-TW', {
    month: 'numeric',
    day: 'numeric',
    weekday: 'short',
  })
}

onMounted(refresh)
</script>

<template>
  <div class="page-header">
    <div>
      <h1>天氣預報範例</h1>
      <p>呼叫範例 API <code>GET /api/weatherforecast</code>，每次取得的資料都是隨機產生的。</p>
    </div>
    <button class="btn" :disabled="loading" @click="refresh">{{ loading ? '讀取中…' : '重新取得' }}</button>
  </div>

  <section class="card table-card">
    <p v-if="error" class="notice error">{{ error }}</p>
    <p v-else-if="!forecasts" class="placeholder">讀取中…</p>

    <table v-else>
      <thead>
        <tr>
          <th>日期</th>
          <th class="num">攝氏</th>
          <th class="num">華氏</th>
          <th>概況</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="forecast in forecasts" :key="forecast.date">
          <td>{{ formatDate(forecast.date) }}</td>
          <td class="num">{{ forecast.temperatureC }} °C</td>
          <td class="num">{{ forecast.temperatureF }} °F</td>
          <td>{{ forecast.summary ? (SUMMARIES[forecast.summary] ?? forecast.summary) : '—' }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.table-card {
  padding: 4px 0;
  overflow-x: auto;
}

.table-card .notice {
  margin: 16px 20px;
}

.placeholder {
  margin: 16px 20px;
  color: var(--text-muted);
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 10px 20px;
  text-align: left;
  white-space: nowrap;
}

th {
  color: var(--text-muted);
  font-weight: 500;
  font-size: 0.875rem;
}

tbody tr {
  border-top: 1px solid var(--border);
}

.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
</style>
