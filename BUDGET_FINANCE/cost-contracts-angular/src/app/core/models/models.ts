/**
 * Dashboard data contract (mirrors Dashboard_Data_Schema_Spec §6 and the SQL views).
 * These interfaces ARE the schema the Angular app consumes.
 */
export interface DashboardPayload {
  entity: Entity;
  projects: ProjectCard[];
  data: Record<string, ProjectData>;   // keyed by project_id
}
export interface Entity { name: string; group: string; gst?: string; }

export interface ProjectCard {
  id: string; name: string; loc: string; stage: string; isReal: boolean;
  budget: number; committed: number; billed: number; available: number;  // ₹Cr
  retention: number; vendors: number; wos: number; alerts: number;
  boqDesign: number; boqOrder: number;
}


export interface ProjectData {
  project: ProjectMeta;
  totals: Totals;
  budget: BudgetRow[];                         // 9-series, grouped by grp
  budgetTree: Record<string, BudgetParent>;    // key = 9-series code
  wos: WorkOrder[];
  woLines: Record<string, PoWoLine[]>;         // key = ref_no
  pos: PurchaseOrder[];
  poLines: Record<string, PoWoLine[]>;
  asnHeaders: Record<string, AsnHeader[]>;     // key = po/wo ref_no
  asnLines: Record<string, AsnLine[]>;         // key = asn_no
  invoices: Invoice[];
  vendors:Vendor[];
  invLines: Record<string, InvoiceLine[]>;     // key = invoice no
  vendorPerf: VendorPerf[];
  vendorInv: Record<string, VendorInvoice[]>;  // key = vendor name
  boq: BoqRow[];
  boqTot: { design: number; order: number };
  retention?: RetentionRow[];
  cr?: any[];
  woDetails: WoDetail[];
  billedDetails:BilledDetail[]
}

/** Legacy DATA.vendors[] row — exactly the shape rVendors() consumes. */
export interface Vendor {
  name: string;        // v.name
  type: string;        // v.type (sub-label under name)
  cat: string;         // v.cat ('Contractor' | 'Material' | ...)
  onboard: string;     // v.onboard ('Yes' | 'No')
  wo: number;          // v.wo (WO value ₹Cr — NOT a count)
  billed: number;      // v.billed
  outstanding: number; // v.outstanding (negative = credit)
  retention: number;   // v.retention
  asn: number;         // v.asn
  inv: number;         // v.inv
}

export interface BilledDetail {
  no: string;
  status: string;
  date: string;
  acct: string;
  amt: number;
}
export interface WoDetail {
  no: string;
  date: string;
  status: string;
  vendor: string;
  item: string;
  desc: string;
  unit: string;
  qty: number;
  rate: number;
  amt: number;
  bqty: number;
  bamt: number;
  code: string;
}

export interface ProjectMeta { name: string; loc: string; entity: string; group: string; id: string; area?: number | null; }

export interface Counts { wo:number; poMat?:number; poDept?:number; po?:number; asn:number; inv:number; vendor:number; }
export interface Totals {
  budget:number; committed:number; woBilled:number; directBilled:number; balance:number; available:number;
  unapproved?:number; unposted?:number; retentionHeld?:number;
  poValueMat?:number; poValueDept?:number;
  invTotal?:number; invPaid?:number; invBal?:number;
  vendorCount?:number; vendorOrderValue?:number; vendorBilled?:number; vendorPaid?:number;
  vendorRetention?:number; vendorContractors?:number; vendorCats?:Record<string,number>;
  counts: Counts;
}

/** Budget & Finance — 9-series row (₹Cr). */
export interface BudgetRow {
  grp:string; code:string; desc:string;
  A:number; B:number; C:number; D:number; E:number;
  unappr?:number; unposted?:number; avail:number;
}
export interface BudgetParent { desc:string; children: BudgetChild[]; }
export interface BudgetChild { code:string; desc:string; A:number; B:number; C:number; D:number; E:number; F:number; }

/** Work Order (amount ₹Cr). */
export interface WorkOrder {
  no:string; glDate:string; dept:string; vendor:string; amount:number;
  ret:number; wctRet:number|null; retDate:string; createdBy:string;
  appr:string; billStatus:string;
  asnNo:string|null; asnDate:string|null; asnStatus:string|null; memo?:string;
}
/** Purchase Order (val ₹Lakh). cat = Material | Department */
export interface PurchaseOrder {
  no:string; cat:'Material'|'Department'; glDate:string; dept:string; supplier:string; item:string;
  val:number; ret:number; wctRet:number|null; createdBy:string; status:string; appr:string;
  grnNo:string|null; grnDate:string|null; grnStatus:string|null; memo?:string;
}
/** Shared PO/WO item line. */
export interface PoWoLine {
  item:string; desc:string; unit:string; qty:number; rate:number; amt:number;
  tax:number; bqty:number; wpRet:number|null; code:string; cdesc:string;
}

/** ASN / GRN header + line (two-level). */
export interface AsnHeader {
  asnNo:string; challan:string; date:string; appDate:string; status:string;
  asnQty:number; apprQty:number; billQty:number; draftInv:string; responsible:string; lineCount:number;
}
export interface AsnLine {
  docNo:string; draftInv:string; vendBill:string; asnQty:number; apprQty:number; billQty:number; responsible:string;
}

/** Invoice (amounts full ₹). paid/balance derived from status. */
export interface Invoice {
  no:string; vendor:string; glDate:string; dept:string; type:string;
  appr:string; payStatus:string; amt:number; paid:number; balance:number;
  ret:number; wopo:string; memo:string;
}
export interface InvoiceLine { item:string; desc:string; qty:number; rate:number; amt:number; tax:number; code:string; cdesc:string; }

/** Vendor performance (₹Cr) + per-vendor invoices (full ₹). */
export interface VendorPerf {
  name:string; cat:string; orders:number; orderValue:number; billed:number; paid:number; balance:number;
  invoices:number; retention:number; grn:number; billProgress:number; apprPct:number; rejBills:number; 
}
export interface VendorInvoice {
  no:string; glDate:string; dept:string; appr:string; payStatus:string; type:string;
  amt:number; paid:number; balance:number; wopo:string;
}

export interface BoqRow { code:string; desc:string; u:string; dq:number; oq:number; dr:number; or_:number; bv:number; wv:number; }
export interface RetentionRow { vendor:string; bills:number; held:number; released:number; due:string; hold:string; }

export type Scope = 'project' | 'entity';


// core/models/models.ts

export interface ProjectSummary {
  id: string;
  name: string;
  stage: string;
  sample: boolean;
  budget: number;      // in Cr
  committed: number;   // in Cr
  billed: number;      // in Cr
  available: number;   // in Cr
  pct: number;         // percentage committed
}

export interface BudgetRow {
  code: string;
  desc: string;
  grp: string;
  A: number;  // Budget
  B: number;  // Committed
  C: number;  // Billed part 1
  D: number;  // Billed part 2
  E: number;  // Other
  avail: number;
   unappr?:number; unposted?:number;
}

export interface Project {
  id: string;
  name: string;
  stage?: string;
  sample?: boolean;
  area?: number;
}

export interface BudgetData {
  project?: Project;
  budget: BudgetRow[];
  budgetTree: { [key: string]: { children: BudgetRow[] } };
}