# Per-store appearance

Super Admin: Dashboard → Stores → select a store → **تخصيص المظهر**.
The editor has independent light/dark drafts, grouped controls, per-property,
per-mode and full reset, a sample preview at desktop/mobile widths, contrast
feedback, and explicit save. Closing discards unsaved edits. The preview contains
sample products; it does not write to the store or change the dashboard theme.

## Storage and deployment

`StoreAppearance` is an optional one-to-one dependent of `Stores`, keyed by
`StoreId`. `OverridesJson` contains sparse `Light` / `Dark` dictionaries. No
default template values are copied into it. Resetting both modes removes the row.
Store deletion cascades to the dependent on physical deletion; soft-deleted
stores cannot be accessed through the appearance management endpoints.

Deploy the backend and frontend together, with the migration applied **before**
the new backend starts serving requests:

```powershell
dotnet ef database update
```

Migration: `20261026170909_AddStoreAppearance` (adds only the appearance table).
The migration was generated and checked against the EF model; the configured
remote database was not changed during implementation. Persistence/reset tests
use an isolated in-memory SQLite database.

## API

All management routes require the existing `SuperAdmin` role:

- `GET /api/Store/{storeId}/appearance` — sparse overrides, or empty dictionaries.
- `PUT /api/Store/{storeId}/appearance` — replace both sets of sparse overrides.
- `DELETE /api/Store/{storeId}/appearance` — reset both modes.
- `DELETE /api/Store/{storeId}/appearance/{light|dark}` — reset one mode.
- `DELETE /api/Store/{storeId}/appearance/{light|dark}/{property}` — reset one property.

Example request/response:

```json
{
  "light": { "primary": "#ff0000", "storeTitleFontSize": "48px" },
  "dark": { "primary": "#800080", "productTitleFontFamily": "Tahoma" }
}
```

PUT is replacement, not merge. Omit a property to return it to the current
template default. Sending an empty dictionary for one mode resets that mode.
Only six-digit hex colors, approved font families, and integer sizes of 10–96px
are accepted. Unknown fields/keys and CSS expressions are rejected. Requests
are bounded to 16 KB. Simultaneous edits follow the existing last-save-wins model.

Public store responses include `appearance` in the existing store payload for
ID, slug and domain resolution; visitors need no additional request. Existing
React Query store caching applies. Saving invalidates store caches in the editor
session; other visitors receive changes on their next store query refresh.

## Frontend resolution and extension

`buildThemeProfile` first resolves the existing template and current mode, then
applies only that mode's overrides. No overrides yields the original profile.
`AppThemeProvider` applies the resolved palette to MUI and CSS tokens; explicit
element rules are scoped to `.store-layout`. Switching modes/stores or leaving
the storefront removes prior rules and variables. The owner dashboard opts out
of appearance overrides, retaining its existing template behavior.

`src/theme/storeAppearance.js` defines labels, groups, controls, CSS tokens,
element selectors, validation, and preview resolution. To extend it, add a field
to the registry and map its token/selector, then permit the matching property in
`Utilities/StoreAppearancePolicy.cs`. No new database columns are required.
The font list uses existing/system fonts; it does not load arbitrary URLs or CSS.

Default CSS, including template-specific hardcoded styles, remains intact.
Explicit title/description, header, button, card, product-title and price settings
override the relevant existing selectors. General palette overrides also flow
through existing CSS tokens and MUI components. Contrast feedback is advisory so
the Super Admin can choose branding colors intentionally.

## Verification

- Backend tests exercise safe values, role requirements, independent stores and
  modes, persistence across contexts, slug/domain responses, reset operations,
  deleted/missing stores, and invalid input.
- Frontend tests cover every existing template's unchanged defaults, mode/store
  switching, management isolation, unsafe CSS, preview, sparse save, reset and
  failed-save recovery.
