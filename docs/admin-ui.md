# Back office: Admin Shell plugin

The module's back office is a Vue plugin of the [Admin Shell](https://github.com/VirtoCommerce/vc-module-admin-shell)
(vc-shell 2.6.3-rc.0). It replaces the AngularJS blades the module used to ship: the module has no AngularJS UI any more.

## Screens

| Blade | Opens from | Does |
|---|---|---|
| Subscriptions | menu (More → Subscriptions until it is placed in a section with the menu editor) | search, status filter, open a subscription; Renewals, Plans, Run scheduler |
| Subscription | a subscription row | quantity, frequency, next order date and card; Order now, Skip next, Pause (optional resume date), Resume, Cancel (with a reason); customer, delivery, consent, orders; its renewals |
| Renewals | Subscriptions toolbar | search, state and "open only" filters |
| Renewal | a renewal row | Process now, Accept changes, Retry payment, Skip, Cancel, Open order; totals, payment attempts, items, history |
| Subscription plans / plan | Subscriptions toolbar | create, edit, delete; frequencies, discount, store, priority, products and categories (catalog picker) |
| Customer subscriptions | the Customer module's contact blade (widget), when its Admin Shell plugin is installed | the contact's subscriptions |

Orders open in the Orders module's Vue blade when its plugin is installed, otherwise in its AngularJS blade inside the
shell.

## Sources and build

- Sources: `src/VirtoCommerce.ProductSubscription.Web/vue/` (Yarn 4.9.1 is committed under `.yarn/releases`).
- Output: `src/VirtoCommerce.ProductSubscription.Web/plugins/admin-shell/` (not committed). `dotnet publish` of the web
  project builds it and copies it next to `bin/`, so `vc-build Compress` packs it; `-p:SkipAdminShellPlugin=true` leaves it out.
- Menu item: `vue/public/plugin.json` (`contributions.menu`).
- Texts: `vue/src/modules/locales/<code>.json`, the 15 languages of the Admin Shell. English is the source; check a
  translation with the Admin Shell's `scripts/l10n_check.py`.

```bash
cd src/VirtoCommerce.ProductSubscription.Web/vue
yarn install
yarn type-check
yarn build
```

To try a change against a running platform, copy `plugins/admin-shell/` to
`{platform}/modules/VirtoCommerce.ProductSubscription/plugins/admin-shell/`; in Development the shell picks it up on
the next page load.

After a change to the module's REST API, regenerate the client against a platform with the module installed:

```bash
APP_PLATFORM_URL=http://localhost:5005/ yarn generate-api-client
```
