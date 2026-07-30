import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpen, Search } from 'lucide-react'
import { ArticleBody } from './ArticleLink'
import { useLawText, useLaws } from './hooks'

/**
 * The laws themselves, article by article, as a plain reading page — so anything a question cites can be
 * read in full. One law at a time, searchable, loaded 25 articles at a time.
 */
export function LawsPanel({ law, jumpTo, onLawChange }: {
  /** Which law is open. */
  law: string
  /** Article key to scroll to once it is on screen (set when arriving from an article popup). */
  jumpTo?: string | null
  onLawChange: (code: string) => void
}) {
  const { t } = useTranslation()
  const { data: laws } = useLaws()
  const [query, setQuery] = useState('')
  const { data, isLoading, hasNextPage, fetchNextPage, isFetchingNextPage } = useLawText(law, query)
  const jumped = useRef<string | null>(null)

  const articles = data?.pages.flatMap((p) => p.articles) ?? []
  const head = data?.pages[0]

  // Arriving from "open the whole law", walk forward through the pages until the article is loaded, then
  // scroll to it. `jumped` keeps this to one attempt per request so it can't fight the user's scrolling.
  useEffect(() => {
    if (!jumpTo || jumped.current === jumpTo) return
    const target = document.getElementById(`article-${jumpTo}`)
    if (target) {
      jumped.current = jumpTo
      target.scrollIntoView({ block: 'start', behavior: 'smooth' })
    } else if (hasNextPage && !isFetchingNextPage) {
      void fetchNextPage()
    }
  }, [jumpTo, articles.length, hasNextPage, isFetchingNextPage, fetchNextPage])

  return (
    <>
      <div className="card">
        <div className="card-h">
          <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} /><h3>{t('exams.laws.title')}</h3></div>
          {head && <span className="chip">{query ? `${head.matchCount}/${head.articleCount}` : `${head.articleCount}`}</span>}
        </div>
        <div className="card-b">
          <div className="seg wrap law-tabs">
            {(laws ?? []).map((l) => (
              <button key={l.code} type="button" className={law === l.code ? 'on' : ''} onClick={() => onLawChange(l.code)}>
                {l.shortTitle}
              </button>
            ))}
          </div>
          {head && (
            <div className="law-head">
              <div className="ttl">{head.title}</div>
              <div className="hint">{head.gazette} · {t('exams.laws.articles', { n: head.articleCount })}</div>
            </div>
          )}
          <label className="exam-search">
            <Search size={14} />
            <input className="inp sm" value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('exams.laws.searchPlaceholder')} aria-label={t('exams.laws.searchPlaceholder')} />
          </label>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && articles.length === 0 && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-exams)' }}><BookOpen size={20} /></span>
          <h4>{t('exams.laws.emptyTitle')}</h4>
          <p>{t('exams.laws.emptySub')}</p>
        </div></div>
      )}

      {!isLoading && articles.length > 0 && (
        <div className="card">
          <div className="card-b flush">
            {articles.map((a) => (
              <article className={`law-article${jumpTo === a.key ? ' target' : ''}`} id={`article-${a.key}`} key={a.key}>
                <header>
                  <h4>{a.label}</h4>
                  {a.title && <span className="ttl">{a.title}</span>}
                </header>
                <ArticleBody article={a} />
              </article>
            ))}
          </div>
          {hasNextPage && (
            <div className="modal-f exam-more">
              <button className="btn" type="button" onClick={() => void fetchNextPage()} disabled={isFetchingNextPage}>
                {isFetchingNextPage ? t('common.loading') : t('exams.laws.more')}
              </button>
            </div>
          )}
        </div>
      )}
    </>
  )
}
