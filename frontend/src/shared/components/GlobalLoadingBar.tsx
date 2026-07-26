import { useIsFetching, useIsMutating } from '@tanstack/react-query'

/**
 * A thin animated bar pinned to the top of the app that appears whenever any query is fetching or any
 * mutation is running — so every data load (GET) has a visible loading indicator, app-wide.
 */
export function GlobalLoadingBar() {
  const active = useIsFetching() + useIsMutating() > 0
  return <div className={`global-loader${active ? ' on' : ''}`} role="progressbar" aria-hidden={!active} aria-label="Loading" />
}
