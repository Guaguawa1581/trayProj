/** 後端無法連線或回傳錯誤時拋出；message 可以直接顯示給使用者。 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status?: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

/**
 * 以 GET 取得 JSON。path 一律用相對路徑 /api/...：
 * 正式環境由 Gateway 轉發給後端，開發時由 vite.config.ts 的 proxy 轉發。
 */
export async function getJson<T>(path: string, timeoutMs = 10_000): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(timeoutMs),
    })
  } catch (error) {
    const timedOut = error instanceof DOMException && error.name === 'TimeoutError'
    throw new ApiError(timedOut ? '後端 API 回應逾時' : '無法連線到伺服器')
  }

  if (!response.ok) {
    // Gateway 連不到後端時回傳 502/503/504；API 由服務自動重啟期間也會短暫出現。
    const message = [502, 503, 504].includes(response.status)
      ? '後端 API 沒有回應，可能尚未啟動或正在重新啟動'
      : `後端回傳 ${response.status} ${response.statusText}`.trim()
    throw new ApiError(message, response.status)
  }

  return (await response.json()) as T
}
