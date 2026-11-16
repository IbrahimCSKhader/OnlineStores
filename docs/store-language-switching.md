# Storefront language switching

Each store has `enableLanguageSwitching`, which defaults to `false` for both existing stores (via migration) and new stores. Store owners can change it from the dashboard overview, including catalog stores; super admins can change it in the store edit dialog.

- Create: `POST /api/Store` accepts multipart field `EnableLanguageSwitching`.
- Update: `PUT /api/Store/{id}` accepts `{ "enableLanguageSwitching": true }`. Omitting the field preserves its current value.
- Store responses expose `enableLanguageSwitching`, `defaultLanguage`, `language`, and `supportedLanguages`.
- `defaultLanguage` is the stored registration language (`A`, `E`, or `I`). `language` is the effective language for the response.
- With switching disabled, `supportedLanguages` contains only the registered language and the API ignores requested language overrides for store, category, section, and product responses.
- With switching enabled, `supportedLanguages` contains all three languages. The storefront begins in the registered language and shows a language menu in the header on desktop and mobile. Visitor choices are shared across pages within the same store for the current app session, independently of other stores. A fresh page load starts in the registered language. Old browser preferences and URL language overrides do not change that initial language.

The `AddStoreLanguageSwitching` migration adds the column with a database default of `false`. Normal application startup applies migrations automatically unless startup seeding is explicitly skipped.

## Optional catalog translations

Category, section and product create/update endpoints accept an optional `translations` list
(`Translations[0].Language`, `Translations[0].Name`, etc. for multipart requests).
Each entry contains `language` (`A`, `E`, or `I`), `name`, `description`, and, for products,
`shortDescription`. The registered store language remains in the normal fields.

- Available only when `enableLanguageSwitching` is enabled; existing store authorization applies.
- Omission (or an empty list) preserves existing translations. Each supplied language replaces
  its translated text fields; empty/omitted text removes that field's translation and restores fallback.
- Duplicate, unknown and registered-language entries are rejected. Translations share the
  entity's database save and use the existing `StoreTranslations` table; no migration is needed.
- Catalog reads expose `translations`, `baseName`, `baseDescription`, and product
  `baseShortDescription` so editing does not copy a localized value over the original.
- The dashboard keeps its normal inputs and adds an optional language selector. Switching
  the selector preserves drafts in the other language; saving submits all edited languages.
- Deploy backend and frontend together (backend first); older servers ignore the new fields.
