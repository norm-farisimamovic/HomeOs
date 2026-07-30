/**
 * The wire format for a picked-options answer is a comma-separated index list ("0,2"); an unanswered
 * question is the empty string. Kept in one place because the runner and the mark sheet must agree on it.
 */

/** Reads "0,2" into the picked option indices. An empty/blank answer means nothing is picked. */
export function parsePicked(given: string): number[] {
  return given
    .split(',')
    // `''.split(',')` yields `['']`, and `Number('')` is 0 — without this filter an unanswered
    // question would read as "option 0 is selected".
    .map((part) => part.trim())
    .filter((part) => part.length > 0)
    .map(Number)
    .filter((n) => Number.isInteger(n) && n >= 0)
}

/** Writes picked option indices back to the wire format, in ascending order. */
export function formatPicked(picked: number[]): string {
  return [...picked].sort((a, b) => a - b).join(',')
}

/** Whether an answer counts as given (a picked option, or some written text). */
export function isAnswered(given: string | undefined): boolean {
  return (given ?? '').trim().length > 0
}
