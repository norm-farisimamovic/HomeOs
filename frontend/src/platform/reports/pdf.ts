import { jsPDF } from 'jspdf'
import autoTable from 'jspdf-autotable'
import { api } from '@/platform/api/client'
import { formatMoney } from '@/platform/money/api'

type T = (key: string, opts?: Record<string, unknown>) => string
interface Opts { t: T; locale: string; household: string }

const BRAND: [number, number, number] = [36, 104, 90] // var(--brand) #24685A

// Shared branded header; returns the Y to start content at.
function header(doc: jsPDF, title: string, household: string, locale: string): number {
  const w = doc.internal.pageSize.getWidth()
  doc.setFillColor(...BRAND)
  doc.rect(0, 0, w, 78, 'F')
  doc.setTextColor(255, 255, 255)
  doc.setFont('helvetica', 'bold'); doc.setFontSize(18)
  doc.text('Home OS', 40, 34)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(12)
  doc.text(title, 40, 56)
  doc.setFontSize(9)
  const when = new Date().toLocaleString(locale, { dateStyle: 'medium', timeStyle: 'short' })
  doc.text(`${household}  ·  ${when}`, w - 40, 40, { align: 'right' })
  doc.setTextColor(20, 26, 22)
  return 104
}

function finalY(doc: jsPDF, fallback: number): number {
  const y = (doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY
  return y ?? fallback
}

function save(doc: jsPDF, name: string) {
  doc.save(`homeos-${name}-${new Date().toISOString().slice(0, 10)}.pdf`)
}

interface FinanceSummary { currency: string; income: number; spent: number; balance: number; dueSoonCount: number; dueSoonAmount: number; byCategory: { category: string; amount: number }[] }
interface FinTx { kind: string; amount: number; currency: string; category: string; occurredOn: string; paidByName: string | null }
interface FinBill { name: string; amount: number; currency: string; nextDue: string; whoPaysName: string | null }

/** Household finance report: KPIs, spending by category, transactions and upcoming bills. */
export async function downloadFinancePdf({ t, locale, household }: Opts): Promise<void> {
  const [summary, txs, bills] = await Promise.all([
    api.get<FinanceSummary>('/api/finance/summary'),
    api.get<FinTx[]>('/api/finance/transactions'),
    api.get<FinBill[]>('/api/finance/bills'),
  ])
  const cur = summary.currency
  const m = (n: number, c = cur) => formatMoney(n, c, locale)

  const doc = new jsPDF({ unit: 'pt', format: 'a4' })
  let y = header(doc, t('reports.finance.title'), household, locale)

  autoTable(doc, {
    startY: y,
    head: [[t('finance.income'), t('finance.spent'), t('finance.balance'), t('finance.dueSoon', { n: summary.dueSoonCount })]],
    body: [[m(summary.income), m(summary.spent), m(summary.balance), m(summary.dueSoonAmount)]],
    theme: 'grid', headStyles: { fillColor: BRAND }, styles: { fontSize: 10, halign: 'center' },
  })
  y = finalY(doc, y) + 22

  if (summary.byCategory?.length) {
    doc.setFont('helvetica', 'bold'); doc.setFontSize(12); doc.text(t('reports.finance.byCategory'), 40, y)
    autoTable(doc, {
      startY: y + 8,
      head: [[t('reports.category'), t('reports.amount')]],
      body: [...summary.byCategory].sort((a, b) => b.amount - a.amount).map((c) => [c.category, m(c.amount)]),
      theme: 'striped', headStyles: { fillColor: BRAND }, styles: { fontSize: 10 }, columnStyles: { 1: { halign: 'right' } },
    })
    y = finalY(doc, y) + 22
  }

  doc.setFont('helvetica', 'bold'); doc.setFontSize(12); doc.text(t('reports.finance.transactions'), 40, y)
  autoTable(doc, {
    startY: y + 8,
    head: [[t('reports.date'), t('reports.category'), t('reports.type'), t('reports.who'), t('reports.amount')]],
    body: txs.map((x) => [x.occurredOn, x.category, x.kind === 'Income' ? t('finance.income') : t('finance.spent'), x.paidByName ?? '—', m(x.amount, x.currency)]),
    theme: 'striped', headStyles: { fillColor: BRAND }, styles: { fontSize: 9 }, columnStyles: { 4: { halign: 'right' } },
  })
  y = finalY(doc, y) + 22

  if (bills?.length) {
    doc.setFont('helvetica', 'bold'); doc.setFontSize(12); doc.text(t('reports.finance.bills'), 40, y)
    autoTable(doc, {
      startY: y + 8,
      head: [[t('reports.name'), t('reports.due'), t('reports.who'), t('reports.amount')]],
      body: bills.map((b) => [b.name, b.nextDue, b.whoPaysName ?? '—', m(b.amount, b.currency)]),
      theme: 'striped', headStyles: { fillColor: BRAND }, styles: { fontSize: 9 }, columnStyles: { 3: { halign: 'right' } },
    })
  }

  save(doc, 'finansije')
}

interface Task { title: string; assigneeName: string | null; dueDate: string | null; priority: string; isDone: boolean; status: string }

/** Household tasks report: everything open + done, with assignee, due date, priority and status. */
export async function downloadTasksPdf({ t, locale, household }: Opts): Promise<void> {
  const tasks = await api.get<Task[]>('/api/tasks')
  const doc = new jsPDF({ unit: 'pt', format: 'a4' })
  const y = header(doc, t('reports.tasks.title'), household, locale)

  const prio = (p: string) => t(`tasks.priority.${p.toLowerCase()}`)
  const sorted = [...tasks].sort((a, b) => Number(a.isDone) - Number(b.isDone) || (a.dueDate ?? '').localeCompare(b.dueDate ?? ''))

  autoTable(doc, {
    startY: y,
    head: [[t('reports.task'), t('reports.who'), t('reports.due'), t('reports.priority'), t('reports.status')]],
    body: sorted.map((x) => [
      x.title, x.assigneeName ?? '—', x.dueDate ?? '—', prio(x.priority),
      x.isDone ? t('reports.done') : t(`kanban.col.${x.status.toLowerCase()}`),
    ]),
    theme: 'striped', headStyles: { fillColor: BRAND }, styles: { fontSize: 9 },
  })

  save(doc, 'zadaci')
}
