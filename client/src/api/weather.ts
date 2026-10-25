import { queryOptions } from '@tanstack/react-query'
import { http } from '../lib/http'

// Matches the WeatherForecast record in server/api/weatherforecast.get.cs
export type WeatherForecast = {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

export async function getWeatherForecast() {
  const { data } = await http.get<WeatherForecast[]>('/weatherforecast')
  return data
}

export const weatherForecastQuery = queryOptions({
  queryKey: ['weatherforecast'],
  queryFn: getWeatherForecast,
})
