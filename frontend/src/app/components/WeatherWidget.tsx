import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/platform/api/client'

interface Weather { tempC: number; code: number; highC: number; lowC: number }

/** Map a WMO weather code to an emoji + i18n condition key. */
function condition(code: number): { icon: string; key: string } {
  if (code === 0) return { icon: '☀️', key: 'clear' }
  if (code <= 2) return { icon: '🌤️', key: 'partly' }
  if (code === 3) return { icon: '☁️', key: 'cloudy' }
  if (code <= 48) return { icon: '🌫️', key: 'fog' }
  if (code <= 67) return { icon: '🌧️', key: 'rain' }
  if (code <= 77) return { icon: '❄️', key: 'snow' }
  if (code <= 82) return { icon: '🌦️', key: 'showers' }
  if (code <= 86) return { icon: '🌨️', key: 'snow' }
  return { icon: '⛈️', key: 'storm' }
}

const GEO_KEY = 'homeos.geo'

/** A compact weather card for the dashboard. Uses the browser location once (remembered), else Sarajevo. */
export function WeatherWidget() {
  const { t } = useTranslation()
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(() => {
    try { const s = localStorage.getItem(GEO_KEY); return s ? JSON.parse(s) : null } catch { return null }
  })

  useEffect(() => {
    if (coords || !('geolocation' in navigator)) return
    navigator.geolocation.getCurrentPosition(
      (p) => { const c = { lat: p.coords.latitude, lon: p.coords.longitude }; setCoords(c); localStorage.setItem(GEO_KEY, JSON.stringify(c)) },
      () => { /* denied → backend default (Sarajevo) */ },
      { timeout: 8000 },
    )
  }, [coords])

  const { data, isLoading } = useQuery({
    queryKey: ['weather', coords?.lat ?? 0, coords?.lon ?? 0],
    queryFn: () => api.get<Weather | null>(`/api/weather${coords ? `?lat=${coords.lat}&lon=${coords.lon}` : ''}`),
    staleTime: 30 * 60 * 1000,
    retry: 1,
  })

  // Always render a card when the widget is on, so "visible" really means visible.
  if (isLoading) {
    return (
      <div className="card weather">
        <div className="weather-side"><span className="weather-ico">🌡️</span><div className="cond">{t('weather.title')}</div></div>
        <div className="weather-temp muted">…</div>
      </div>
    )
  }
  if (!data) {
    return (
      <div className="card weather">
        <div className="weather-side"><span className="weather-ico">🌐</span><div className="cond">{t('weather.title')}</div></div>
        <div className="weather-unavail">{t('weather.unavailable')}</div>
      </div>
    )
  }

  const c = condition(data.code)
  return (
    <div className="card weather">
      <div className="weather-main">
        <span className="weather-ico">{c.icon}</span>
        <div className="weather-temp">{Math.round(data.tempC)}°</div>
        <div className="weather-meta">
          <div className="cond">{t(`weather.${c.key}`)}</div>
          <div className="hilo">↑{Math.round(data.highC)}° ↓{Math.round(data.lowC)}°</div>
        </div>
      </div>
    </div>
  )
}
