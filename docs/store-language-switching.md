# Storefront language switching

Each store has `enableLanguageSwitching`, which defaults to `false` for both existing stores (via migration) and new stores. Store owners can change it from the dashboard overview, including catalog stores; super admins can change it in the store edit dialog.

- Create: `POST /api/Store` accepts multipart field `EnableLanguageSwitching`.
- Update: `PUT /api/Store/{id}` accepts `{ "enableLanguageSwitching": true }`. Omitting the field preserves its current value.
- Store responses expose `enableLanguageSwitching`, `defaultLanguage`, `language`, and `supportedLanguages`.
- `defaultLanguage` is the stored registration language (`A`, `E`, or `I`). `language` is the effective language for the response.
- With switching disabled, `supportedLanguages` contains only the registered language and the API ignores requested language overrides for store, category, section, and product responses.
- With switching enabled, `supportedLanguages` contains all three languages. The storefront begins in the registered language and shows a language menu in the header on desktop and mobile. Visitor choices are shared across pages within the same store for the current app session, independently of other stores. A fresh page load starts in the registered language. Old browser preferences and URL language overrides do not change that initial language.

The `AddStoreLanguageSwitching` migration adds the column with a database default of `false`. Normal application startup applies migrations automatically unless startup seeding is explicitly skipped.
