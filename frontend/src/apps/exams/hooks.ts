import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { examKeys, examsApi, type StartExam } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

/** The laws in the bank, their question counts and how written answers will be marked. */
export function useExamSubjects() {
  // Reference data — it only changes with a new release, so keep it out of the refetch path.
  return useQuery({ queryKey: examKeys.subjects, queryFn: examsApi.subjects, staleTime: 60 * 60 * 1000 })
}

/** The member's own attempts, newest first. */
export function useExamAttempts() {
  return useQuery({ queryKey: examKeys.attempts, queryFn: examsApi.attempts })
}

/** One attempt with its paper (mark sheet included once it is finished). */
export function useExamAttempt(id: string | null) {
  return useQuery({
    queryKey: examKeys.attempt(id ?? ''),
    queryFn: () => examsApi.attempt(id!),
    enabled: !!id,
  })
}

/** How many study questions are fetched per page. */
export const STUDY_PAGE_SIZE = 40

/**
 * Study mode: the questions of the chosen laws with their answers, paged so the whole 700-question
 * bank never lands in one response (or one DOM tree).
 */
export function useStudyQuestions(laws: string[], query: string) {
  return useInfiniteQuery({
    queryKey: examKeys.study(laws, query),
    queryFn: ({ pageParam }) => examsApi.study(laws, query, pageParam, STUDY_PAGE_SIZE),
    initialPageParam: 0,
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((n, p) => n + p.questions.length, 0)
      return loaded < lastPage.total ? loaded : undefined
    },
    // Reference data: it only changes with a release, so don't refetch while revising.
    staleTime: 30 * 60 * 1000,
  })
}

/** Start / answer / finish / delete — everything that changes an attempt. */
export function useExamMutations() {
  const qc = useQueryClient()
  const refreshHistory = () => qc.invalidateQueries({ queryKey: examKeys.attempts })

  const start = useMutation({
    mutationFn: (body: StartExam) => examsApi.start(body),
    onSuccess: (attempt) => {
      qc.setQueryData(examKeys.attempt(attempt.id), attempt)
      refreshHistory()
    },
    onError: toastError,
  })

  // Answers save silently as the candidate works — a failed autosave must not interrupt the exam,
  // so it only surfaces as a toast and the local answer stays on screen.
  const saveAnswer = useMutation({
    mutationFn: (v: { attemptId: string; questionId: string; answer: string }) =>
      examsApi.saveAnswer(v.attemptId, v.questionId, v.answer),
    onError: toastError,
  })

  const finish = useMutation({
    mutationFn: (attemptId: string) => examsApi.finish(attemptId),
    onSuccess: (attempt) => {
      qc.setQueryData(examKeys.attempt(attempt.id), attempt)
      refreshHistory()
    },
    onError: toastError,
  })

  const remove = useMutation({
    mutationFn: (attemptId: string) => examsApi.remove(attemptId),
    onSuccess: refreshHistory,
    onError: toastError,
  })

  return { start, saveAnswer, finish, remove }
}

/** How many articles of a law are fetched per page. */
export const LAW_PAGE_SIZE = 25

/** The laws whose full text ships with the app. */
export function useLaws() {
  return useQuery({ queryKey: examKeys.laws, queryFn: examsApi.laws, staleTime: 60 * 60 * 1000 })
}

/**
 * One law's articles, paged. Reference data that only changes with a release, so it is cached hard —
 * scrolling a law and coming back should not refetch.
 */
export function useLawText(code: string, query: string) {
  return useInfiniteQuery({
    queryKey: examKeys.law(code, query),
    queryFn: ({ pageParam }) => examsApi.law(code, query, pageParam, LAW_PAGE_SIZE),
    initialPageParam: 0,
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((n, p) => n + p.articles.length, 0)
      return loaded < lastPage.matchCount ? loaded : undefined
    },
    enabled: !!code,
    staleTime: 60 * 60 * 1000,
  })
}

/** A single article — what the "read this article" popup shows. */
export function useArticle(code: string | null, key: string | null) {
  return useQuery({
    queryKey: examKeys.article(code ?? '', key ?? ''),
    queryFn: () => examsApi.article(code!, key!),
    enabled: !!code && !!key,
    staleTime: 60 * 60 * 1000,
  })
}
