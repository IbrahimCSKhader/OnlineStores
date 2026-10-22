# Store bundle offers

A bundle contains one or more products, a positive integer quantity per product,
and one merchant-defined price for the whole package. Uses the existing Offers /
OfferItems tables (`BundleFixedPrice = 1`) and adds nullable package snapshots to
CartItems and OrderItems, keeping existing carts and orders compatible.

## API

- `POST /api/offer`: create a package (store owner / super admin; existing plan entitlement applies).
- `PUT /api/offer/{id}`: edit name, contents, fixed price or active status.
- `DELETE /api/offer/{id}`: soft-delete; historical orders remain intact.
- `GET /api/offer/store/{storeId}`: management listing when authorized.
- `GET /api/offer/store/{storeId}/active`: public storefront listing, valid dates and active products only.
- `POST /api/cart/bundle`: atomically add every component. Body:

```json
{
  "storeId": "STORE_GUID",
  "offerId": "OFFER_GUID",
  "quantity": 2,
  "variants": { "PRODUCT_GUID": "VARIANT_GUID" }
}
```

`variants` is optional; each product uses its default active variant if omitted.
The scoped customer token must match the store. Catalog stores cannot purchase.
All components are validated before persistence. Invalid/foreign/deleted products,
unavailable variants, invalid quantities and insufficient tracked stock reject the
whole operation. Existing standalone and package quantities share stock limits.

Cart components include BundleOfferId, BundleGroupId, BundleName, BundleQuantity,
BundleItemQuantity, BundlePrice and BundleLineTotal. Updating any component through
the existing cart item endpoint changes the number of packages; removing any
component removes its entire package group. Different packages and standalone
purchases of the same product remain separate.

## Pricing and order history

- Fixed package totals are allocated across component lines in exact cents;
  rounding remainders are assigned to the last line. TotalPrice is authoritative,
  because displayed per-unit prices may round when a component quantity is > 1.
- A package is explicitly selected. Automatic percentage/fixed discounts apply to
  standalone products; fixed bundle offers do not automatically match standalone
  products and cannot discount the same package twice.
- Customer wholesale discounts do not alter a selected fixed package price.
  Existing coupon and delivery calculations continue after package pricing.
- Checkout revalidates the active offer, its current contents, current price,
  product/variant availability and aggregate stock. Changed contents or a disabled,
  expired or deleted offer require removal/re-addition. Invalid packages remain
  removable from the cart.
- Orders retain package identity/name, per-package price, package quantity and
  component quantities independently of subsequent offer edits/deletion.
  Order list responses include ContainsBundles.

## Inventory

Follows the existing order lifecycle: pending orders do not reserve stock;
confirmation (or another inventory-reserving status) subtracts each tracked
component's quantity. Repeated confirmation does not subtract twice. Cancellation
restores reserved stock. Untracked products never change stock. Checkout and
status transitions use serializable transactions to protect shared stock.

## Migration and deployment

Deploy the backend migration before the updated frontend. The application already
calls `Database.MigrateAsync()` on startup. Migration
`20261022213907_AddBundleCartAndOrderSnapshots` adds only nullable snapshot columns;
no existing rows or merchant catalog data are rewritten. The checked-in model
snapshot and generated migration designer are included. This change does not
publish/deploy the running production application.

## Verification

```powershell
$env:BUNDLE_TEST_SQLSERVER='Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
$env:DELIVERY_TEST_SQLSERVER=$env:BUNDLE_TEST_SQLSERVER
dotnet test onlineStore.Tests/onlineStore.Tests.csproj
```

Integration tests create and delete their own randomly named databases and never
use the application's configured database. They cover migration up/down, exact
allocation, multi-product packages, stock limits, package quantity/removal,
standalone coexistence, wholesale pricing, order snapshots, confirmation and
cancellation, expired/deleted offers and ownership isolation.
