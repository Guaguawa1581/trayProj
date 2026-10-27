# 前端（Vue build 產出）

把 Vue 專案 `npm run build` 產生的 `dist` 內容放到 `frontend/dist/`（`index.html` 要在 `dist` 第一層），
再執行 `build\build.ps1`，就會一起打包進安裝程式，由 Gateway 在前端 port 提供。

- Vue Router 可以用 history 模式：找不到的路徑會回傳 `index.html`。
- 呼叫後端一律用相對路徑 `/api/...`，正式環境由 Gateway 轉發給後端，不需要處理 CORS。
- 開發時在 `vite.config.ts` 加上相同的轉發：

```ts
export default defineConfig({
  server: {
    proxy: { '/api': 'http://localhost:5100' },
  },
})
```

`dist` 不進版控（見 `.gitignore`）。
