# BackendTray

以 Windows 服務執行網站與 Web API（取代 IIS），系統匣程式是管理介面。整套打包成一支安裝程式，同一支 Setup.exe 也是更新包。

```
BackendTray.Manager.exe（系統匣管理介面，系統管理員）
   │  HTTP/JSON over named pipe \\.\pipe\BackendTray.Control
   ▼
BackendTray.Service.exe（Windows 服務，NT SERVICE\BackendTray，不需登入即執行）
   └─ Supervisor（Job Object：服務結束時子程序一併結束）
        ├─▶ gateway\BackendTray.Gateway.exe   :8080  前端 port：wwwroot（Vue）＋ /api 轉發
        └─▶ servers\MainApi\MainApi.exe        :5100  後端 port（預設只接受本機連線）
```

## 專案

| 路徑 | 說明 |
|---|---|
| `src/BackendTray.Service` | Windows 服務：管理 API、設定、啟動並監控子程序；也提供安裝程式用的 `install` / `uninstall` / `stop-for-update` 指令 |
| `src/BackendTray.Manager` | WinForms 系統匣與主畫面 |
| `src/BackendTray.Core` | 不含 UI 的核心：設定（DPAPI 加密密碼）、Supervisor、DB 連線測試 |
| `src/BackendTray.Contracts` | 管理 API 的 DTO 與共用名稱 |
| `src/BackendTray.Gateway` | 前端靜態檔（SPA fallback）＋ YARP 反向代理 |
| `src/BackendTray.Agent` | 給 API 引用的小函式庫：讓服務可以優雅地停止 API |
| `src/Servers/MainApi` | 範例後端 API（Swagger、`/health`、`server.json`）；`/api/products` 的資料來自 SQL Server（`Database/CreateTestData.sql`） |
| `frontend/dist` | 放 Vue `npm run build` 的產出（見 `frontend/README.md`） |
| `build/` | `build.ps1` 打包腳本、Inno Setup 安裝腳本 |

設定與 log 在 `C:\ProgramData\BackendTray`（開發時在 `artifacts\data`），更新時不會被覆蓋。

## 打包與測試

完整步驟見 [docs/build-and-test.md](docs/build-and-test.md)。

- **打包**：`.\build\build.ps1` → `artifacts\installer\BackendTray-Setup-<版本>.exe`。同一支 Setup.exe 可用於第一次安裝與更新。
- **單元測試**：`dotnet test`
- **只執行 API**：`dotnet run --project src/Servers/MainApi`，開啟 http://localhost:5100/swagger
- **不安裝、執行整套**：先 `.\build\build.ps1 -SkipInstaller`，再 `dotnet run --project src/BackendTray.Service`，最後以 Debug 執行 `BackendTray.Manager`

## 新增一個 API

1. 在 `src/Servers/<名稱>/` 建立 ASP.NET Core 專案，引用 `BackendTray.Agent` 並在 `Program.cs` 呼叫 `builder.AddTrayAgent()`、`app.MapHealthChecks("/health")`。
2. 加上 `server.json`（`id`、`displayName`、`executable`、`defaultPort`、`routePrefix`、`connections`），並設定為隨發佈輸出。
3. 執行 `build.ps1`。服務會自動找到它、在管理介面列出、依 `routePrefix` 讓 Gateway 轉發；連線字串以 `ConnectionStrings__<名稱>` 環境變數傳入。
