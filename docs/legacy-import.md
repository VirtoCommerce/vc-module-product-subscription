# Importing subscriptions from the old subscription module

This module can take over subscriptions created by the old Virto subscription module (`VirtoCommerce.Subscription`).
The import works on JSON only: this module does not reference the old module, and the old module does not need to be
installed on the platform that runs the import.

## What gets imported

The old module keeps one subscription per order, with an order prototype that can hold several products. This module is
item-level, so **each product of the old order prototype becomes its own subscription**. Gift lines and cancelled lines
are left out; lines of the same product are merged.

| Old module | New subscription |
| --- | --- |
| `Id` | `OuterId` = `legacy:{Id}`, or `legacy:{Id}:{ProductId}` when the old subscription has several products |
| `Number` | Reported only; new subscriptions get numbers from `ProductSubscription.NumberTemplate` |
| `StoreId`, `CustomerId`, `CustomerName` | Same (falling back to the order prototype) |
| Prototype `OrganizationId`, `Currency`, `LanguageCode` | Same; currency and language fall back to the store defaults |
| Prototype line `ProductId`, `Sku`, `Name`, `ImageUrl`, `Quantity` | Same; empty values are filled from the catalog |
| Prototype line `PlacedPrice` | `ReferencePrice` |
| `Interval` (`Days`, `Weeks`, `Months`, `Years`) and `IntervalCount` | `IntervalUnit` (`Day`, `Week`, `Month`, `Year`) and `IntervalCount` |
| `SubscriptionStatus` `Active`, `Trialing` | `Active` |
| `SubscriptionStatus` `PastDue`, `Unpaid` | `PastDue` |
| `SubscriptionStatus` `Cancelled`, `IsCancelled`, or an `EndDate` that has passed | `Cancelled`, with `CancelledDate` and `CancelReason` |
| `Active` with `Balance` > 0 | `PastDue` (the old module treats it as `Unpaid`) |
| `CurrentPeriodEnd` (when the old module creates the next order) | `NextOrderDate` |
| `CurrentPeriodStart` | `LastOrderDate` |
| `StartDate` | `StartDate` |
| First shipment `DeliveryAddress` (or the order's shipping address) | `ShippingAddress` |
| First shipment `ShipmentMethodCode` / `ShipmentMethodOption` | `ShippingMethodCode` / `ShippingMethodOption` |
| First payment `GatewayCode` | `PaymentMethodCode` |

Import rules:

- **Idempotent.** A record whose `OuterId` already exists is reported as `SkippedDuplicate`. You can run the same import
  again after fixing the failed records.
- **Plan.** The importer asks for the active plan that the store offers for the product. The plan is linked only when it
  offers the legacy frequency. Otherwise the subscription is imported without a plan, keeps the legacy frequency, and the
  result says so.
- **Discount.** A discount in the record is kept. Without one, imported subscriptions get **no** subscription discount,
  so legacy customers keep the price they agreed to. Pass `applyPlanDiscount=true` to give them the linked plan's
  discount instead (e.g. `POST api/product-subscription/import/legacy?dryRun=true&applyPlanDiscount=true`). The old
  module has no subscription discount, so the JSON mapper never sets one.
- **Next order date.** Never in the past: a date before tomorrow is moved to tomorrow (now + 1 day), and the result says
  so. With the default store settings, the reminder email goes out and the order is placed on the first scheduler run
  after that date minus the cutoff, so check the dry-run dates before the real import.
- **Validation.** Store, customer, product (from the order prototype), quantity 1–9999 and frequency 1–365 are required.
  The store must exist. The product must exist, except for cancelled subscriptions, which are kept as history.
- **No emails.** The import sends no "subscription created" emails. The import date counts as the last annual
  reminder, so the first scheduler run doesn't email every old subscriber.
- **Dry run.** Everything is validated and reported, including the status, plan and next order date each subscription
  would get, but nothing is saved.

### Payments: legacy subscriptions have no saved card

The old module never saved cards: it created each recurring order with a `New` payment that was collected separately.
Imported subscriptions therefore get `StoredPaymentMethodId = null` and keep the old payment method code. They keep their
status, so active subscriptions keep renewing:

1. The scheduler creates the renewal, sends the reminder and places the order as usual. Renewal carts only offer
   payment methods that support saved cards (`ISupportStoredCredentialFlow`).
2. When the renewal reaches `Ordered` and there is no saved card, the renewal goes to `PaymentActionRequired`: the
   customer gets the "confirm payment" email with a link to the order payment page, where they pay with a card and can
   save it for the next deliveries. Orders paid with a method that can't charge saved cards (for example invoice terms)
   are closed as `Completed` with "payment is collected separately".
3. Once the customer (in the storefront) or an admin (`PUT api/product-subscription/subscriptions/{id}` with
   `storedPaymentMethodId`) sets a saved card, renewals are charged automatically.

Imported `PastDue` subscriptions don't renew until a saved card is set; setting one makes them `Active` again.

Prices: the old module copied the order prototype, prices included. This module prices every renewal through xCart at the
current price, minus the subscription discount. `ReferencePrice` keeps the unit price the customer paid in the old module.

## Step 1. Export from the old module

1. On the platform that runs the old module, turn off its jobs so they don't create orders during or after the move:
   set **Subscription.EnableSubscriptionProcessJob** and **Subscription.EnableSubscriptionOrdersCreateJob** to `false`.
2. Read the subscriptions with their order prototypes, page by page (the user needs `subscription:read`):

   ```http
   POST /api/subscriptions/search
   Content-Type: application/json

   { "responseGroup": "Default,WithOrderPrototype", "skip": 0, "take": 100 }
   ```

   Add `"storeId"` or `"statuses": ["Active", "Trialing", "PastDue", "Unpaid"]` to import part of the data.
   Save each response as is: the import accepts the search result (`{ "totalCount": ..., "results": [...] }`).

   Don't use the platform's Export/Import package for this: it writes subscriptions without their order prototypes, so
   the products are missing and every record fails.

## Step 2. Import (dry run first)

The user needs the `product-subscription:import` permission.

```http
POST /api/product-subscription/import/legacy?dryRun=true
Content-Type: application/json

{ "totalCount": 2, "results": [ ...subscriptions from step 1... ] }
```

The body can be:

- the old module's JSON: a search result, an array of subscriptions, a single subscription, or `{ "Subscriptions": [...] }`;
- an array of `LegacySubscriptionImportRecord` objects, for data prepared by another tool (detected by its
  `legacySubscriptionId` or `intervalUnit` properties):

  ```json
  [
    {
      "legacySubscriptionId": "6a1f...",
      "storeId": "B2B-store",
      "customerId": "c0ffee...",
      "productId": "8b7b...",
      "quantity": 2,
      "intervalUnit": "Month",
      "intervalCount": 1,
      "status": "Active",
      "nextOrderDate": "2026-11-15T00:00:00Z",
      "paymentMethodCode": "AuthorizeNetPaymentMethod",
      "currency": "USD",
      "shippingAddress": { "firstName": "Jane", "lastName": "Doe", "line1": "1 Main St", "city": "Springfield", "postalCode": "12345", "countryCode": "USA" }
    }
  ]
  ```

`dryRun` defaults to `true`. The response is a `SubscriptionImportResult`:

```json
{
  "dryRun": true,
  "totalCount": 3,
  "createdCount": 2,
  "skippedCount": 0,
  "failedCount": 1,
  "records": [
    {
      "legacySubscriptionId": "6a1f...",
      "legacySubscriptionNumber": "SU240101-00001",
      "outerId": "legacy:6a1f...:8b7b...",
      "productId": "8b7b...",
      "outcome": "Created",
      "status": "Active",
      "planId": "4d2e...",
      "planName": "Subscribe & Save",
      "nextOrderDate": "2026-11-15T00:00:00Z",
      "messages": []
    }
  ]
}
```

In a dry run, `createdCount` and `Created` mean "would be created". Fix the failed records (create the missing plans,
restore products, correct the data), run the dry run again, then run the import with `dryRun=false`. Running it again
later only adds the records that are not imported yet.

## Step 3. After the import

- Check the imported subscriptions in **Subscriptions** (search by `outerId` through
  `POST api/product-subscription/subscriptions/search` with `{ "outerIds": ["legacy:..."] }`).
- Keep the old module's jobs turned off, or uninstall the old module, so customers don't get two orders.
- Ask customers to add a saved card in **My account › Subscriptions** to get automatic charging.
