import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Flag, Plus, X } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { confirm } from '@/platform/ui/confirmStore'
import type { Task } from '@/apps/tasks/api'
import { useBoards, useCreateBoard, useDeleteBoard, useSetTaskStatus, useTasks } from '@/apps/tasks/hooks'
import { TaskModal } from '@/apps/tasks/TaskModal'

type Col = 'Todo' | 'Doing' | 'Done'
const COLUMNS: { key: Col; hue: string }[] = [
  { key: 'Todo', hue: 'var(--text-3)' },
  { key: 'Doing', hue: 'var(--m-tasks)' },
  { key: 'Done', hue: 'var(--ok)' },
]

function formatDue(iso: string, locale: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(locale === 'bs' ? 'bs-BA' : 'en-GB', { day: 'numeric', month: 'short' })
}

export function KanbanPage() {
  const { t, i18n } = useTranslation()
  const { data: tasks, isLoading } = useTasks()
  const { data: boards = [] } = useBoards()
  const createBoard = useCreateBoard()
  const deleteBoard = useDeleteBoard()
  const move = useSetTaskStatus()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Task | undefined>(undefined)
  const [dragOver, setDragOver] = useState<Col | null>(null)
  // 'all' shows every board; 'general' = tasks with no board; otherwise a board id.
  const [active, setActive] = useState<string>('all')
  const [newBoard, setNewBoard] = useState<string | null>(null)

  const byCol = useMemo(() => {
    const map: Record<Col, Task[]> = { Todo: [], Doing: [], Done: [] }
    // The board shows top-level tasks only; sub-tasks live inside their parent on the Tasks page.
    for (const task of tasks ?? []) {
      if (task.parentId) continue
      if (active === 'general' && task.boardId) continue
      if (active !== 'all' && active !== 'general' && task.boardId !== active) continue
      map[task.status].push(task)
    }
    return map
  }, [tasks, active])

  // A new task lands on the board you're viewing (null for All/General).
  const boardForNew = active === 'all' || active === 'general' ? undefined : active
  const openNew = () => { setEditing(undefined); setModalOpen(true) }
  const openEdit = (task: Task) => { setEditing(task); setModalOpen(true) }

  const addBoard = () => {
    const name = (newBoard ?? '').trim()
    if (!name) { setNewBoard(null); return }
    createBoard.mutate({ name }, { onSuccess: (b) => { setActive(b.id); setNewBoard(null) } })
  }

  const removeBoard = async (id: string, name: string) => {
    if (await confirm({ title: t('kanban.deleteBoardTitle'), message: t('kanban.deleteBoardMsg', { name }), confirmLabel: t('common.delete'), danger: true })) {
      deleteBoard.mutate(id)
      if (active === id) setActive('all')
    }
  }

  const drop = async (e: React.DragEvent, col: Col) => {
    e.preventDefault()
    setDragOver(null)
    const id = e.dataTransfer.getData('text/plain')
    const task = (tasks ?? []).find((x) => x.id === id)
    if (!task || task.status === col) return
    if (!task.canEdit) return
    // Moving into "Done" is completing the task → confirm (house rule).
    if (col === 'Done') {
      const ok = await confirm({ title: t('tasks.confirmComplete.title'), message: t('tasks.confirmComplete.message', { title: task.title }), confirmLabel: t('common.confirm') })
      if (!ok) return
    }
    move.mutate({ id, status: col })
  }

  return (
    <div className="wrap wide">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-boards)' }}>{t('nav.boards')}</div>
          <h1>{t('kanban.title')}</h1>
          <p className="sub">{t('kanban.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('tasks.newTask')}</button>
        </div>
      </div>

      <div className="board-tabs">
        <button type="button" className={`board-tab${active === 'all' ? ' on' : ''}`} onClick={() => setActive('all')}>{t('kanban.allBoards')}</button>
        <button type="button" className={`board-tab${active === 'general' ? ' on' : ''}`} onClick={() => setActive('general')}>{t('kanban.general')}</button>
        {boards.map((b) => (
          <span key={b.id} className={`board-tab${active === b.id ? ' on' : ''}`}>
            <button type="button" onClick={() => setActive(b.id)} style={{ ['--mc' as string]: b.color }}><i className="board-dot" style={{ background: b.color }} />{b.name}</button>
            <button type="button" className="board-x" onClick={() => void removeBoard(b.id, b.name)} aria-label={t('common.delete')}><X size={11} /></button>
          </span>
        ))}
        {newBoard === null ? (
          <button type="button" className="board-tab add" onClick={() => setNewBoard('')}><Plus size={13} />{t('kanban.newBoard')}</button>
        ) : (
          <input className="inp sm board-input" autoFocus value={newBoard} placeholder={t('kanban.boardName')}
            onChange={(e) => setNewBoard(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') addBoard(); if (e.key === 'Escape') setNewBoard(null) }}
            onBlur={addBoard} />
        )}
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && (
        <div className="kb-board">
          {COLUMNS.map(({ key, hue }) => (
            <div
              key={key}
              className={`kb-col${dragOver === key ? ' over' : ''}`}
              onDragOver={(e) => { e.preventDefault(); setDragOver(key) }}
              onDragLeave={() => setDragOver((c) => (c === key ? null : c))}
              onDrop={(e) => void drop(e, key)}
            >
              <div className="kb-col-h">
                <span className="kb-dot" style={{ background: hue }} />
                <span className="kb-title">{t(`kanban.col.${key.toLowerCase()}`)}</span>
                <span className="chip section-count">{byCol[key].length}</span>
              </div>
              <div className="kb-list">
                {byCol[key].map((task) => (
                  <div
                    key={task.id}
                    className={`kb-card${task.canEdit ? '' : ' locked'}`}
                    draggable={task.canEdit}
                    onDragStart={(e) => e.dataTransfer.setData('text/plain', task.id)}
                    onClick={() => openEdit(task)}
                    role="button"
                    tabIndex={0}
                  >
                    <div className="kb-card-t">{task.title}</div>
                    <div className="kb-card-m">
                      {task.priority === 'High' && <span className="chip warn"><Flag size={11} className="ic" />{t('tasks.priority.high')}</span>}
                      {task.dueDate && <span className={`chip due-chip${task.isOverdue ? ' danger' : ''}`}>{formatDue(task.dueDate, i18n.resolvedLanguage ?? 'en')}</span>}
                      {task.tags.map((tag) => <span key={tag} className="chip">{tag}</span>)}
                      {task.assigneeName && <span className="kb-assignee"><Avatar name={task.assigneeName} size="xs" /></span>}
                    </div>
                  </div>
                ))}
                {byCol[key].length === 0 && <div className="kb-empty">{t('kanban.empty')}</div>}
              </div>
            </div>
          ))}
        </div>
      )}

      {modalOpen && <TaskModal task={editing} boardId={editing ? undefined : boardForNew} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
