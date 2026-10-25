# Invoice print attempts

The merchant UI records one attempt after invoking the browser print command. Previewing does not count. Cancelling the browser dialog still counts: browsers do not report whether a printer produced paper. Historical attempts before this feature are unknown and start at zero.

Apply migration `20261025010000_AddOrderInvoicePrints` when deploying the backend. Existing orders and their prices are unchanged. The frontend can still print if saving the count fails; it shows an error with a retry that saves the same attempt without printing again.

Both endpoints require a StoreOwner or SuperAdmin token and management access to the requested store:

- `GET /api/order-invoice-prints/store/{storeId}` returns `{ orderId, printCount, lastPrintedAt }[]`. Orders with no print records have count zero.
- `POST /api/order-invoice-prints/store/{storeId}/{orderId}` accepts `{ "requestId": "<new UUID per print attempt>" }` and returns the updated count. Repeating the same UUID never counts twice. Reusing it for another order returns 409. Orders outside the store or deleted orders cannot be recorded.

Counts are shared across browsers and devices. The log stores the order, server timestamp, and authenticated actor. The primary key prevents duplicate retries and the order/time index supports the store counts query.
