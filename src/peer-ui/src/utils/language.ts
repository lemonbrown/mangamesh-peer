/** Maps ISO 639-1 language codes to ISO 3166-1 alpha-2 country codes (lowercase, for flag-icons). */
const LANG_COUNTRY: Record<string, string> = {
    en: 'gb',
    ja: 'jp',
    es: 'es',
    fr: 'fr',
    de: 'de',
    pt: 'pt',
    zh: 'cn',
    ko: 'kr',
    it: 'it',
    ru: 'ru',
    ar: 'sa',
    pl: 'pl',
    nl: 'nl',
    tr: 'tr',
    id: 'id',
    vi: 'vn',
    th: 'th',
    uk: 'ua',
    cs: 'cz',
    hu: 'hu',
    ro: 'ro',
    sv: 'se',
    da: 'dk',
    fi: 'fi',
    nb: 'no',
    sk: 'sk',
    bg: 'bg',
    hr: 'hr',
    ca: 'es',
    'pt-br': 'br',
    'es-419': 'un',
};

/** Returns the ISO 3166-1 alpha-2 country code for a language code, or null if unknown. */
export function langCountryCode(code: string): string | null {
    return LANG_COUNTRY[code?.toLowerCase()] ?? null;
}
