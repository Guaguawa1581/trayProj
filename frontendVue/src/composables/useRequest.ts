import { ref, shallowRef } from 'vue'

/** 包裝一個 API 呼叫，提供 data / error / loading 狀態。上一次還沒完成時，再次呼叫 run 會被略過。 */
export function useRequest<T>(request: () => Promise<T>) {
  const data = shallowRef<T>()
  const error = ref<string>()
  const loading = ref(false)
  const updatedAt = ref<Date>()

  async function run() {
    if (loading.value) return
    loading.value = true
    try {
      data.value = await request()
      error.value = undefined
    } catch (e) {
      // 失敗時清掉舊資料，避免把過時的狀態當成目前狀態顯示。
      data.value = undefined
      error.value = e instanceof Error ? e.message : String(e)
    } finally {
      loading.value = false
      updatedAt.value = new Date()
    }
  }

  return { data, error, loading, updatedAt, run }
}
