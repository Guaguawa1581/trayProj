import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

// 正式環境由 BackendTray Gateway 把 /api 轉發給後端；開發與 preview 時由 Vite 做同樣的轉發，
// 所以前端程式碼一律用相對路徑 /api/...，也不需要處理 CORS。
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxy = { '/api': env.API_PROXY_TARGET || 'http://127.0.0.1:5100' }

  return {
    plugins: [vue()],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: { proxy },
    preview: { proxy },
    // Gateway 對 /assets 下的檔案設定長期快取，Vite 預設的輸出位置（assets/，檔名含 hash）剛好符合，不要更改。
    build: { assetsDir: 'assets' },
  }
})
