import { createRouter, createWebHistory } from 'vue-router'
import StatusView from '@/views/StatusView.vue'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
  }
}

// history 模式：Gateway 對找不到的路徑會回傳 index.html，直接開啟或重新整理深層網址都沒問題。
// 注意前端路由不要用 /api 開頭，那些路徑會被 Gateway 轉發給後端。
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'status', component: StatusView, meta: { title: '系統狀態' } },
    {
      path: '/weather',
      name: 'weather',
      component: () => import('@/views/WeatherView.vue'),
      meta: { title: '天氣預報範例' },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
      meta: { title: '找不到頁面' },
    },
  ],
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} - BackendTray` : 'BackendTray'
})

export default router
