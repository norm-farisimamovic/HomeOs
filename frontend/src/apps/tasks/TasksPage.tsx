import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'
import type { Task } from './api'
import { useTasks } from './hooks'
import { TaskRow } from './TaskRow'
import { TaskModal } from './TaskModal'

type Bucket = 'overdue' | 'today' | 'later' | 'someday' | 'done'
const ORDER: Bucket[] = ['overdue', 'today', 'later', 'someday', 'done']
type Filter = 'all' | 'open' | 'done'

function todayIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function bucketOf(task: Task, today: string): Bucket {
  if (task.isDone) return 'done'
  if (!task.dueDate) return 'someday'
  if (task.dueDate < today) return 'overdue'
  if (task.dueDate === today) return 'today'
  return 'later'
}

export function TasksPage() {
  const { t } = useTranslation()
  const { data: tasks, isLoading, isError, refetch } = useTasks()
  const [filter, setFilter] = useState<Filter>('all')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Task | undefined>(undefined)
  const [parentFor, setParentFor] = useState<string | undefined>(undefined)

  // Sub-tasks are nested under their parent; only top-level tasks go into the date buckets.
  const childrenByParent = useMemo(() => {
    const map = new Map<string, Task[]>()
    for (const task of tasks ?? []) {
      if (!task.parentId) continue
      const list = map.get(task.parentId) ?? []
      list.push(task)
      map.set(task.parentId, list)
    }
    return map
  }, [tasks])

  const groups = useMemo(() => {
    const today = todayIso()
    const map: Record<Bucket, Task[]> = { overdue: [], today: [], later: [], someday: [], done: [] }
    for (const task of tasks ?? []) {
      if (task.parentId) continue
      if (filter === 'open' && task.isDone) continue
      if (filter === 'done' && !task.isDone) continue
      map[bucketOf(task, today)].push(task)
    }
    return map
  }, [tasks, filter])

  const openEdit = (task: Task) => { setEditing(task); setParentFor(undefined); setModalOpen(true) }
  const openNew = () => { setEditing(undefined); setParentFor(undefined); setModalOpen(true) }
  const openSub = (parent: Task) => { setEditing(undefined); setParentFor(parent.id); setModalOpen(true) }

  const hasAny = ORDER.some((b) => groups[b].length > 0)

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-tasks)' }}>{t('nav.tasks')}</div>
          <h1>{t('tasks.title')}</h1>
          <p className="sub">{t('tasks.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('tasks.newTask')}</button>
        </div>
      </div>

      <div className="toolbar" style={{ marginBottom: 14 }}>
        <div className="seg">
          {(['all', 'open', 'done'] as Filter[]).map((f) => (
            <button key={f} type="button" className={filter === f ? 'on' : undefined} onClick={() => setFilter(f)}>
              {t(`tasks.filter.${f}`)}
            </button>
          ))}
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {isError && (
        <div className="card"><div className="card-b empty">
          <p>{t('common.error')}</p>
          <button className="btn" type="button" onClick={() => void refetch()}>{t('common.retry')}</button>
        </div></div>
      )}

      {!isLoading && !isError && !hasAny && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-tasks)' }}>✳</span>
          <h4>{t('tasks.emptyTitle')}</h4>
          <p>{t('tasks.emptySub')}</p>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('tasks.newTask')}</button>
        </div></div>
      )}

      {!isLoading && !isError && ORDER.map((bucket) =>
        groups[bucket].length > 0 ? (
          <div className="card" key={bucket} style={{ marginBottom: 14 }}>
            <div className="card-h">
              <div className="t"><h3>{t(`tasks.bucket.${bucket}`)}</h3></div>
              <span className={`chip section-count${bucket === 'overdue' ? ' danger' : ''}`}>{groups[bucket].length}</span>
            </div>
            <div className="card-b flush scroll-list">
              {groups[bucket].map((task) => (
                <TaskRow key={task.id} task={task} onEdit={openEdit} onAddSub={openSub} subtasks={childrenByParent.get(task.id)} />
              ))}
            </div>
          </div>
        ) : null,
      )}

      {modalOpen && <TaskModal task={editing} parentId={parentFor} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
