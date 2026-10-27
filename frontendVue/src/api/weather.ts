import { getJson } from './http'

/** 對應 MainApi 的 GET /api/weatherforecast（範例 API）。 */
export interface WeatherForecast {
  /** DateOnly，格式 yyyy-MM-dd。 */
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

export const getWeatherForecast = () => getJson<WeatherForecast[]>('/api/weatherforecast')
