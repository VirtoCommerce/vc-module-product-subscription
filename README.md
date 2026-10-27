# Product Subscription Module

## Overview

The Product Subscription module adds Subscribe & Save to Virto Commerce: item-level subscriptions for physical products. A store defines subscription plans with delivery frequencies and a discount, and assigns them to products and categories. A customer subscribes a cart line at checkout; each delivery then becomes a new order, priced through the cart and charged to the customer's saved card without the customer present. Customers manage their subscriptions in the storefront through xAPI, and operators manage them in the Admin Shell.

## Key Features

* **Subscription plans** — delivery frequencies (days, weeks, months, years) with a default, a percent or per-item discount, priority, and the products and categories a plan applies to
* **Item-level subscriptions** — a subscription per subscribed cart line, with its own quantity, frequency, next order date and saved card; pack size and minimum and maximum quantity are enforced on every change
* **Renewals** — due subscriptions of a customer are grouped into one renewal order, priced through the cart (prices, promotions, tax, stock); a reminder before each order states the maximum price, and a price increase or an unavailable item sends the renewal to review
* **Payments without the customer present** — saved cards are charged through the Payment module's stored credential contract: authorization at order creation, capture at shipment, re-authorization when it expires, retries after a soft decline within the card network limit, and a 3-D Secure recovery path for the customer
* **US auto-renewal compliance** — terms shown before the card fields with an unchecked consent box, recorded consents (terms version and hash, channel, IP address), a reminder before each order, a yearly reminder, and one-step cancellation
* **Customer self-service (xAPI)** — list and detail, skip, pause and resume, change frequency, quantity, next date and card, order now, cancel, and saved cards
* **Back office (Admin Shell)** — subscriptions, renewals and plans, a run scheduler command, and a subscriptions widget on the contact; desktop and phone, in the Admin Shell's 15 languages
* **Email notifications** — subscription created and cancelled, renewal reminder, review required, payment action required, payment failed, yearly reminder
* **Import from the old subscription module** — subscriptions exported from `VirtoCommerce.Subscription` as JSON, with a dry run; this module does not depend on the old one
* **Mock stored-card payment method** — for development and tests: test cards for success, declines and 3-D Secure, registered only when `ProductSubscription:EnableMockPaymentMethod` is `true`
* **Multi-database support** — SQL Server, MySQL and PostgreSQL via dedicated EF Core provider assemblies

## Configuration

### Application Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ProductSubscription.Scheduler.Enabled` | Boolean | `true` | Run the renewal scheduler |
| `ProductSubscription.Scheduler.CronExpression` | ShortText | `*/5 * * * *` | How often due renewals are processed; a change applies without a restart |
| `ProductSubscription.NumberTemplate` | ShortText | `SUB{0:yyMMdd}-{1:D5}` | Subscription number template |
| `ProductSubscription.Renewal.NumberTemplate` | ShortText | `REN{0:yyMMdd}-{1:D5}` | Renewal number template |
| `ProductSubscription.Payments.AuthorizationLifetimeMinutes` | Integer | `10080` | How long a renewal's card authorization stays valid at the payment provider; an order that ships later is authorized again before the capture. Shorten it to test re-authorization |

### Store Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ProductSubscription.Enabled` | Boolean | `true` | Offer Subscribe & Save in the store; the storefront hides every subscription screen when it is off |
| `ProductSubscription.ReminderLeadDays` | Integer | `3` | Days before each delivery the reminder email is sent |
| `ProductSubscription.CutoffHours` | Integer | `24` | Hours before the delivery when prices are re-checked and the order is placed |
| `ProductSubscription.RetryScheduleHours` | ShortText | `24,72,120` | Comma-separated delays between payment retries after a soft decline |
| `ProductSubscription.MaxRetriesPer30Days` | Integer | `15` | Card network limit on payment attempts per 30 days |
| `ProductSubscription.PaymentActionWaitDays` | Integer | `7` | Days to wait for 3-D Secure confirmation or a review before the renewal fails or is skipped |
| `ProductSubscription.Terms.Version` | ShortText | `2026-10` | Version of the subscription terms; change it with the text, and customers accept the new version at checkout |
| `ProductSubscription.Terms.Text` | LongText | *(US auto-renewal disclosure)* | Terms shown before the payment fields at checkout |
| `ProductSubscription.AnnualReminder.Enabled` | Boolean | `true` | Send the yearly reminder (required in California, Minnesota and Connecticut) |

### Application Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `ProductSubscription:EnableMockPaymentMethod` | `false` | Register the mock stored-card payment method; never enable it in production |

### Permissions

| Permission | Description |
|------------|-------------|
| `product-subscription:access` | Open the Subscriptions menu |
| `product-subscription:read` | View subscriptions, renewals and plans |
| `product-subscription:update` | Change, skip, pause, resume and cancel subscriptions |
| `product-subscription:delete` | Delete subscriptions |
| `product-subscription:plans:manage` | Create and edit subscription plans |
| `product-subscription:renewals:process` | Process renewals, order now and retry payments |
| `product-subscription:import` | Import subscriptions from the old subscription module |

