import { getJson } from './http'

/** 對應 MainApi 的 GET /api/system/info（SystemController.Info）。 */
export interface SystemInfo {
  /** AssemblyInformationalVersion，可能帶有 +commit 後綴。 */
  version: string | null
  environment: string
  /** 由 BackendTray 服務啟動時為 true；單獨 dotnet run 時為 false。 */
  managedByBackendTray: boolean
  /** 已設定的連線字串名稱（只有名稱，不含內容）。 */
  configuredConnections: string[]
}

export const getSystemInfo = () => getJson<SystemInfo>('/api/system/info')
