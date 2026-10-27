# 打包與測試

這份文件說明兩件事：

1. [打包](#一打包)：產生安裝程式，安裝或更新到目標電腦。
2. [不打包的測試方式](#二不打包的測試方式)：開發時直接從原始碼執行與驗證。

以下指令都在專案根目錄的 PowerShell 執行。

## 前置需求

| 項目 | 用途 | 安裝方式 |
|---|---|---|
| Windows 10/11 x64 | 開發與執行環境 | — |
| .NET 10 SDK | 建置、測試、發佈 | `winget install Microsoft.DotNet.SDK.10` |
| Inno Setup 6 | 只有產生安裝程式時需要 | `winget install JRSoftware.InnoSetup` |
| Visual Studio 2026 | 偵錯管理介面、使用 WinForms 設計工具（可改用 `dotnet run`） | — |
| SQL Server | 測試資料庫連線（不測資料庫可以不用） | — |

如果 PowerShell 不允許執行 `build.ps1`，請改用：

```powershell
powershell -ExecutionPolicy Bypass -File .\build\build.ps1
```

---

## 一、打包

### 1. 放入前端（可省略）

把 Vue 專案 `npm run build` 的產出放到 `frontend\dist\`，`index.html` 要在 `dist` 的第一層。
沒有放的話，網站會顯示「前端尚未部署」的頁面，其餘功能不受影響。

### 2. 調整版本號

修改根目錄 `Directory.Build.props` 的 `<Version>`。所有程式和安裝程式都使用這個版本號。

- 每次發佈都要調高版本號。安裝程式不會阻止安裝較舊的版本，需要自行留意。
- 不要修改 `build\installer\BackendTray.iss` 裡的 `AppId`。安裝程式靠它辨識「同一個產品」，改了之後就無法更新已安裝的版本。

### 3. 執行打包腳本

```powershell
.\build\build.ps1
```

腳本依序執行：

1. 執行單元測試。任何一個失敗就停止打包。
2. 以 framework-dependent、win-x64 模式發佈各元件到 `artifacts\publish`。
3. 把 `frontend\dist` 複製到 `artifacts\publish\wwwroot`。
4. 用 Inno Setup 產生 `artifacts\installer\BackendTray-Setup-<版本>.exe`。

| 參數 | 說明 |
|---|---|
| `-SkipTests` | 不執行單元測試 |
| `-SkipInstaller` | 只發佈到 `artifacts\publish`，不產生安裝程式 |
| `-Configuration Debug` | 以 Debug 組態建置（預設 Release） |

發佈後的目錄結構，也就是安裝後 `C:\Program Files\BackendTray` 的內容：

```
artifacts\publish
├─ service\BackendTray.Service.exe     Windows 服務
├─ manager\BackendTray.Manager.exe     系統匣管理介面
├─ gateway\BackendTray.Gateway.exe     前端 port（8080）
├─ servers\MainApi\                    後端 API（5100），含 server.json
└─ wwwroot\                            Vue 前端
```

### 4. 安裝到目標電腦

**先決條件**：目標電腦要先安裝以下兩個執行環境（x64），下載頁面是 https://dotnet.microsoft.com/download/dotnet/10.0 。

- ASP.NET Core Runtime 10
- .NET Desktop Runtime 10

安裝程式只會檢查，不會自動下載。缺少時會跳出提示並停止安裝。

**安裝**：以系統管理員身分執行 `BackendTray-Setup-<版本>.exe`，安裝程式會：

1. 把程式裝到 `C:\Program Files\BackendTray`。
2. 建立 Windows 服務 `BackendTray`：
   - 帳戶：`NT SERVICE\BackendTray`
   - 啟動類型：自動（延遲啟動）
   - 失敗時自動重啟
3. 設定 `C:\ProgramData\BackendTray` 的權限，只有 SYSTEM、Administrators 和服務帳戶可以存取。
4. 建立防火牆規則，允許網域與私人網路連入。
5. 建立工作排程器工作：系統管理員登入時自動開啟管理介面。
6. 啟動服務，並開啟管理介面。

**安裝後確認**：

```powershell
Get-Service BackendTray        # Status 應為 Running
```

| 網址 | 預期結果 |
|---|---|
| http://localhost:8080 | 前端頁面（或「前端尚未部署」） |
| http://localhost:8080/api/system/info | 經由 Gateway 轉發到後端，`managedByBackendTray` 為 `true` |
| http://localhost:5100/swagger | Swagger（只能在伺服器本機開啟） |

接著在管理介面的「資料庫」頁設定 SQL Server 連線，按「測試連線」，再按「儲存設定」。

### 5. 更新

執行新版本的 `BackendTray-Setup-<版本>.exe` 即可，不需要先移除舊版。若要無人值守更新：

```powershell
BackendTray-Setup-1.0.1.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

更新流程：

1. 停止服務，並關閉所有登入工作階段的管理介面。
2. 取代程式檔。`wwwroot` 會先整個清空，避免舊版前端檔案殘留。
3. 更新服務設定。管理員設定過的啟動類型會保留。
4. 更新前服務在執行的話，自動重新啟動。

`C:\ProgramData\BackendTray` 裡的設定（`settings.json`）和 log 不會被覆蓋。

### 6. 移除

從「設定 → 應用程式」解除安裝 BackendTray。服務、防火牆規則和排程工作都會移除。

`C:\ProgramData\BackendTray`（設定與 log）會保留，以免誤刪；不需要的話請手動刪除。

### 7. 疑難排解

log 都在 `C:\ProgramData\BackendTray\logs\`。這個資料夾只有系統管理員能開。

| 檔案 | 內容 |
|---|---|
| `setup-*.log` | 安裝、更新、移除時服務設定的過程 |
| `service-*.log` | 服務本身：啟動、停止、設定變更、警告 |
| `gateway-*.log`、`main-api-*.log` | 各伺服器的輸出 |

| 狀況 | 處理 |
|---|---|
| 打包時出現 `ISCC.exe not found` | 安裝 Inno Setup 6 |
| 安裝時提示缺少執行環境 | 安裝上面列的兩個 .NET 10 執行環境 |
| 安裝時跳出「設定 Windows 服務失敗」 | 查看 `setup-*.log` |
| 伺服器狀態是「異常」 | 查看該伺服器的 log，常見原因是 port 被佔用；到管理介面「服務與連接埠」改 port |

---

## 二、不打包的測試方式

依照要測的範圍選一種，越前面的越快。

| 方式 | 測試範圍 | 需要系統管理員權限 |
|---|---|---|
| [A. 單元測試](#a-單元測試) | 設定、驗證、啟動規劃等核心邏輯 | 否 |
| [B. 只執行後端 API](#b-只執行後端-api) | API 本身 | 否 |
| [C. 執行整套（主控台模式）](#c-執行整套主控台模式) | 服務、Gateway、API、管理介面 | 否 |
| [D. 註冊成真正的 Windows 服務](#d-註冊成真正的-windows-服務) | 加上服務註冊、權限、防火牆、開機自動啟動 | 是 |

### A. 單元測試

```powershell
dotnet test
```

測試位於 `tests\BackendTray.Core.Tests`，涵蓋：

- 設定檔讀寫
- 密碼不外洩
- port 衝突驗證
- 連線字串注入
- Gateway 路由產生
- `server.json` 掃描

`build.ps1` 打包前也會自動執行。

### B. 只執行後端 API

開發 API 時不需要啟動服務：

```powershell
dotnet run --project src/Servers/MainApi
```

執行後開啟 http://localhost:5100/swagger 。

單獨執行時，服務不會注入連線字串，需要自己設定。建議用 user-secrets，密碼不會進版控：

```powershell
dotnet user-secrets init --project src/Servers/MainApi
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=AppDb;User Id=app;Password=***;TrustServerCertificate=True" --project src/Servers/MainApi
```

`src\Servers\MainApi\Database\CreateTestData.sql` 會建立上面的資料庫 `AppDb`、帳號 `app` 和 `dbo.Products` 範例資料，給 `/api/products` 使用。以系統管理員身分在 SSMS 執行，或：

```powershell
sqlcmd -S localhost -E -C -i src\Servers\MainApi\Database\CreateTestData.sql
```

可以重複執行，每次都會把 `dbo.Products` 還原成初始資料。要測的請求寫在 `src\Servers\MainApi\MainApi.http`。

前端開發時，在 Vue 專案的 `vite.config.ts` 把 `/api` 轉發到這個 API。這樣前端程式碼跟正式環境一樣都使用 `/api/...`：

```ts
export default defineConfig({
  server: { proxy: { '/api': 'http://localhost:5100' } },
})
```

### C. 執行整套（主控台模式）

不安裝 Windows 服務，直接以主控台模式執行服務，再搭配 Debug 版的管理介面。

**步驟 1：發佈 Gateway 與 API**

```powershell
.\build\build.ps1 -SkipInstaller -SkipTests
```

服務啟動的是 `artifacts\publish` 裡已發佈的 Gateway 和 API。修改了 API、Gateway 或前端之後，要重新執行這一步。

**步驟 2：啟動服務**

```powershell
dotnet run --project src/BackendTray.Service
```

- 在 `BackendTray.slnx` 所在的原始碼目錄內執行時，服務會自動使用 `artifacts\publish`（程式）與 `artifacts\data`（設定、log）。
- log 會同時顯示在主控台。
- 按 Ctrl+C 停止服務，Gateway 和 API 會一起結束。

**步驟 3：啟動管理介面**

在 Visual Studio 中把 `BackendTray.Manager` 設為啟動專案，以 Debug 執行。或在另一個終端機執行：

```powershell
dotnet run --project src/BackendTray.Manager
```

- Debug 組態不要求系統管理員權限，可以直接連上步驟 2 的服務；Release 組態才要求。
- 想同時偵錯服務與管理介面：在 Visual Studio 的「設定啟動專案」選「多個啟動專案」，勾選 `BackendTray.Service` 與 `BackendTray.Manager`。
- 想偵錯由服務啟動的 API：用「偵錯 → 附加至處理序」，選 `MainApi.exe`。

**驗證清單**

| 動作 | 預期結果 |
|---|---|
| 開啟管理介面的「總覽」 | 前端 Gateway 與主要 API 都是「執行中」 |
| 開啟 http://localhost:8080/api/system/info | 回傳 JSON，`managedByBackendTray` 為 `true` |
| 「資料庫」頁填入連線後按「測試連線」 | 顯示連線成功與 SQL Server 版本，或具體的失敗原因 |
| 儲存資料庫設定 | 主要 API 自動重新啟動；`/api/system/info` 的 `configuredConnections` 出現 `Default` |
| 「服務與連接埠」把 API 改成 5150 並儲存 | 主要 API 與 Gateway 重新啟動；`/api/...` 仍可正常使用 |
| 系統匣右鍵「暫停服務」 | http://localhost:8080 無法連線，圖示變灰 |
| 系統匣右鍵「繼續服務」 | 網站恢復，圖示變綠 |
| 在工作管理員結束 `MainApi.exe` | 約 1 秒內自動重新啟動，「重啟」欄位加 1 |
| 按主畫面右上角 X | 視窗隱藏，系統匣圖示仍在 |
| 單擊或雙擊系統匣圖示 | 主畫面重新出現 |
| 再執行一次管理介面 | 不會開第二個，而是把現有的視窗帶到最前面 |

在這個模式下，下列功能會顯示「服務未安裝」或停用，屬於正常現象，請改用方式 D 或實際安裝來測試：

- 「啟動／停止服務」
- 「啟動類型」
- 「登入時自動開啟管理介面」

系統匣的「結束」在這個模式下只會關閉管理介面；主控台中的服務要自己按 Ctrl+C 停止。

**清除測試資料**：刪除 `artifacts\data`，裡面是設定與 log。

### D. 註冊成真正的 Windows 服務

不產生安裝程式，直接把 `artifacts\publish` 註冊成 Windows 服務。用來測試服務註冊、資料夾權限、防火牆、開機自動啟動和管理介面的管理員權限。以下指令都要在**系統管理員**的 PowerShell 執行。

```powershell
.\build\build.ps1 -SkipInstaller

# 建立服務並啟動（服務直接執行 artifacts\publish 裡的檔案）
.\artifacts\publish\service\BackendTray.Service.exe install

# 開啟 Release 版管理介面（會要求系統管理員權限）
.\artifacts\publish\manager\BackendTray.Manager.exe
```

測試完一定要移除。否則之後的正式安裝會沿用指向 `artifacts` 的排程工作：

```powershell
.\artifacts\publish\service\BackendTray.Service.exe uninstall
```

注意事項：

- 這個模式的設定與 log 寫在 `C:\ProgramData\BackendTray`，和正式安裝共用同一個位置。
- 服務執行中，`artifacts\publish` 的檔案會被鎖住。重新執行 `build.ps1` 前要先停止服務（`Stop-Service BackendTray`）或先 `uninstall`。
- 服務帳戶 `NT SERVICE\BackendTray` 必須能讀取專案資料夾。如果專案放在 `C:\Users\<使用者>\` 底下，服務會因為沒有權限而無法啟動。這時請先把 `artifacts\publish` 複製到例如 `C:\BackendTrayDev`，再從那裡執行 `install`。
