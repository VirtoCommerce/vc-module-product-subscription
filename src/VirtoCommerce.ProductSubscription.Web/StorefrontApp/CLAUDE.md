# CLAUDE.md

A storefront plugin: a Module Federation remote that runs inside the Virto Commerce storefront (vc-frontend).
How it plugs in, its files, rules and commands: @README.md

## Before a change is done

1. `yarn type-check`, `yarn lint` and `yarn test` pass. Changed `plugin.config.ts`? `yarn build` too: it validates
   the declarations.
2. From a vc-frontend checkout, `yarn check:plugin-module` on the module passes.

## Never

1. Import from a storefront path (`@/…`). A symbol the facade lacks is either added to it in vc-frontend
   ([S3](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/s3-extend-the-facade.md)) or copied here, small.
2. Edit `@vc-frontend/core` types, or commit its pin as `portal:`, `link:` or a yalc path.
3. Move a `@tailwind` directive out of `@layer plugins` in `src/styles.css`: unlayered, the plugin's
   utilities override the storefront's responsive ones on every page.
4. Edit `src/api/graphql/types.ts` by hand: run `yarn generate:graphql-types`.

## Where each task is described

| Task | Doc |
| --- | --- |
| Wire it into the module, before the first commit | [S1a](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/s1a-plugin-in-an-existing-module.md), or the `mf-new-plugin` skill (the `vc-frontend` plugin of the VirtoCommerce/ai-tools marketplace) |
| Run it in a local storefront | [S2](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/s2-develop-locally.md) |
| Add a route, menu entry or slot | [declarations](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/declarations.md) |
| Move to a new facade version | [S4](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/s4-upgrade-a-plugin.md) |
| It does not show up, or fails | [S7](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/s7-missing-or-broken.md) |
| Anything else | [the scenarios](https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/scenarios/README.md) |
