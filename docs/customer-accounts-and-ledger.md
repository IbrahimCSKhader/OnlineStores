# Customer profiles and account ledger

## Existing implementation and integration

The API already had `StoreCustomer` (the `CustomerStores` table), store-scoped customer authentication, owner CRUD, guest checkout, order contact snapshots, and a cumulative nullable `Order.ReceivedAmount`. There is no separate manual owner order-creation flow. The React dashboard is in the sibling `onlineStoreFrontEnd/onlineStore` repository; its existing Customers section, customer detail route, and order detail route now expose the ledger.

The directory extends `StoreCustomer`. Guest profiles have `IsGuestProfile = true`, an internal unique `.invalid` email placeholder, and **no password or login capability**. These placeholders are not customer contact emails and are not shown in the new directory. Actual checkout contact details stay on the original order.

`Order.AccountCustomerId` is the owner accounting association. `StoreCustomerId` retains its existing authentication/access meaning. Guest orders keep `StoreCustomerId = null`; neither phone matching, merging, nor reassignment changes storefront access. Public checkout responses and customer order APIs do not expose the accounting association or private financial data.

Phone normalization handles separators, Arabic numerals, international `00` prefixes, and Palestinian/Israeli mobile national formats. Matches require a unique store-scoped phone candidate and a consistent full name. Names never match on their own. Missing/invalid phones, differing names, shared-phone flags, and multiple candidates create separate profiles for owner review. Full names are used conservatively to disambiguate a phone, so spelling differences can require a manual merge. Registered checkout retains its previous accounting association. A registered identity without financial history can join a matching guest profile while retaining separate credentials and order access.

## Financial rules

* Signed ledger amounts: order charges and cash refunds are positive; money received is negative. The signed sum is the account balance. Negative balance means customer credit.
* Every order contributes its current total once. Cancellation, full return (`Refunded`), deletion, reopening, and total changes append charge deltas. Fulfillment status is independent of payment status.
* Cancelling or returning an order does **not** claim that cash was refunded. Existing receipts become credit until an explicit refund is recorded.
* Payments may be allocated to one or several orders or left unallocated. Excess money remains general account credit. Allocating that credit later adds a zero-value movement and does not count the payment twice.
* An order's received amount is its net allocations, including refunds and reversals. Allocation amounts cannot exceed the outstanding charge; refunds cannot exceed receipts. A full refund of a cancelled paid order therefore restores both the account balance and order receipt to zero.
* Existing `ReceivedAmount` is imported as an opening receipt. The old cumulative receipt endpoint remains available and appends only the difference, including corrections/clearing. Repeating the same absolute value creates no extra movement.
* Payment and credit-allocation requests require a request key. Reuse with the same payload returns the original movement; reuse with different data is rejected. Database unique indexes and serializable transactions protect duplicate requests and allocation limits.
* Posted movement amounts and allocations cannot be edited/deleted through the EF async write path. Corrections append an explicit reversal referencing the original. Only one reversal per movement is permitted. Allocation/refund dependencies must be undone first when necessary.
* Owner reassignment and merging preserve monetary values, original order snapshots, login ownership, and explicit audit records. Association metadata moves to the destination account. Reassignment refuses split payments or credit/refund dependencies that would leave either account's allocations unfunded. Reverse and re-record those dependent transactions first.
* Deleting a customer through the older management endpoint deactivates customers who have accounting history instead of hiding their statements.

All new account APIs require a platform `StoreOwner` or `SuperAdmin` identity and the existing `IStoreAuthorizationService` store check. Transactions, order lookups, profile targets, merges, and allocations remain scoped to that store. Historical soft-deleted orders remain available to financial calculations.

## API

Base route: `/api/CustomerStore/store/{storeId}/accounts`.

| Method | Suffix | Operation |
| --- | --- | --- |
| GET | `?search=...&page=1` | Directory, 50 customers per page |
| POST | empty | Create profile without credentials |
| GET / PUT | `/{customerId}` | Statement / edit profile |
| POST | `/{customerId}/payments` | Receipt or explicit cash refund |
| POST | `/{customerId}/allocate-credit` | Allocate previously received money |
| POST | `/{customerId}/entries/{entryId}/reverse` | Append reversal with reason |
| POST | `/{customerId}/merge` | Merge into `targetCustomerId`, with reason |
| PUT | `/orders/{orderId}/customer` | Correct accounting association, with reason |
| POST | `/backfill` | Process up to 200 historical orders |
| GET | `/audit` | Latest 200 profile/association audit records |

Example receipt (amounts are decimal currency values):

```json
{
  "requestKey": "a-persisted-unique-client-request-key",
  "amount": 150.00,
  "paymentDate": "2026-11-04T10:00:00Z",
  "method": "نقدي",
  "notes": "دفعة من العميل",
  "isRefund": false,
  "allocations": [
    { "orderId": "00000000-0000-0000-0000-000000000101", "amount": 50.00 },
    { "orderId": "00000000-0000-0000-0000-000000000125", "amount": 100.00 }
  ]
}
```

An empty allocations list records a general payment. The credit-allocation endpoint uses the same request shape but takes its amount from the allocations. The statement includes each allocation and reference; order details show only relevant transactions, including split payments. The frontend retains the request key for retries, disables pending submissions, and supplies a new key for a new payment form.

## Rollout and historical records

1. Review and apply `20261104212201_AddCustomerAccountLedger` to the intended database through the existing deployment process. The migration is additive; it does not rewrite historical orders or make speculative customer merges.
2. Deploy the API and sibling React frontend together. No hosted database changes or deployment were performed during implementation.
3. In **العملاء وحساباتهم**, click **ربط الطلبات السابقة**. Each request processes at most 200 orders atomically. Repeat until `remaining` is zero. Repeating completed batches is safe. Original contact snapshots and registered order ownership remain intact. Unknown legacy received amounts remain unknown; no payment is inferred from delivery status.
4. Review shared-phone/name conflicts, merge confirmed duplicates with a reason, and correct mistaken associations from the order page. Existing received amounts are preserved as opening movements and are never treated as additional fresh receipts.

Until historical backfill finishes, the new account summaries contain only initialized orders. The order page directs owners to run backfill when its account association is absent. Newly placed orders are initialized automatically. There is no requirement for a guest to register or create a password.

The existing lifecycle has full `Refunded` status, not item-level partial returns. Record actual cash refunds separately; future item-level return workflows should update the order's chargeable total through the same lifecycle path.

## Validation

`dotnet test` exercises normalized identity, store/access boundaries, repeated guest checkout, existing receipt import, partial and multiple payments, split allocations, general/advance credit, refunds, reversals, total/status changes, merge and reassignment, immutable monetary history, and request-key replay/conflict.

Frontend: `npx vitest run --maxWorkers=2 --testTimeout=15000`, focused ESLint checks, and `npm run build`. A Playwright check with intercepted fixture APIs verifies Arabic account rendering, a 390px viewport without horizontal overflow, allocated payment submission, and manual profile creation without credentials. It does not contact the production API. Screenshots and the browser-check script are under the frontend `artifacts/` directory.

SQL Server migration execution and concurrent production-load behavior require the normal deployment/staging checks; automated financial integration tests here use SQLite. Existing dependency-audit/compiler warnings were outside this feature's scope.
