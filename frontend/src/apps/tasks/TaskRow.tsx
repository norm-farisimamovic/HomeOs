import { useTranslation } from 'react-i18next'
import { Check, Flag, ListTree, Pencil, Plus, Repeat, Trash2 } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { confirm } from '@/platform/ui/confirmStore'
import type { Task } from './api'
import { useDeleteTask, useToggleTask } from './hooks'

function formatDue(iso: string, locale: string): string {
  const d = new Date(`${iso}T00:00:00`)
  return d.toLocaleDateString(locale === 'bs' ? 'bs-BA' : 'en-GB', { day: 'numeric', month: 'short' })
}

/** A single task row — checkbox + title + meta + assignee + actions. Reused on the dashboard and Tasks page. */
export function TaskRow({ task, onEdit, onAddSub, subtasks, nested }: {
  task: Task
  onEdit?: (task: Task) => void
  onAddSub?: (parent: Task) => void
  subtasks?: Task[]
  nested?: boolean
}) {
  const { t, i18n } = useTranslation()
  const toggle = useToggleTask()
  const del = useDeleteTask()

  // Marking done is a completion → confirm first. Reopening is direct.
  const onToggle = async () => {
    if (!task.isDone) {
      const ok = await confirm({
        title: t('tasks.confirmComplete.title'),
        message: t('tasks.confirmComplete.message', { title: task.title }),
        confirmLabel: t('common.confirm'),
      })
      if (!ok) return
    }
    toggle.mutate(task.id)
  }

  const onDelete = async () => {
    const ok = await confirm({
      title: t('tasks.confirmDelete.title'),
      message: t('tasks.confirmDelete.message', { title: task.title }),
      confirmLabel: t('common.delete'),
      danger: true,
    })
    if (ok) del.mutate(task.id)
  }

  return (
    <>
      <div className={`row-item task-row${task.isDone ? ' done' : ''}${task.priority === 'High' && !task.isDone ? ' pri-high' : ''}${nested ? ' subtask' : ''}`}>
        <label className="cb">
          <input type="checkbox" checked={task.isDone} onChange={() => void onToggle()} aria-label={t('tasks.markDone')} />
          <span className="box"><Check size={12} /></span>
        </label>

        <div className="body">
          <div className="ttl">
            {task.title}
            {task.priority === 'High' && !task.isDone && (
              <span className="chip warn"><Flag size={12} className="ic" />{t('tasks.priority.high')}</span>
            )}
            {task.subtaskTotal > 0 && (
              <span className="chip"><ListTree size={11} />{task.subtaskDone}/{task.subtaskTotal}</span>
            )}
          </div>
          <div className="meta">
            {task.dueDate && (
              <span className={`chip due-chip${task.isOverdue ? ' danger' : ''}`}>
                {formatDue(task.dueDate, i18n.resolvedLanguage ?? 'en')}
              </span>
            )}
            {task.recurrence !== 'None' && <span className="chip repeat"><Repeat size={11} />{t(`recurrence.${task.recurrence}`)}</span>}
            {task.tags.map((tag) => <span key={tag} className="chip">{tag}</span>)}
          </div>
        </div>

        <div className="end">
          {task.assigneeName && <Avatar name={task.assigneeName} memberId={task.assigneeId} size="xs" />}
          <div className="acts">
            {onAddSub && !nested && task.canEdit && (
              <button className="btn ghost icon sm" type="button" onClick={() => onAddSub(task)} aria-label={t('tasks.newSubtask')} title={t('tasks.newSubtask')}>
                <Plus size={14} />
              </button>
            )}
            {onEdit && task.canEdit && (
              <button className="btn ghost icon sm" type="button" onClick={() => onEdit(task)} aria-label={t('common.edit')}>
                <Pencil size={14} />
              </button>
            )}
            {task.canDelete && (
              <button className="btn ghost icon sm danger" type="button" onClick={() => void onDelete()} aria-label={t('tasks.delete')}>
                <Trash2 size={14} />
              </button>
            )}
          </div>
        </div>
      </div>

      {(subtasks ?? []).map((child) => (
        <TaskRow key={child.id} task={child} onEdit={onEdit} nested />
      ))}
    </>
  )
}
