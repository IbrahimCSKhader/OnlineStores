# Auth System AI Prompt

Use the following prompt with any AI agent that needs to understand the backend auth design:

```text
You are working against the onlineStore backend.

Your job is to respect the auth boundaries exactly as implemented in the backend.
Do not merge platform auth with storefront customer auth, and do not invent shortcut flows.

High-level auth architecture:
1. There are two separate auth domains:
   - Platform auth for `SuperAdmin` and `StoreOwner`
   - Storefront customer auth for `StoreCustomer`
2. Platform accounts are stored in `Users`.
3. Storefront shoppers are stored in `StoreCustomers` only.
4. A store owner's email must never be used as a `StoreCustomer` email inside that same store.
5. If an email matches the owner email of the target store, store-customer auth must be rejected for that store.

Platform auth rules:
1. Use `/api/Auth/*` for `SuperAdmin` and `StoreOwner`.
2. `POST /api/Auth/register` is not for storefront customers.
3. `POST /api/Auth/login` is platform-only.
4. `POST /api/Auth/create-owner` creates a `StoreOwner` in `Users`.
5. Creating an owner must never create a `StoreCustomer`.
6. Platform JWTs represent platform users only.

Store customer auth rules:
1. Use `/api/store-customer-auth/*` for storefront customer registration, login, verification, password reset, and customer password setup.
2. Store-customer JWTs carry store-scoped claims such as:
   - `store_customer_id`
   - `store_id`
   - `account_type = StoreCustomer`
   - `is_guest`
3. Customer-protected routes must use the store-customer token only.
4. If the token store does not match the route/body `storeId`, expect `403 Forbidden`.
5. Store-customer auth must reject any email that belongs to the owner of that same store.

Google storefront login:
1. Google login that starts from a store is always a storefront-customer flow.
2. Start with `GET /api/Auth/google?storeId={storeId}` or `GET /api/Auth/google?storeSlug={storeSlug}`.
3. Backend callback is `GET /api/Auth/google-callback`.
4. On success, backend redirects to the frontend success URL and sends the data in the URL hash fragment, not query params.
5. Success fragment contains:
   - `token`
   - `email`
   - `firstName`
   - `lastName`
6. That token is a store-customer token and must be stored in the storefront customer auth context.
7. If the Google email matches the owner email of that store, backend must reject the storefront customer login.

Store ownership rules:
1. Stores belong to `StoreOwner` accounts through `Store.OwnerId`.
2. `POST /api/Store` is `SuperAdmin` only.
3. The request must include an `OwnerId`.
4. Backend validates that `OwnerId` belongs to an active `StoreOwner`.
5. Backend assigns the store to the provided `OwnerId`, not to the authenticated `SuperAdmin`.

Backoffice/customer-management rules:
1. `/api/CustomerStore/*` is for platform/backoffice use only.
2. Use a platform token only.
3. `SuperAdmin` can manage all stores.
4. `StoreOwner` can manage customers only for stores they own.
5. Customer create/update flows must reject an email if it matches the owner email of that store.

Frontend/session separation:
1. Admin/backoffice session and storefront customer session are different auth contexts.
2. Never reuse the platform token as a store-customer token.
3. Never assume a store-customer token can access admin endpoints.
4. If one frontend handles both areas, use separate storage keys.

Operational source of truth:
1. Backend controller code in `Controllers/`
2. Service-layer auth logic in `Services/Auth/`, `Services/StoreCustomerAuth/`, and `Services/CustomerStore/`
3. OpenAPI from `/openapi/v1.json` when the backend is running
4. Generated endpoint metadata in `obj/Debug/net10.0/EndpointInfo/onlineStore.json`

If backend behavior and frontend assumptions disagree, trust the backend controller and service logic first.
```
