import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpen, ScrollText } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { useArticle } from './hooks'

/**
 * A question's article citation, turned into something you can read. Clicking it opens the article's full
 * official text; the popup then offers to open the whole law. Falls back to a plain chip when the citation
 * doesn't resolve to an article we hold.
 */
export function ArticleLink({ law, articleKey, citation, onOpenLaw }: {
  law: string
  articleKey?: string | null
  citation: string
  /** Called with (law, articleKey) to jump into the full law text. */
  onOpenLaw?: (law: string, articleKey: string) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  if (!articleKey) return <span className="chip">{citation}</span>

  return (
    <>
      <button type="button" className="chip link" onClick={() => setOpen(true)} title={t('exams.article.open')}>
        <ScrollText size={11} />{citation}
      </button>
      {open && (
        <ArticlePopup
          law={law}
          articleKey={articleKey}
          onClose={() => setOpen(false)}
          onOpenLaw={onOpenLaw && (() => { setOpen(false); onOpenLaw(law, articleKey) })}
        />
      )}
    </>
  )
}

/** The article's text, exactly as the official gazette version reads. */
function ArticlePopup({ law, articleKey, onClose, onOpenLaw }: {
  law: string
  articleKey: string
  onClose: () => void
  onOpenLaw?: () => void
}) {
  const { t } = useTranslation()
  const { data, isLoading, isError } = useArticle(law, articleKey)

  return (
    <Modal
      title={data ? data.article.label : t('exams.article.loading')}
      subtitle={data ? data.title : undefined}
      icon={ScrollText}
      hue="var(--m-exams)"
      onClose={onClose}
      footer={onOpenLaw && (
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={onOpenLaw}><BookOpen size={15} />{t('exams.article.openLaw')}</button>
        </>
      )}
    >
      {isLoading && <p className="hint">{t('common.loading')}</p>}
      {isError && <p className="hint">{t('exams.article.missing')}</p>}
      {data && <ArticleBody article={data.article} gazette={data.gazette} />}
    </Modal>
  )
}

/** Chapter, title and paragraphs of one article — the shared rendering for the popup and the law page. */
export function ArticleBody({ article, gazette }: { article: { label: string; title: string; chapter: string; text: string }; gazette?: string }) {
  return (
    <div className="law-article-body">
      {article.chapter && <div className="chapter">{article.chapter}</div>}
      {article.text.split('\n').map((paragraph, i) => <p key={i}>{paragraph}</p>)}
      {gazette && <div className="src">{gazette}</div>}
    </div>
  )
}
