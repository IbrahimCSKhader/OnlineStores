## Auth Flow AI Prompt (OnlineStore)

Use this prompt with another AI agent to get a full, code-accurate explanation of authentication and account boundaries in this backend.

```text
You are an AI reviewer for the onlineStore backend authentication system.

Your goal:
Explain the complete auth architecture exactly as implemented in code.
Do not invent endpoints, roles, tables, or behavior.

Core project context:
- Tech stack: ASP.NET Core + Identity + JWT.
- Platform accounts are stored in table: Users (AppUser).
- Storefront customer accounts are stored in table: CustomerStores (StoreCustomer).
- These are two separate account domains and must never be mixed.

Critical rules you must verify and explain:
1) Store owners are platform users only:
	- Created through: POST /api/Auth/create-owner (SuperAdmin only).
	- Stored in Users only.
	- Must NOT be created in StoreCustomer/CustomerStores.

2) Store customers are storefront users only:
	- Use /api/store-customer-auth/* endpoints.
	- Stored in CustomerStores only.

3) Owner/customer conflict (same store) must be blocked:
	- The store owner email is reserved for that store.
	- Owner email must never be accepted as StoreCustomer email for the same store.
	- This guard must be checked in:
	  - Store customer register
	  - Store customer login
	  - Store customer verify email
	  - Store customer resend verification code
	  - Store customer forgot password
	  - Store customer reset password
	  - Store customer set password
	  - Google storefront login flow
	  - Backoffice customer create/update flows

4) Auth context separation:
	- /api/Auth/* is for platform auth (SuperAdmin, StoreOwner).
	- /api/store-customer-auth/* is for storefront customer auth (StoreCustomer).
	- Never use platform auth endpoints for storefront customer lifecycle.

5) JWT separation:
	- Platform JWT carries platform roles (SuperAdmin/StoreOwner).
	- Store-customer JWT carries store-customer claims such as:
	  - store_customer_id
	  - store_id
	  - account_type = StoreCustomer
	  - is_guest

6) Google storefront login behavior:
	- Start: GET /api/Auth/google?storeId=... or ?storeSlug=...
	- Callback: GET /api/Auth/google-callback
	- Must end as StoreCustomer auth, not as platform AppUser auth.
	- Must reject when callback email belongs to the owner of that same store.

What you must produce:

A) Step-by-step flow map for:
	1. Platform login
	2. Owner creation
	3. Store-customer register/login
	4. Store-customer email verification + password reset
	5. Google storefront login

B) A table titled: Account Boundaries
	Columns:
	- Flow
	- Endpoint
	- Table touched
	- Token type
	- Conflict checks

C) A section titled: Security Guarantees
	Must prove from code that:
	- Owner is never created as StoreCustomer.
	- Owner email cannot exist as StoreCustomer for the same store.
	- Platform and storefront sessions are isolated.

D) A section titled: Expected Failure Cases
	Include the expected 400/401/403 style responses for boundary violations.

Code source of truth (must cite these files):
- Controllers/AuthController.cs
- Controllers/StoreCustomerAuthController.cs
- Controllers/CustomerStoreController.cs
- Services/Auth/AuthService.cs
- Services/StoreCustomerAuth/StoreCustomerAuthService.cs
- Services/CustomerStore/CustomerStoreService.cs
- Security/StoreAccountBoundaryService.cs
- Data/AppDbContext.cs

Rules for your analysis output:
- If docs and code conflict, trust code.
- Be explicit about which flow writes to Users vs CustomerStores.
- Include notes about owner-email reservation logic per store.
- Keep explanations technical and implementation-accurate.
```

