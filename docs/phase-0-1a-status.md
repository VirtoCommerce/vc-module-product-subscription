# Phase 0 and Phase 1A: status

Branch `feat/VCST-6507` in each repository below (not merged, not pushed). Phases as in the Product Subscriptions
benchmark's roadmap: Phase 0 hardens the existing 3.x module, Phase 1A builds the new module on a mock payment connector.

How each item is verified:
- **Unit**: unit tests in the repository (`dotnet test`).
- **E2E**: the Playwright scenarios in [`tests/e2e`](../tests/e2e/README.md), desktop and phone.
- **Manual**: checked in the running platform and storefront, during the build.

## Phase 0: hardening 3.x

| Item | Where | Verified |
|---|---|---|
| Renewal idempotent (an order saved without its subscription is not created twice) | vc-module-subscription `41e7322`: `CreateRecurrentOrdersJob` | Unit (`CreateRecurrentOrdersJobTests`) |
| `SubscriptionBuilder` safe to share; no sync-over-async | `41e7322`: `TakeSubscription` returns a bound copy; async job and handler paths | Unit (concurrent builder test) |
| PastDue reachable; no schedule drift; manual "create order" persists the period | `41e7322`: `SubscriptionPeriodCalculator` (anchor based), `Subscription.PastDue.Delay`, `POST api/subscriptions/order` | Unit (period math, status evaluation) |
| Subscription ownership in the API | `41e7322`: the REST API is back-office only (permission per endpoint), documented in `docs/index.md`; customer self-service goes through the new module's xAPI, which checks ownership | Unit (endpoint permission tests) |
| Honour `EnableSubscriptions` | vc-module-subscription `297b225`: orders of a store with it off create no subscription | Unit (enabled, unset, disabled) |
| Unit tests for period math, status changes and the jobs | `41e7322`, `297b225` | 94 tests pass |
| Marketplace: template and child orders not split | vc-module-marketplace-vendor `5bc710a`: `ShouldProcessOrder`, payments stay on the parent order | Unit (handler filter, splitter) |
| Skyflow: card ownership on delete, no stored or exposed CVV, safe queries | vc-module-skyflow `ba13a97`; vc-frontend `b6ddf23ed` keeps the CVV out of saved cards | Unit (ownership, id validation, session tokens) |

## Phase 1A: MVP on a mock payment connector

| Item | Where | Verified |
|---|---|---|
| New module: plans with a discount, assigned to products and categories; frequency chosen per item on the product page | vc-module-product-subscription (`b850e11` and after); vc-frontend `622b52275` (purchase options) | Unit; E2E (plan editor, product page) |
| Item-level subscriptions; renewals that group a customer's due items; renewal priced through the cart (pricing, promotions, tax, stock) | `RenewalProcessor` (`GetGroupKey`), cart aggregate pricing (`e6006b4`, `bcc8c5f`) | Unit; Manual (renewal totals and discount) |
| Payment contract: stored credential flow, `StoredPaymentMethod`, `PaymentConsent`, standard charge results | vc-module-payment `dad9d1f` | Unit (connector contract) |
| Mock stored-card connector | `MockStoredCardPaymentMethod`, test cards for success, soft and hard declines, 3-D Secure on save and on renewal, gateway timeout | Unit (`MockConnectorContractTests`) |
| Authorize at order creation, capture at shipment, re-authorize when expired | renewal states Authorized → Completed; `MockPayment.AuthorizationLifetimeMinutes`; `fb551cf` | Unit; Manual |
| Retry policy and payment failure; 3-D Secure recovery for the customer | `RetryScheduleHours`, `MaxRetriesPer30Days`, `PaymentActionRequired`; vc-frontend `7951efdd9`, `a976a5320`, `f1e1105ae` | Unit; E2E (renewal with payment action shown); Manual (confirm payment) |
| US compliance: terms before the card fields, unchecked consent box, consent records, reminder with a maximum price before each order, yearly reminder, one-click cancel | checkout terms component (unchecked by default), `PaymentConsent`, reminder emails (`2d04aa4`, `2bbfabb`), `AnnualReminder.Enabled`, storefront cancel | Unit; Manual (emails in the log) |
| xAPI and storefront: list, detail, skip, pause and resume, change frequency and quantity, cancel; saved payment methods | xAPI project; vc-frontend subscription pages; pack size and quantity limits (`fafdffb`, `ecb626582`) | Unit; E2E (account list, detail, pause and resume, payment methods) |
| Back office | Admin Shell plugin (`cf8c1dd` and after), 15 languages, phone and desktop, dark theme | E2E (all back-office screens, desktop and phone); Manual (dark theme, Hebrew right-to-left) |
| Importer for old subscriptions; shared connector test kit, green against the mock | `dcaf2f8` (`docs/legacy-import.md`), `b99d992` | Unit |

## Not in these phases

Real gateways (Authorize.Net, CyberSource) and their sandboxes are Phase 1B. Delivery-day merging, product swap,
analytics and the card-network retry engine are Phase 2.
