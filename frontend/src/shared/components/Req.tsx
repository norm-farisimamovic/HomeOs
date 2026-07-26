import { useTranslation } from 'react-i18next'

/** Visual "required field" marker (a red asterisk) for form labels, with an accessible label. */
export function Req() {
  const { t } = useTranslation()
  return <span className="req-star" title={t('common.required')} aria-label={t('common.required')}>*</span>
}
