# End-to-end scenarios

Playwright scenarios for the Product Subscriptions back office (the Admin Shell plugin) and storefront (vc-frontend),
each run at desktop size (1440×900) and on a phone (Pixel 7: narrow, touch, mobile user agent).

| Project | Covers |
|---|---|
| `admin-desktop`, `admin-mobile` | menu entry; subscriptions list and search; schedule edit with server validation and reset; pause and resume; renewals list and detail (payment, items, history, open order); a subscription's renewals; plan editor with the catalog picker; plan create and delete; Run scheduler; no console errors |
| `storefront-desktop`, `storefront-mobile` | Subscribe & Save on the product page; account subscriptions, deliveries and tabs; subscription detail with pause and resume; saved payment methods |

The scenarios leave the data as they found it: edits are reset, a paused subscription is resumed, the temporary plan is
deleted. They need a platform with the module and a storefront with at least one active subscription (with renewals)
for the buyer, and an active plan; the module's demo data has them.

## Run

The servers must be running. Accounts come from the environment, never from the suite:

| Variable | Default |
|---|---|
| `E2E_ADMIN_USER`, `E2E_ADMIN_PASSWORD` | required: a back-office user with the `product-subscription:*` permissions |
| `E2E_BUYER_USER`, `E2E_BUYER_PASSWORD` | required: the storefront buyer who owns the subscriptions |
| `E2E_PLATFORM_URL` | `http://localhost:5005` |
| `E2E_STOREFRONT_URL` | `https://localhost:3000` |
| `E2E_BROWSER_CHANNEL` | `msedge` (the installed Edge; `chrome` works too, no browser download needed) |

```bash
cd tests/e2e
npm ci
npx playwright test                 # everything
npm run test:admin                  # back office only
npm run test:storefront             # storefront only
npx playwright show-report          # the HTML report, with traces of failures
```

A storefront dev server compiles pages on their first visit, so the first storefront scenario can take a minute.
