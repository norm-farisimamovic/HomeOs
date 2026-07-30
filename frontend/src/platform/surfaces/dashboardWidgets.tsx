import type { ComponentType } from 'react'
import { ShoppingWidget } from '@/apps/shopping/ShoppingWidget'
import { WeatherWidget } from '@/app/components/WeatherWidget'
import { ScoreboardCard } from '@/app/components/ScoreboardCard'
import { HouseholdWidget } from '@/app/components/HouseholdWidget'
import { SpendingChartWidget } from '@/app/components/SpendingChartWidget'
import { ExamsWidget } from '@/apps/exams/ExamsWidget'

/** A card shown in the dashboard's widget column — reorderable/hideable by the member, hidden when its app is off. */
export interface DashboardWidget {
  id: string
  /** i18n key for the widget's name, shown in the "edit layout" list. */
  nameKey: string
  /** The app this widget belongs to; when set, the dashboard hides it if the household disabled that app. */
  appId?: string
  Component: ComponentType
}

/**
 * The dashboard-widget extension point. The dashboard renders these (filtered by enabled apps, in the
 * member's saved order, minus hidden ones) without knowing any individual widget — a platform card or an
 * app self-surfaces by adding one entry here, the same way search/nav pick apps up from the registry.
 */
export const dashboardWidgets: DashboardWidget[] = [
  { id: 'weather', nameKey: 'dashboard.widgets.weather', Component: WeatherWidget },
  { id: 'spending', nameKey: 'dashboard.widgets.spending', appId: 'finance', Component: SpendingChartWidget },
  { id: 'scoreboard', nameKey: 'scoreboard.title', Component: ScoreboardCard },
  { id: 'household', nameKey: 'dashboard.household', Component: HouseholdWidget },
  { id: 'shopping', nameKey: 'nav.shopping', appId: 'shopping', Component: ShoppingWidget },
  { id: 'exams', nameKey: 'nav.exams', appId: 'exams', Component: ExamsWidget },
]
