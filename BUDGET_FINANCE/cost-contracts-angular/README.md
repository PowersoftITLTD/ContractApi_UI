# Cost & Contracts Cockpit — Angular

A component-based Angular 17 (standalone) rebuild of the Cost & Contracts Cockpit — **all tabs built**,
**two projects** for the Consolidated roll-up, and a **tiny mock API** so you can run it live.

## Run (bundled data — no backend)
```bash
npm install            # Node 18.19+ ; also: npm i -g @angular/cli
npm start              # ng serve -> http://localhost:4200
```
Loads `src/assets/data/payload.json` (Hubtown Seasons – F Wing = real; Twenty Five South – Tower C = trimmed demo).

## Run against the mock API (live path)
```bash
cd mock-server && npm install && npm start     # http://localhost:3000   (terminal 1)
# set useApi:true in src/app/core/config.ts
npm start                                        # terminal 2
```
See `mock-server/README.md`.

## Feature tabs (all implemented)
| Route | Component | Depth |
|---|---|---|
| overview | OverviewComponent | KPIs + project roll-up cards (project & consolidated) |
| budget | BudgetComponent | 3 levels: Summary → 9-series → 7-series |
| boq | BoqComponent | design vs ordered qty/rate/value variance |
| vendors | VendorsComponent | list + drawer (Performance · Orders · Invoices) |
| work-orders | WorkOrdersComponent | list + drawer (Summary · Item Lines · two-level ASN) |
| purchase-orders | PurchaseOrdersComponent | Material/Department sub-tabs + drawer (… · two-level GRN) |
| asn-grn | AsnGrnComponent | standalone two-level receipts |
| invoices | InvoicesComponent | list + drawer (Summary · Item Details) |
| risk | RiskComponent | KPIs + codes-at-risk + risk register (data-driven) |

## Reusable components (the "inbuilt" UI kit)
`<cc-kpi>` · `<cc-card>` · `<cc-sym>` (approval/billing/payment/asn) · `<cc-subtabs>` · `<cc-backbar>` · `<cc-drawer>`

## Structure
```
src/app/
  core/models/models.ts        THE SCHEMA (TS interfaces = JSON contract = SQL views)
  core/services/data.service.ts signals: projectId, scope; entity roll-up; API-or-file loader
  core/config.ts               useApi toggle + apiBase
  core/pipes/format.pipes.ts    cr | inr | rup | pctOf
  shared/                       kpi-tile · ui-card · status-symbol · sub-tabs · back-bar · drill-drawer
  layout/                       sidebar · topbar (scope toggle + project switcher)
  features/                     overview · budget · boq · vendors · work-orders ·
                                purchase-orders · asn-grn · invoices · risk
src/assets/data/payload.json    two-project sample payload
mock-server/                    Express: db.json + serializer.js + /api endpoints
docs/dashboard_mart.sql         DB structure: base tables + derived views
docs/UI_Schema_Mapping.md       component <-> payload <-> model linkage
```

## Schema · DB · linkage
- **Schema (app):** `core/models/models.ts` mirrors the JSON contract 1:1.
- **DB structure:** `docs/dashboard_mart.sql` — dims, facts, and views (`v_project_totals`, `v_budget_tree`,
  `v_billed_split` [the C-vs-D rule], `v_vendor_perf`, `v_bill_paidbal`, …).
- **Linkage:** `docs/UI_Schema_Mapping.md`.

## Verified
This project was compiled (`ng build`, Angular 17, strict templates) and run (`ng serve`) — all nine tabs
render from the two bundled projects, drill drawers open, and **Consolidated ↔ Project** rolls up both
projects, with no runtime errors. Just `npm install && npm start`.

Notes:
- Needs **Node 18.19+** and **Angular CLI 17** (`ng version` to confirm).
- **Live mode:** the mock server enables CORS for `localhost`; if you host the API elsewhere, allow your dev origin.
