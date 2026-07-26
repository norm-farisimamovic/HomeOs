import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckSquare } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Attachments } from '@/shared/components/Attachments'
import { Req } from '@/shared/components/Req'
import { useMembers } from '@/platform/members/useMembers'
import { ApiError } from '@/platform/api/client'
import type { Task, TaskInput } from './api'
import { useBoards, useCreateTask, useUpdateTask } from './hooks'

const priorities: Array<TaskInput['priority']> = ['Low', 'Normal', 'High']

/** Create or edit a task (a sub-task when `parentId` is set; on a board when `boardId` is set). */
export function TaskModal({ task, parentId, boardId, onClose }: { task?: Task; parentId?: string; boardId?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { data: members = [] } = useMembers()
  const create = useCreateTask()
  const update = useUpdateTask()
  const editing = task !== undefined

  const [title, setTitle] = useState(task?.title ?? '')
  const [description, setDescription] = useState(task?.description ?? '')
  const [dueDate, setDueDate] = useState(task?.dueDate ?? '')
  const [assigneeId, setAssigneeId] = useState(task?.assigneeId ?? '')
  const [priority, setPriority] = useState<string>(task?.priority ?? 'Normal')
  const [visibility, setVisibility] = useState<string>(task?.visibility ?? 'Household')
  const [recurrence, setRecurrence] = useState<string>(task?.recurrence ?? 'None')
  const [board, setBoard] = useState<string>(task?.boardId ?? boardId ?? '')
  const [tags, setTags] = useState(task?.tags.join(', ') ?? '')
  const [error, setError] = useState<string | null>(null)
  const { data: boards = [] } = useBoards()

  const busy = create.isPending || update.isPending

  const submit = async () => {
    if (!title.trim()) { setError(t('tasks.titleRequired')); return }
    const input: TaskInput = {
      title: title.trim(),
      description: description.trim() || undefined,
      dueDate: dueDate || null,
      assigneeId: assigneeId || null,
      priority,
      visibility,
      recurrence,
      parentId: parentId ?? undefined,
      boardId: board || null,
      tags: tags.split(',').map((s) => s.trim()).filter(Boolean),
    }
    try {
      if (editing) await update.mutateAsync({ id: task.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <Modal
      icon={CheckSquare}
      hue="var(--m-tasks)"
      title={editing ? t('tasks.editTask') : parentId ? t('tasks.newSubtask') : t('tasks.newTask')}
      subtitle={t('tasks.modalSub')}
      onClose={onClose}
      footer={
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={() => void submit()} disabled={busy}>
            {editing ? t('common.save') : t('tasks.createTask')}
          </button>
        </>
      }
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}

      <div className="field">
        <label>{t('tasks.fTitle')} <Req /></label>
        <input className="inp" value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('tasks.fTitlePh')} autoFocus />
      </div>

      <div className="form-grid">
        <div className="field">
          <label>{t('tasks.fDue')}</label>
          <input className="inp" type="date" value={dueDate ?? ''} onChange={(e) => setDueDate(e.target.value)} />
        </div>
        <div className="field">
          <label>{t('tasks.fAssignee')}</label>
          <select className="sel" value={assigneeId ?? ''} onChange={(e) => setAssigneeId(e.target.value)}>
            <option value="">{t('tasks.anyone')}</option>
            {members.map((m) => <option key={m.id} value={m.id}>{m.displayName}</option>)}
          </select>
        </div>
      </div>

      <div className="field">
        <label>{t('tasks.fPriority')}</label>
        <div className="seg">
          {priorities.map((p) => (
            <button key={p} type="button" className={priority === p ? 'on' : undefined} onClick={() => setPriority(p!)}>
              {t(`tasks.priority.${p!.toLowerCase()}`)}
            </button>
          ))}
        </div>
      </div>

      <div className="field">
        <label>{t('tasks.fTags')}</label>
        <input className="inp" value={tags} onChange={(e) => setTags(e.target.value)} placeholder={t('tasks.fTagsPh')} />
      </div>

      <div className="form-grid">
        <div className="field">
          <label>{t('tasks.fVisibility')}</label>
          <select className="sel" value={visibility} onChange={(e) => setVisibility(e.target.value)}>
            <option value="Private">{t('tasks.visibility.private')}</option>
            <option value="Household">{t('tasks.visibility.household')}</option>
            <option value="Shared">{t('tasks.visibility.shared')}</option>
          </select>
        </div>
        <div className="field">
          <label>{t('common.repeat')}</label>
          <select className="sel" value={recurrence} onChange={(e) => setRecurrence(e.target.value)}>
            <option value="None">{t('recurrence.None')}</option>
            <option value="Daily">{t('recurrence.Daily')}</option>
            <option value="Weekly">{t('recurrence.Weekly')}</option>
            <option value="Monthly">{t('recurrence.Monthly')}</option>
            <option value="Yearly">{t('recurrence.Yearly')}</option>
          </select>
        </div>
      </div>

      <div className="field">
        <label>{t('kanban.board')}</label>
        <select className="sel" value={board} onChange={(e) => setBoard(e.target.value)}>
          <option value="">{t('kanban.general')}</option>
          {boards.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </select>
      </div>

      <div className="field">
        <label>{t('tasks.fDetails')}</label>
        <textarea className="ta" value={description} onChange={(e) => setDescription(e.target.value)} placeholder={t('tasks.fDetailsPh')} />
      </div>

      {editing && <Attachments ownerType="task" ownerId={task.id} />}
    </Modal>
  )
}
