# Frontend AI Prompt: StoreCustomer Auth and Google Login for OnlineStore

Use this prompt with the frontend AI agent.

```text
You are the frontend integration agent for OnlineStore.

Your task:
Implement storefront authentication correctly for customers who are logging in from inside a store page.
The storefront login must use the store-scoped auth controller.
Do not guess session type from pending sessionStorage state alone.
Do not route Google login through platform auth.
Do not fall back to registration when login fails.

Core auth model
There are 2 isolated auth domains:
1) Platform auth for SuperAdmin and StoreOwner via /api/auth/*
2) Storefront customer auth for StoreCustomer via /api/store-customer-auth/*

What you must know about StoreCustomer auth
1) StoreCustomer accounts live in CustomerStores only.
2) StoreOwner accounts live in Users only.
3) A store page may authenticate either a StoreCustomer or a StoreOwner.
4) If the token represents StoreCustomer, it must become a storefront session.
5) If the token represents StoreOwner, it must become a platform session.
6) Do not assume the session type only from the page where the user clicked login.

Storefront login endpoints
1) POST /api/store-customer-auth/store/{storeId}/login
2) GET /api/store-customer-auth/google?storeId=...&storeSlug=...&redirectTo=...
3) GET /api/store-customer-auth/google-callback

Normal email/password login flow
1) The store login form should call:
   - POST /api/store-customer-auth/store/{storeId}/login
2) Send only:
   - email
   - password
3) Read the returned object.
4) If success=true and dashboard == Customer:
   - Save the token in storefront session storage.
   - Mark the session as storefront customer.
   - Redirect to the customer dashboard.
5) If success=true and dashboard == Owner:
   - Save the token in platform session storage.
   - Mark the session as platform owner.
   - Redirect to the owner dashboard.
6) If requiresEmailVerification == true:
   - Redirect to the verification flow for the same store.
7) If IsForbidden == true or HTTP 403:
   - Show a clear message that this account is not the owner of this store.
8) If validation fails or credentials are invalid:
   - Show the backend message.

Google login flow for storefront
1) When the user clicks "Continue with Google" from a store page, start Google auth from the store-scoped controller:
   - GET /api/store-customer-auth/google?storeId=...&storeSlug=...&redirectTo=...
2) Store context must be present at challenge time.
3) Preserve the following values through the roundtrip:
   - storeId
   - storeSlug
   - redirectTo
4) When Google returns to the callback, the backend may automatically create a StoreCustomer if that Google email does not already exist as a store customer.
5) Do not send the user to registration first.
6) Do not depend only on pending Google auth state in sessionStorage.

How to classify the Google result
The final Google success response must be classified from the returned token and metadata.
Use these fields as the source of truth:
- accountType
- roles
- storeId
- storeCustomerId
- authMode
- sessionScope
- dashboard

Classification rules
1) If accountType is StoreCustomer, or roles contains StoreCustomer:
   - Store it in storefront session storage.
   - Mark it as storefrontSession.
   - Open the customer dashboard.
2) If accountType is StoreOwner, or roles contains StoreOwner:
   - Store it in platform session storage.
   - Mark it as platformSession.
   - Open the owner dashboard.
3) If dashboard == Customer:
   - Treat it as storefront customer session.
4) If dashboard == Owner:
   - Treat it as platform owner session.
5) If sessionScope == storefront:
   - Do not write the token to platform storage.
6) If sessionScope == platform:
   - Do not write the token to storefront storage.

Expected Google success payload
The backend success redirect must provide a fragment with at least:
{
  token,
  email,
  firstName,
  lastName,
  storeId,
  storeSlug,
  storeCustomerId,
  accountType,
  redirectTo,
  authMode,
  sessionScope,
  dashboard
}

How to store the Google result
1) Parse the fragment after redirect.
2) Validate token + metadata.
3) If token belongs to StoreCustomer, store all storefront session data in storefront storage only.
4) If token belongs to StoreOwner, store all platform session data in platform storage only.
5) Persist session metadata separately from token storage.
6) Never use the same storage key for both session types.

Recommended storage keys
- platformOwnerTokenKey
- storefrontCustomerTokenKey
- platformSessionMetaKey
- storefrontSessionMetaKey

Frontend behavior after Google callback
1) If the response indicates a storefront customer:
   - Save the storefront session.
   - Redirect to the store customer dashboard.
2) If the response indicates a platform owner:
   - Save the platform session.
   - Redirect to the owner dashboard.
3) If the response contains an errorCode:
   - Show the mapped error message.
4) If the response is missing token or required metadata:
   - Treat it as a failed auth flow.

Error handling contract
1) store_context_required:
   - Show a message that the store is required before Google login can start.
2) store_context_missing:
   - Show a message that the store context was lost during the Google roundtrip.
3) google_auth_failed:
   - Show a generic Google authentication failure message.
4) email_not_found:
   - Show a message that Google did not return an email.
5) store_invalid_or_missing:
   - Show a message that the store is invalid or inactive.
6) owner_customer_conflict:
   - Show a message that this email belongs to the store owner flow, not the storefront customer flow.
7) jwt_generation_failed:
   - Show a message that the session could not be completed.
8) redirect_build_failed:
   - Show a message that the Google redirect could not be completed.
9) unexpected_error:
   - Show a generic unexpected error message.

Critical anti-bug rules
1) Do not call /api/store-customer-auth/register as a fallback for Google login.
2) Do not create a StoreCustomer on the frontend.
3) Do not infer session type only from the page route.
4) Do not ignore accountType, roles, dashboard, sessionScope, or storeCustomerId.
5) Do not let a StoreCustomer token overwrite platform session storage.
6) Do not let a StoreOwner token overwrite storefront session storage.
7) Do not redirect before reading the complete response contract.

Route guard rules
1) Public store pages do not need auth.
2) Store customer pages require storefront customer session.
3) Owner/admin pages require platform session with StoreOwner role.
4) A customer token must never open owner-only routes.
5) An owner token must never be treated as a customer token.

Implementation checklist
1) Build a store login page that supports email/password and Google.
2) Use the same store context for both login methods.
3) Handle the redirect contract from the Google callback.
4) Store sessions in the correct storage bucket.
5) Verify that the dashboard selection is driven by response.dashboard.
6) Make sure Google login works even when the user has no pre-existing StoreCustomer account.
7) Make sure the user can continue through Google without first creating an account manually.

Backend source of truth
Use these files as the authority for the response shape and flow:
- Controllers/StoreCustomerAuthController.cs
- Services/StoreCustomerAuth/StoreCustomerAuthService.cs
- Controllers/AuthController.cs
- Services/Auth/AuthService.cs

Non-negotiable rules
1) Keep platform and storefront sessions isolated.
2) Trust backend claims and response metadata over frontend guesses.
3) If the token says StoreCustomer, it is a storefront session.
4) If the token says StoreOwner, it is a platform session.
```