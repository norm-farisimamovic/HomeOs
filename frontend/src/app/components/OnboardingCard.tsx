import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { CheckSquare, Sparkles, Store, UserPlus, Wand2 } from 'lucide-react'
import { seedExamples } from '@/platform/onboarding/seed'
import { toast } from '@/platform/ui/toastStore'

/** Shown on the dashboard only for a brand-new (empty) household: first steps + a one-click sample-data seed. */
export function OnboardingCard() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [seeding, setSeeding] = useState(false)

  const onSeed = async () => {
    setSeeding(true)
    try {
      await seedExamples({
        task1: t('onboarding.seed.task1'),
        task2: t('onboarding.seed.task2'),
        note: t('onboarding.seed.note'),
        reminder: t('onboarding.seed.reminder'),
        shoppingList: t('onboarding.seed.shoppingList'),
        shoppingItems: [t('onboarding.seed.item1'), t('onboarding.seed.item2'), t('onboarding.seed.item3')],
      }, qc)
      toast.success(t('onboarding.seedDone'))
    } catch {
      toast.error(t('common.error'))
    } finally {
      setSeeding(false)
    }
  }

  return (
    <div className="card onboard">
      <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div className="onboard-head">
          <span className="onboard-ic"><Sparkles size={18} /></span>
          <div>
            <h3>{t('onboarding.title')}</h3>
            <p className="hint" style={{ margin: 0 }}>{t('onboarding.sub')}</p>
          </div>
        </div>
        <div className="onboard-steps">
          <Link className="onboard-step" to="/tasks"><CheckSquare size={15} /><span>{t('onboarding.step.task')}</span></Link>
          <Link className="onboard-step" to="/shopping"><Store size={15} /><span>{t('onboarding.step.shopping')}</span></Link>
          <Link className="onboard-step" to="/household"><UserPlus size={15} /><span>{t('onboarding.step.invite')}</span></Link>
        </div>
        <button className="btn primary" type="button" onClick={() => void onSeed()} disabled={seeding} style={{ alignSelf: 'flex-start' }}>
          <Wand2 size={15} />{seeding ? t('common.loading') : t('onboarding.loadExamples')}
        </button>
      </div>
    </div>
  )
}
