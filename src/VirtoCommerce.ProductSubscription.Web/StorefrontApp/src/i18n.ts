import { globals, registerLocaleLoader } from "@vc-frontend/core";
import type { I18n, ILanguage } from "@vc-frontend/core";

const FALLBACK_LOCALE = "en";
const LOCALE_FILES = import.meta.glob<{ default: Record<string, unknown> }>("./locales/*.json");

async function loadFile(locale: string): Promise<Record<string, unknown>> {
  const load = LOCALE_FILES[`./locales/${locale}.json`];
  return load ? (await load()).default : {};
}

/**
 * Merges this plugin's messages for a locale, and the `en` fallback, into the storefront's i18n.
 * setLocaleMessage on a fresh object, never mergeLocaleMessage: the host's messages can be frozen.
 */
async function mergeLocale(i18n: I18n, locale: string): Promise<void> {
  const locales = locale === FALLBACK_LOCALE ? [FALLBACK_LOCALE] : [FALLBACK_LOCALE, locale];
  const messages = await Promise.all(locales.map(loadFile));
  locales.forEach((name, index) => {
    const existing = i18n.global.getLocaleMessage(name) as Record<string, unknown>;
    i18n.global.setLocaleMessage(name, { ...existing, ...messages[index] });
  });
}

/** Merges the current locale now, and again on every language switch. */
export function loadLocales(): void {
  registerLocaleLoader("plugin:product-subscription", (i18n: I18n, language: ILanguage) =>
    mergeLocale(i18n, language?.twoLetterLanguageName || FALLBACK_LOCALE),
  );
  const { locale } = globals.i18n.global;
  // i18n holds the full culture ("en-US"); the message files are two-letter, like the host's.
  const current = typeof locale === "object" ? locale.value : locale;
  void mergeLocale(globals.i18n, current?.split("-")[0] || FALLBACK_LOCALE);
}