## Architecture

The module follows the layered architecture of Virto Commerce modules:

```
┌─────────────────────────────────────────────────────────────┐
│  Web Layer (REST API, Module initialization,                │
│  Admin Shell plugin, notification templates)                │
├──────────────────────────────┬──────────────────────────────┤
│  Xapi (GraphQL queries,      │  Data Layer (Services,       │
│  mutations, type hooks)      │  Renewals, Pricing, Payments,│
│                              │  Import, Jobs, EF Core)      │
├──────────┬──────────────┬────┴─────────┬────────────────────┤
│ SqlServer│    MySql     │  PostgreSql  │  DB Providers      │
├──────────┴──────────────┴──────────────┴────────────────────┤
│  Core Layer (Domain Models, Service Interfaces, Events,     │
│  Constants, Settings, Permissions)                          │
└─────────────────────────────────────────────────────────────┘
```

### Subscription Flow

1. **Offer** — the product page asks `subscriptionOffers` (or `Product.subscriptionOffer`) for the plan, discount and frequencies of a product
2. **Cart** — `setCartItemSubscription` marks a cart line as a subscription; the cart applies the plan discount to that line
3. **Consent** — at checkout the customer accepts the terms with `acceptSubscriptionTerms`, which records a `PaymentConsent`
4. **Order** — when the order is created, `OrderChangedEventHandler` creates a subscription per subscribed line and attaches the card the payment saved

### Renewal Flow

1. **Schedule** — `RenewalSchedulerJob` (a platform background job on the `ProductSubscription.Scheduler.CronExpression` schedule) runs `IRenewalProcessor`, which groups a customer's due subscriptions into one renewal (`Scheduled`)
2. **Remind** — `ReminderLeadDays` before the delivery the customer gets a reminder with the maximum price (`Notified`)
3. **Cutoff** — `CutoffHours` before the delivery the renewal is priced again through the cart; a higher price or an unavailable item moves it to `ReviewRequired`, otherwise the order is placed (`Ordered`)
4. **Charge** — the saved card is authorized with `ProcessStoredPaymentAsync` (`Authorized`); a soft decline schedules a retry (`RetryScheduled`), 3-D Secure asks the customer to confirm (`PaymentActionRequired`), a hard decline fails the renewal and marks the card for update (`Failed`)
5. **Capture** — the payment is captured at shipment, re-authorized first if the authorization expired (`Completed`)
6. Each step is recorded in the renewal's history; renewals can also be skipped or cancelled before the order (`Skipped`, `Cancelled`)

## Components

### Projects

| Project | Layer | Purpose |
|---------|-------|---------|
| `VirtoCommerce.ProductSubscription.Core` | Core | Domain models (plans, subscriptions, renewals, cart items, consents), service interfaces, events, constants, settings and permissions |
| `VirtoCommerce.ProductSubscription.Data` | Data | Services, renewal processing, cart pricing and promotions, the mock payment method, legacy import, event handlers, background jobs, EF Core repository |
| `VirtoCommerce.ProductSubscription.Data.SqlServer` | Data | SQL Server EF Core configurations and migrations |
| `VirtoCommerce.ProductSubscription.Data.MySql` | Data | MySQL EF Core configurations and migrations |
| `VirtoCommerce.ProductSubscription.Data.PostgreSql` | Data | PostgreSQL EF Core configurations and migrations |
| `VirtoCommerce.ProductSubscription.Xapi` | Xapi | GraphQL queries, mutations and type hooks for the storefront |
| `VirtoCommerce.ProductSubscription.Web` | Web | REST API, module bootstrapping and DI registration, notification templates, the Admin Shell plugin (`vue/`) |
| `VirtoCommerce.ProductSubscription.Tests` | Tests | Unit tests |
| `VirtoCommerce.ProductSubscription.ConnectorTestKit` | Tests | Contract tests a payment connector runs to prove it supports stored credential charges |

### Key Services

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `SubscriptionManager` | `ISubscriptionManager` | Customer and operator actions: change, skip, pause, resume, cancel, order now |
| `RenewalProcessor` | `IRenewalProcessor` | Groups due subscriptions into renewals and moves them through reminder, cutoff, order, charge and capture |
| `RenewalCartBuilder` | — | Prices a renewal through the cart (prices, promotions, tax, stock) |
| `SubscriptionOfferService` | `ISubscriptionOfferService` | Finds the plan, discount and frequencies a product can be subscribed with |
| `SubscriptionTermsService` | `ISubscriptionTermsService` | The store's terms version, text and hash |
| `SubscriptionNotificationService` | — | Sends the subscription and renewal emails |
| `LegacySubscriptionImporter` | `ILegacySubscriptionImporter` | Imports subscriptions from the old subscription module's JSON |
| `SubscriptionPlanService`, `SubscriptionService`, `RenewalService`, `SubscriptionCartItemService` | `ISubscriptionPlanService`, `ISubscriptionService`, `IRenewalService`, `ISubscriptionCartItemService` and their search services | CRUD and search, publishing `*ChangingEvent` / `*ChangedEvent` |
| `MockStoredCardPaymentMethod` | `ISupportStoredCredentialFlow` | Mock payment method for development and tests |

