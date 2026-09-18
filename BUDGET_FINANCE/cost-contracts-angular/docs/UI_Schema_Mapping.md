# UI ↔ Schema mapping (Angular)

Each feature component reads `DataService.current()` (a `ProjectData`) and renders one slice of it.

| Route / component | Reads from `ProjectData` | Model interfaces |
|---|---|---|
| `overview` OverviewComponent | `totals`, `projects[]` (+ `entitySum`) | `Totals`, `ProjectCard` |
| `budget` BudgetComponent | `budget[]`, `budgetTree{}`, `project.area` | `BudgetRow`, `BudgetParent`, `BudgetChild` |
| `work-orders` WorkOrdersComponent | `wos[]`, `woLines{}`, `asnHeaders{}`, `asnLines{}` | `WorkOrder`, `PoWoLine`, `AsnHeader`, `AsnLine` |
| `purchase-orders` (stub) | `pos[]`, `poLines{}`, `asnHeaders{}`, `asnLines{}` | `PurchaseOrder`, `PoWoLine`, `AsnHeader/Line` |
| `asn-grn` (stub) | `asnHeaders{}`, `asnLines{}` | `AsnHeader`, `AsnLine` |
| `invoices` (stub) | `invoices[]`, `invLines{}` | `Invoice`, `InvoiceLine` |
| `vendors` (stub) | `vendorPerf[]`, `vendorInv{}` | `VendorPerf`, `VendorInvoice` |
| `boq` (stub) | `boq[]`, `boqTot` | `BoqRow` |
| `risk` (stub) | `totals`, `budget[]`, `boqTot`, `vendorPerf[]` | derived |

**Join keys inside the payload** (already resolved server-side, mirrored as object keys):
`woLines[ref_no]`, `poLines[ref_no]`, `asnHeaders[po/wo ref]`, `asnLines[asn_no]`,
`invLines[invoice_no]`, `vendorInv[vendor_name]`, `budgetTree[9-series].children[]`.

The **models in `src/app/core/models/models.ts` are the schema** — they match the SQL views
(`dashboard_mart.sql`) and the JSON contract 1:1. To move from the bundled sample file to a live API,
change one line in `DataService` (see README §Serving).
