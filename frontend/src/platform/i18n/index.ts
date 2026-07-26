import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import bs from './locales/bs.json'
import en from './locales/en.json'

/**
 * Shared i18n instance. Apps contribute their own translation namespaces later; for now the
 * platform ships `bs` (default) and `en`. The detected/chosen language is persisted and, from M1,
 * synced to the member's `PreferredCulture` so server-rendered text matches the UI.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      bs: { translation: bs },
      en: { translation: en },
    },
    fallbackLng: 'bs',
    supportedLngs: ['bs', 'en'],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'homeos.lang',
    },
  })

export default i18n
