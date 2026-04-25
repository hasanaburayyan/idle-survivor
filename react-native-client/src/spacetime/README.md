# SpacetimeDB Bindings

The `generated/` folder is created by running:

```bash
npm run generate
```

This calls `spacetime generate --lang typescript` against the C# module in `../spacetimedb/`.
You must re-run it any time the backend schema changes.

## Prerequisites

- SpacetimeDB CLI installed: https://spacetimedb.com/install
- Backend compiled at least once (`spacetime publish` or a local `spacetime start`)
