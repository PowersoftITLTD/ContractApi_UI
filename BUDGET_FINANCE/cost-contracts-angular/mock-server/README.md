# Cockpit mock API

A tiny Express server that mimics the production data endpoint, so you can run the
dashboard against a live API instead of the bundled file.

## Run
```bash
cd mock-server
npm install
npm start          # http://localhost:3000
```

## Endpoints
| Method | Path | Returns |
|---|---|---|
| GET | `/api/payload` | whole payload (entity + projects[] + data{}) |
| GET | `/api/projects` | entity + project registry |
| GET | `/api/project/:id` | one project's data object |

## Point the app at it
In `../src/app/core/config.ts` set `useApi: true` (apiBase defaults to `http://localhost:3000`),
then run the Angular app in another terminal (`npm start`). The dashboard now fetches
`/api/payload` from this server.

## Going real
`serializer.js` is where the SQL mart (`../docs/dashboard_mart.sql`) becomes JSON:
run the `v_*` views, convert base ₹ to ₹Cr / ₹L, format dates, and (in production)
put this behind authentication so each user only receives their permitted projects.
`db.json` here stands in for that already-serialized output.
