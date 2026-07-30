import { api } from '@/platform/api/client'

/** How a question is answered — and therefore how it is marked. */
export type QuestionType = 'single' | 'multi' | 'open'

/** A law covered by the question bank, with the counts the setup screen needs. */
export interface LawInfo {
  code: string
  title: string
  shortTitle: string
  gazette: string
  total: number
  choice: number
  open: number
}

/** The laws on offer plus how the exam will be marked. */
export interface Subjects {
  laws: LawInfo[]
  totalQuestions: number
  /** True when an AI examiner marks written answers (otherwise key terms are used). */
  aiGrading: boolean
  passPercent: number
}

/** One question on the paper. Mark-sheet fields stay empty until the attempt is finished. */
export interface ExamQuestion {
  id: string
  ordinal: number
  law: string
  lawShort: string
  article?: string | null
  topic?: string | null
  type: QuestionType
  text: string
  options: string[]
  maxPoints: number
  given: string
  points?: number | null
  correct?: boolean | null
  feedback?: string | null
  aiGraded: boolean
  correctOptions: number[]
  modelAnswer?: string | null
  explanation?: string | null
}

/** An attempt with its paper; the score fills in once it is marked. */
export interface ExamAttempt {
  id: string
  laws: string
  mode: string
  startedAtUtc: string
  finishedAtUtc?: string | null
  finished: boolean
  earnedPoints: number
  maxPoints: number
  percent: number
  grade: number
  passed: boolean
  questions: ExamQuestion[]
}

/** A past attempt as it appears in the history list. */
export interface AttemptSummary {
  id: string
  laws: string
  mode: string
  startedAtUtc: string
  finishedAtUtc?: string | null
  questionCount: number
  earnedPoints: number
  maxPoints: number
  percent: number
  grade: number
  passed: boolean
}

/** A question with its answer — study mode shows everything. */
export interface StudyQuestion {
  id: string
  law: string
  lawShort: string
  article?: string | null
  topic?: string | null
  type: QuestionType
  text: string
  options: string[]
  correct: number[]
  answer?: string | null
  explanation?: string | null
}

/** A page of study-mode questions. */
export interface StudyPage {
  total: number
  questions: StudyQuestion[]
}

/** Options for drawing a new paper. */
export interface StartExam {
  laws: string[]
  count: number
  mode: 'mixed' | 'choice' | 'open'
}

/** Query keys — a contract other surfaces (e.g. the dashboard widget) reuse. */
export const examKeys = {
  subjects: ['exams', 'subjects'] as const,
  attempts: ['exams', 'attempts'] as const,
  attempt: (id: string) => ['exams', 'attempt', id] as const,
  study: (laws: string[], q: string) => ['exams', 'study', laws.join(','), q] as const,
}

export const examsApi = {
  subjects: () => api.get<Subjects>('/api/exams/subjects'),
  attempts: () => api.get<AttemptSummary[]>('/api/exams/attempts'),
  attempt: (id: string) => api.get<ExamAttempt>(`/api/exams/attempts/${id}`),
  start: (body: StartExam) => api.post<ExamAttempt>('/api/exams/attempts', body),
  saveAnswer: (attemptId: string, questionId: string, answer: string) =>
    api.put<void>(`/api/exams/attempts/${attemptId}/answers/${encodeURIComponent(questionId)}`, { answer }),
  finish: (attemptId: string) => api.post<ExamAttempt>(`/api/exams/attempts/${attemptId}/finish`),
  remove: (attemptId: string) => api.del<void>(`/api/exams/attempts/${attemptId}`),
  /** One page of study questions; `laws` empty means the whole bank. */
  study: (laws: string[], q: string, skip: number, take: number) => {
    const params = new URLSearchParams()
    if (laws.length > 0) params.set('law', laws.join(','))
    if (q) params.set('q', q)
    params.set('skip', String(skip))
    params.set('take', String(take))
    return api.get<StudyPage>(`/api/exams/study?${params.toString()}`)
  },
}
