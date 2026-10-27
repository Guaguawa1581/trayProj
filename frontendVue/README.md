# BackendTray 前端（Vue）

搭配 [backendTray](../backendTray) 的 Vue 3 + TypeScript + Vite 前端。正式環境由 BackendTray Gateway 在前端 port（預設 8080）提供，`/api/...` 由 Gateway 轉發給後端 API。

```
瀏覽器 ──▶ Gateway :8080 ─┬─ /api/*  ──▶ MainApi :5100
                          └─ 其他路徑 ──▶ wwwroot（這個專案的 build 產出，找不到的路徑回傳 index.html）
```

## 頁面

| 路徑 | 說明 |
|---|---|
| `/` | 系統狀態：呼叫 `GET /api/system/info`，顯示 API 版本、執行環境、是否由 BackendTray 服務管理、已設定的資料庫連線。每 10 秒自動更新 |
| `/weather` | 天氣預報範例：呼叫 `GET /api/weatherforecast` |
| 其他 | 找不到頁面 |

## 前置需求

- Node.js 18 以上（Vite 6）。改用 Node 20.19 以上的話，可以升級到 Vite 7。

```powershell
npm install
```

## 開發

先啟動後端 API，再啟動 Vite：

```powershell
# 在 backendTray
dotnet run --project src/Servers/MainApi

# 在這個專案
npm run dev
```

開啟 http://localhost:5173 。Vite 會把 `/api` 轉發到 `.env` 的 `API_PROXY_TARGET`（預設 `http://127.0.0.1:5100`），所以程式碼跟正式環境一樣都使用相對路徑 `/api/...`，不需要處理 CORS。

- 以主控台模式執行整套（`dotnet run --project src/BackendTray.Service`）時，可改成轉發到 Gateway：建立 `.env.local`，內容 `API_PROXY_TARGET=http://127.0.0.1:8080`。
- 在管理介面「服務與連接埠」改了 API 的 port，`API_PROXY_TARGET` 也要跟著改。
- 用 `dotnet run` 單獨執行 API 時，「系統狀態」會顯示「單獨執行」，這是正常的。

## 打包進安裝程式

```powershell
npm run deploy
```

會先做型別檢查與 `vite build`，再把 `dist` 複製到 `..\backendTray\frontend\dist`（保留 `.gitkeep`、清掉舊檔）。接著在 backendTray 執行：

```powershell
.\build\build.ps1
```

`build.ps1` 會把 `frontend\dist` 複製到 `artifacts\publish\wwwroot`，一起打包進 Setup.exe。只要在本機看效果，用 `.\build\build.ps1 -SkipInstaller -SkipTests` 後以主控台模式執行服務，開啟 http://localhost:8080 。

backendTray 不在同層時，用環境變數指定目標：

```powershell
$env:TRAY_FRONTEND_DIST = 'D:\path\to\backendTray\frontend\dist'; npm run deploy
```

## 跟 Gateway 配合的規則

- **呼叫後端一律用相對路徑 `/api/...`**（見 `src/api/http.ts`）。不要寫死 `http://localhost:5100`，正式環境 API 預設只接受本機連線，瀏覽器連不到。
- **前端路由不要用 `/api` 開頭**，這些路徑會被 Gateway 轉發給後端。
- **Vue Router 使用 history 模式**：Gateway 對找不到的路徑回傳 `index.html`，直接開啟或重新整理深層網址都沒問題。
- **不要更改 `base` 和 `build.assetsDir`**：Gateway 對 `/assets/*` 設定一年快取（檔名含 hash），`index.html` 每次重新驗證，更新後使用者會拿到新版。
- 新增後端 API 時（backendTray `src/Servers/<名稱>`，`server.json` 的 `routePrefix`），如果 `routePrefix` 不是 `/api`，`vite.config.ts` 的 `proxy` 也要加上相同的路徑。

## 指令

| 指令 | 說明 |
|---|---|
| `npm run dev` | 開發伺服器 |
| `npm run build` | 型別檢查並輸出到 `dist` |
| `npm run preview` | 預覽 `dist`（同樣會轉發 `/api`） |
| `npm run type-check` | 只做型別檢查 |
| `npm run deploy` | build 並複製到 backendTray |

## 目錄

```
src/
├─ api/            呼叫後端的函式與型別（http.ts 統一處理錯誤與逾時）
├─ composables/    useRequest：data / error / loading 狀態
├─ router/         路由（history 模式）
├─ views/          頁面
└─ assets/main.css 全域樣式（支援深色模式）
scripts/deploy.mjs 複製 build 產出到 backendTray
```
