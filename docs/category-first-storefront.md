# Category-first storefront

Template `C` is named `العرض بناء على الاصناف`. It uses the existing brown Mawja palette, a compact brand header, and a responsive grid containing every category. Category links retain preview parameters. Other template defaults are unchanged. Only the fragrance-secret store was assigned C in production.

All category routes now render the shared ProductsPage with a fixed category scope. Search, price range, sort, stock, featured and discount filters, cart actions and pagination therefore use the product page components. The category selector is locked to the route; reset cannot expose another category. The route key resets/restores view state independently when moving between categories.

Migration `20261118234751_AddCategoryGridStoreTheme` extends the existing constraint. Rolling it back returns C stores to N before restoring the old constraint.

The requested fragrance-secret catalog replacement uses 117 provided photographs grouped into 76 products and 17 categories, one perfume section, quantity 30, inventory tracking disabled, and temporary prices of ILS 100–220. Product labels were checked against the full source images. Unclear branding for Thumbelina is grouped under miscellaneous perfumes instead of assigning an unsupported manufacturer. Existing catalog rows and offers were soft deleted after a local backup. Order history was preserved.

Local import manifest, photo assignments, API upload receipts, pre-import snapshot and operational scripts are retained under `output/fragrance-secret/`. These operational artifacts, credentials and deployment outputs are not source files and are not committed.

Validation: frontend build; existing catalog, language and filter tests; category scope/reset and category-grid navigation/isolation tests; 19 backend theme policy tests; public API counts and inventory values; category image HTTP responses; live desktop/mobile browser inspection of tiles and direct category product results. The frontend was deployed through its existing main-branch Render workflow and the backend assembly through the existing Web Deploy profile, preserving the previous assembly locally.
