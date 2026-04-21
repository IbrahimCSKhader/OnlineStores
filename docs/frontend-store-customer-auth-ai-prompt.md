# Frontend AI Prompt: Store Auth for OnlineStore

Use this exact prompt with the frontend AI agent.

```text
You are the frontend integration agent for OnlineStore.

Your task:
Implement storefront authentication correctly and safely, exactly matching backend behavior.
Do not invent endpoints.
Do not auto-create accounts.
Do not merge platform auth and storefront customer auth.

Core auth model
There are 2 isolated auth domains:
1) Platform auth for SuperAdmin and StoreOwner via /api/Auth/*
2) Storefront customer auth for StoreCustomer via /api/store-customer-auth/*

Important business rules
1) StoreOwner accounts live in Users only.
2) StoreCustomer accounts live in CustomerStores only.
3) Store login page must allow both a StoreCustomer session and a StoreOwner session.
4) The frontend must route to the correct dashboard after login.
5) Never create a StoreOwner as StoreCustomer.
6) Never treat a StoreOwner token as a StoreCustomer token.
7) Never fall back to registration when store login fails.

The exact backend behavior you must follow
1) If the user logs in from a store page with StoreCustomer credentials, the backend returns a StoreCustomer response.
2) If the user logs in from a store page with StoreOwner credentials for that same store, the backend must return a platform owner response and the frontend must open the owner dashboard.
3) If the credentials are invalid, show an error and do not create any account.
4) If the token is valid but the account is not owner of the requested store, show 403-style feedback.

Google login flow for storefront
1) Start Google login from the storefront using the store-scoped auth controller.
2) Use this endpoint for the challenge:
   - GET /api/store-customer-auth/google?storeId=...&storeSlug=...&redirectTo=...
3) The backend will preserve store context through the callback and may create a StoreCustomer automatically when the Google email does not already exist as a store customer.
4) Do not send the user to registration first.
5) Do not guess session type from sessionStorage alone.
6) Classify the final session from the returned token + metadata:
   - accountType
   - roles
   - storeId
   - storeCustomerId
   - authMode
   - sessionScope
   - dashboard
7) If accountType is StoreCustomer or roles contains StoreCustomer, store it in storefrontCustomer session storage.
8) If accountType is platform or roles contains StoreOwner, store it in platform session storage.

Primary store-login endpoint
- POST /api/store-customer-auth/store/{storeId}/login

Request body
{
  "email": "string",
  "password": "string"
}

Expected response contract for the store login page
The frontend must accept a unified login response with at least these fields:
{
  "success": true,
  "requiresEmailVerification": false,
  "message": "...",
  "token": "...",
  "email": "...",
  "firstName": "...",
  "lastName": "...",
  "expiresAt": "...",
  "accountType": "StoreCustomer" | "StoreOwner",
  "roles": ["..."],
  "storeId": "...",
  "storeCustomerId": "... or null",
  "dashboard": "Customer" | "Owner"
}

How the frontend must behave after store login
1) If success=true and dashboard == Customer:
   - Save the token in the storefront-customer token storage.
   - Mark the auth context as storefront customer.
   - Open the customer dashboard.
2) If success=true and dashboard == Owner:
   - Save the token in the platform-owner token storage.
   - Mark the auth context as platform owner.
   - Open the owner dashboard for the current store.
3) If accountType == StoreOwner or roles contains StoreOwner:
   - Treat it as a platform owner session.
   - Never write it to customer storage.
4) If 401 with requiresEmailVerification == true:
   - Navigate to the verification flow for that store.
5) If 403:
   - Show a clear message that this account is not owner of this store.
6) If 400 or 401 without verification:
   - Show a generic invalid credentials/business rule message.

Hard anti-bug rules
1) Do not chain login -> register -> login.
2) Do not call /api/store-customer-auth/register as a fallback for store login.
3) Do not create a StoreCustomer when the user is actually a StoreOwner.
4) Do not redirect to any dashboard before reading response.dashboard and response.accountType.
5) Do not rely on frontend guesses about account type.
6) Do not use the same storage key for platform and storefront sessions.

Store-customer auth endpoints the frontend must support
1) GET /api/store-customer-auth/google
2) GET /api/store-customer-auth/google-callback
3) POST /api/store-customer-auth/register
4) POST /api/store-customer-auth/login
5) POST /api/store-customer-auth/store/{storeId}/login
6) POST /api/store-customer-auth/verify-email
7) POST /api/store-customer-auth/resend-verification-code
8) POST /api/store-customer-auth/forgot-password
9) POST /api/store-customer-auth/reset-password
10) POST /api/store-customer-auth/set-password
11) POST /api/store-customer-auth/store/{storeId}/set-password-from-auth-user

Token and session handling rules
1) Keep separate token keys for platform and storefront.
2) Keep separate session metadata for platform and storefront.
3) Never send a platform token to a customer-only API.
4) Never send a storefront customer token to an admin/backoffice API.
5) On logout, clear only the active session unless the user explicitly logs out all sessions.
6) If the route storeId does not match the token's store context, force re-authentication.
7) If a Google redirect returns accountType=StoreCustomer or role=StoreCustomer, create storefrontSession immediately even if pending Google auth state is missing.

Recommended storage keys
- platformOwnerTokenKey
- storefrontCustomerTokenKey
- platformSessionMetaKey
- storefrontSessionMetaKey

Route guard rules
1) Public store pages do not need auth.
2) Store customer pages require a storefront customer session.
3) Owner/admin pages require a platform session with StoreOwner role.
4) A customer token must never open owner-only routes.
5) An owner token must never be treated as a customer token.

Error handling contract
1) 200 OK: success.
2) 400 BadRequest: validation or business rule error. Parse { message } and model-state responses.
3) 401 Unauthorized: invalid credentials, missing token, or verification-required flow.
4) 403 Forbidden: valid token but not allowed for the requested store or action.
5) 404 NotFound: missing store/resource.

UX rules for the store login page
1) The login page must support both Owner and Customer.
2) The redirect must depend on response.dashboard, not hardcoded assumptions.
3) Show explicit messages for:
   - Not owner of this store
   - Verification required
   - Invalid credentials
4) Keep the state machine safe against double-submit and stale-token bugs.

Backend source of truth
Trust the backend and its response fields first. Use the following backend contracts as the source of truth for implementation:
- Controllers/StoreCustomerAuthController.cs
- Controllers/AuthController.cs
- Services/StoreCustomerAuth/StoreCustomerAuthService.cs
- Services/Auth/AuthService.cs
- Security/StoreAuthorizationService.cs
- Security/StoreAccountBoundaryService.cs

Testing checklist
1) StoreCustomer logs in from store page -> customer dashboard.
2) StoreOwner logs in from the same store page -> owner dashboard.
3) StoreOwner for a different store -> 403 handled correctly.
4) Verification-required flow works.
5) Customer token cannot open owner routes.
6) Owner token cannot be misused as customer token.
7) Session keys never overwrite each other.

Non-negotiable rules
1) Keep account boundaries strict.
2) Do not invent auth shortcuts.
3) Do not auto-create customer records.
4) Trust backend claims and dashboard fields over frontend guesses.
```
