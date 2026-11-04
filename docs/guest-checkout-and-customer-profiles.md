# Guest checkout and customer profiles

Each store has `requireCustomerLoginForCheckout` (default **true**, including
existing stores during migration). Super Admin can change it in Stores → Store
details → Edit store. The store owner can change it from their dashboard overview
under **تسجيل الزبون عند الطلب**. Changes affect only that store.

When false, visitors use their existing local cart, enter name, optional email,
phone, city, address and notes, and submit without creating an account. The server
revalidates the setting, store, products, variants, bundles, delivery location,
quantities and current prices. Guest orders use the same pricing, pending status,
merchant confirmation and inventory workflow as registered orders. No account,
password, purchase points or customer discount is created for a guest. Customer
coupons remain restricted to signed-in customers. Local carts are cleared only
after the order succeeds; WhatsApp receives the server-confirmed order summary.

`POST /api/Order/guest` is anonymous, limited to 10 requests per minute per IP
and a 64 KB body. Its payload extends regular checkout contact fields with
`items` containing product/variant IDs and quantities. Bundle rows additionally
carry bundle IDs, group IDs and component quantities; their contents and prices
are verified against the store's offer on the server. Browser prices are ignored.
Registered customers continue to use the existing authenticated `/api/Order`
route and server cart.

Guest orders have `storeCustomerId: null` and `isGuest: true`. Their contact
information lives on the order, not in the store's customer directory. Merchant
order cards and order details label them **زائر — غير مسجل في المتجر**. Customer
name/email are saved as checkout snapshots, alongside existing delivery fields.

The storefront `/market/{slug}/profile` (or `/profile` on a custom domain) is
available from the customer's menu on desktop/mobile. It updates first/last
name, phone, address and city. Email is read-only; existing verification and
password-recovery flows are retained. Profile data prefills registered checkout
without overwriting edits the customer has already made. Caches are scoped to
both store and customer identity and removed on logout.

Profile API: `GET` / `PUT
/api/store-customer-auth/store/{storeId}/profile`. The existing StoreCustomerOnly
policy and token's customer/store IDs authorize access; body input cannot select
another customer. Inactive/deleted customers and stores are rejected.

Apply the `AddGuestCheckoutAndCustomerProfiles` EF migration before deploying
the new backend, then deploy the frontend. No live store's setting was changed
as part of implementation. Existing stores require login until explicitly changed.
Rollback refuses to make the customer FK mandatory while guest orders exist,
so guest order records cannot silently be discarded or reassigned.

The cart action now reads **متابعة لتأكيد الطلب**.
