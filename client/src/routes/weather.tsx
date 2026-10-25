import { useSuspenseQuery } from '@tanstack/react-query'
import { createFileRoute } from '@tanstack/react-router'
import { weatherForecastQuery } from '../api/weather'

export const Route = createFileRoute('/weather')({
  // Start fetching before the page renders (and when hovering the link).
  loader: ({ context }) => context.queryClient.ensureQueryData(weatherForecastQuery),
  pendingComponent: () => <p>Loading…</p>,
  errorComponent: ({ error }) => (
    <p className="text-red-600">
      Could not load weather: {error instanceof Error ? error.message : String(error)}
    </p>
  ),
  component: WeatherPage,
})

function WeatherPage() {
  const { data: forecasts, refetch, isFetching } = useSuspenseQuery(weatherForecastQuery)

  return (
    <section className="space-y-4">
      <h1 className="text-3xl font-bold">Weather forecast</h1>
      <p>
        Served by <code className="rounded bg-gray-100 px-1 dark:bg-gray-800">GET /api/weatherforecast</code> in{' '}
        <code className="rounded bg-gray-100 px-1 dark:bg-gray-800">server/api/weatherforecast.get.cs</code>.
      </p>
      <table className="w-full text-left">
        <thead>
          <tr className="border-b border-gray-200 dark:border-gray-800">
            <th className="p-2">Date</th>
            <th className="p-2">Temp (°C)</th>
            <th className="p-2">Temp (°F)</th>
            <th className="p-2">Summary</th>
          </tr>
        </thead>
        <tbody>
          {forecasts.map((f) => (
            <tr key={f.date} className="border-b border-gray-200 dark:border-gray-800">
              <td className="p-2">{f.date}</td>
              <td className="p-2">{f.temperatureC}</td>
              <td className="p-2">{f.temperatureF}</td>
              <td className="p-2">{f.summary}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <button
        type="button"
        onClick={() => refetch()}
        disabled={isFetching}
        className="rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {isFetching ? 'Refreshing…' : 'Refresh'}
      </button>
    </section>
  )
}