### REST API

Base route: `api/product-subscription`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/plans/search` | Search plans |
| `GET` | `/plans/{id}` | Get a plan |
| `POST` | `/plans` | Create or update a plan |
| `DELETE` | `/plans` | Delete plans |
| `GET` | `/offers` | Subscription offers of products in a store |
| `GET` | `/terms` | The store's subscription terms |
| `POST` | `/subscriptions/search` | Search subscriptions |
| `GET` | `/subscriptions/{id}` | Get a subscription |
| `PUT` | `/subscriptions/{id}` | Change quantity, frequency, next order date or card |
| `POST` | `/subscriptions/{id}/skip` | Skip the next delivery |
| `POST` | `/subscriptions/{id}/pause` | Pause, optionally until a date |
| `POST` | `/subscriptions/{id}/resume` | Resume |
| `POST` | `/subscriptions/{id}/cancel` | Cancel |
| `POST` | `/subscriptions/{id}/order-now` | Place the next order now |
| `DELETE` | `/subscriptions` | Delete subscriptions |
| `GET` | `/customers/{customerId}/saved-payment-methods` | A customer's saved cards |
| `GET` | `/consents/{id}` | Get a consent record |
| `POST` | `/renewals/search` | Search renewals |
| `GET` | `/renewals/{id}` | Get a renewal |
| `POST` | `/renewals/{id}/process` | Process a renewal now |
| `POST` | `/renewals/{id}/skip` | Skip a renewal |
| `POST` | `/renewals/{id}/accept` | Accept the changes of a renewal in review |
| `POST` | `/renewals/{id}/retry-payment` | Retry the payment |
| `POST` | `/renewals/{id}/cancel` | Cancel a renewal |
| `POST` | `/scheduler/run` | Process due renewals now |
| `POST` | `/import/legacy` | Import subscriptions from the old subscription module (`dryRun`, `applyPlanDiscount`) |

### GraphQL (xAPI)

Each operation acts on the signed-in customer's own subscriptions, renewals, carts and cards.

| Kind | Operations |
|------|------------|
| Queries | `productSubscriptions`, `productSubscription`, `subscriptionRenewals`, `subscriptionRenewal`, `savedPaymentMethods`, `subscriptionTerms`, `subscriptionOffers`, `cartSubscriptionItems` |
| Mutations | `setCartItemSubscription`, `removeCartItemSubscription`, `acceptSubscriptionTerms`, `skipProductSubscription`, `pauseProductSubscription`, `resumeProductSubscription`, `cancelProductSubscription`, `changeProductSubscription`, `orderProductSubscriptionNow`, `acceptSubscriptionRenewal`, `skipSubscriptionRenewal`, `retrySubscriptionRenewalPayment`, `addSavedPaymentMethod`, `removeSavedPaymentMethod`, `setDefaultSavedPaymentMethod` |
| Fields on existing types | `Product.subscriptionOffer(storeId)`, `LineItemType.subscription` |

### Back Office

The back office is an Admin Shell plugin built from `src/VirtoCommerce.ProductSubscription.Web/vue` into `plugins/admin-shell` on publish. The module ships no AngularJS UI: without the Admin Shell module the back office is the REST API only. See [docs/admin-ui.md](docs/admin-ui.md).

### Notifications

| Notification | Sent when |
|--------------|-----------|
| `SubscriptionCreatedEmailNotification` | A subscription is created at checkout |
| `RenewalReminderEmailNotification` | `ReminderLeadDays` before a delivery, with the maximum price |
| `RenewalReviewRequiredEmailNotification` | A renewal needs the customer's review (price increase, unavailable item) |
| `RenewalPaymentActionRequiredEmailNotification` | The bank asks the customer to confirm the payment (3-D Secure) |
| `RenewalPaymentFailedEmailNotification` | The payment of a renewal failed |
| `SubscriptionCancelledEmailNotification` | A subscription is cancelled |
| `SubscriptionAnnualReminderEmailNotification` | Once a year, when `AnnualReminder.Enabled` is on |

### Dependencies

Core, Store, Customer, Catalog, Cart, Orders, Payment (stored credential contract), Marketing, Notifications, Xapi, XCatalog and XCart modules.

## Documentation

* [Back office (Admin Shell plugin)](docs/admin-ui.md)
* [Import from the old subscription module](docs/legacy-import.md)
* [End-to-end scenarios](tests/e2e/README.md)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-product-subscription/)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-product-subscription/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

This software is licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense.

Unless required by the applicable law or agreed to in written form, the software
distributed under the License is provided on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
