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
  /** The article the citation points at, for linking into the law text; null when it names none. */
  articleKey?: string | null
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
  /** False when the answer was left out of the score (written question, no AI examiner available). */
  graded: boolean
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
  /** How many written questions were left out of the score because no AI examiner was available. */
  ungradedCount: number
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
  articleKey?: string | null
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

/** One article of a law, as it reads in the official text. */
export interface LawArticle {
  key: string
  label: string
  title: string
  chapter: string
  text: string
}

/** A law on the shelf. */
export interface LawSummary {
  code: string
  title: string
  shortTitle: string
  gazette: string
  articleCount: number
}

/** A page of a law's articles (filtered when a search term is given). */
export interface LawPage {
  code: string
  title: string
  shortTitle: string
  gazette: string
  articleCount: number
  matchCount: number
  articles: LawArticle[]
}

/** One article together with the law it belongs to. */
export interface ArticleDetail {
  code: string
  title: string
  shortTitle: string
  gazette: string
  article: LawArticle
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
  laws: ['exams', 'laws'] as const,
  law: (code: string, q: string) => ['exams', 'law', code, q] as const,
  article: (code: string, key: string) => ['exams', 'article', code, key] as const,
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

  /** The laws whose full text ships with the app. */
  laws: () => api.get<LawSummary[]>('/api/exams/laws'),

  /** One page of a law's articles; `q` narrows it to the articles that mention the term. */
  law: (code: string, q: string, skip: number, take: number) => {
    const params = new URLSearchParams()
    if (q) params.set('q', q)
    params.set('skip', String(skip))
    params.set('take', String(take))
    return api.get<LawPage>(`/api/exams/laws/${code}?${params.toString()}`)
  },

  /** A single article — what the "read this article" popup shows. */
  article: (code: string, key: string) =>
    api.get<ArticleDetail>(`/api/exams/laws/${code}/articles/${encodeURIComponent(key)}`),
}
