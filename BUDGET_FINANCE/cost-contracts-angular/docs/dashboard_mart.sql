-- =====================================================================
--  COST & CONTRACTS COCKPIT — DASHBOARD DATA MART (DDL)
--  Companion to Dashboard_Data_Schema_Spec.md
--
--  Populate the base (dim_/fact_) tables from your SQL DB; the v_ views
--  compute the dashboard-ready structures. Amounts stored in BASE RUPEES;
--  the serialization layer converts to ₹Cr / ₹L / ₹ per the spec (§2.1).
--
--  ANSI-ish SQL. Adjust identifier quoting / data types to your engine
--  (SQL Server: DATETIME2/BIT; Postgres: BOOLEAN/NUMERIC; MySQL: DECIMAL).
-- =====================================================================

-- ---------- DIMENSIONS ------------------------------------------------

CREATE TABLE dim_entity (
  entity_id     VARCHAR(16)  PRIMARY KEY,
  name          VARCHAR(160) NOT NULL,
  grp           VARCHAR(120),
  gst           VARCHAR(20)
);

CREATE TABLE dim_project (
  project_id    VARCHAR(16)  PRIMARY KEY,
  entity_id     VARCHAR(16)  NOT NULL REFERENCES dim_entity(entity_id),
  name          VARCHAR(160) NOT NULL,
  location      VARCHAR(120),
  stage         VARCHAR(60),
  is_real       BOOLEAN      NOT NULL DEFAULT FALSE
);

-- Chart of budget accounts (9-series parents, 7-series children) + parent map
CREATE TABLE dim_budget_code (
  code          VARCHAR(12)  PRIMARY KEY,
  description   VARCHAR(160),
  series        CHAR(1),                       -- '9' parent, '7' child
  rollup_group  VARCHAR(120),
  parent_code   VARCHAR(12),                   -- 9-series parent for a 7-series row
  alloc_pct     DECIMAL(9,4)                   -- child % of parent budget
);

CREATE TABLE dim_vendor (
  vendor_id     VARCHAR(24)  PRIMARY KEY,
  vendor_name   VARCHAR(160) NOT NULL,
  category      VARCHAR(16),                   -- derived (see v_vendor_perf)
  pan           VARCHAR(16),
  gst           VARCHAR(20)
);

-- ---------- FACTS (populated by ETL) ---------------------------------

-- 9-series budget (Budget & Finance main table). One row per project x code.
CREATE TABLE fact_budget_finance (
  project_id         VARCHAR(16) NOT NULL REFERENCES dim_project(project_id),
  code               VARCHAR(12) NOT NULL,     -- 9-series
  rollup_group       VARCHAR(120),
  description        VARCHAR(160),
  budget_amt         DECIMAL(18,2) DEFAULT 0,  -- A
  wopo_issued        DECIMAL(18,2) DEFAULT 0,  -- B
  wopo_billed        DECIMAL(18,2) DEFAULT 0,  -- C
  billed_wo_powo_jv  DECIMAL(18,2) DEFAULT 0,  -- D  (was "Direct Billed")
  wopo_balance       DECIMAL(18,2) DEFAULT 0,  -- E = B - C
  unapproved_wopo    DECIMAL(18,2) DEFAULT 0,
  unposted_direct    DECIMAL(18,2) DEFAULT 0,
  budget_available   DECIMAL(18,2) DEFAULT 0,  -- F = A - B - E
  PRIMARY KEY (project_id, code)
);

-- 7-series breakdown (budget drill). One row per project x 7-series code.
CREATE TABLE fact_budget_child (
  project_id         VARCHAR(16) NOT NULL,
  parent_code        VARCHAR(12) NOT NULL,     -- 9-series
  code               VARCHAR(12) NOT NULL,     -- 7-series
  description        VARCHAR(160),
  budget_amt         DECIMAL(18,2) DEFAULT 0,  -- A
  wopo_issued        DECIMAL(18,2) DEFAULT 0,  -- B
  wopo_billed        DECIMAL(18,2) DEFAULT 0,  -- C
  billed_wo_powo_jv  DECIMAL(18,2) DEFAULT 0,  -- D
  wopo_balance       DECIMAL(18,2) DEFAULT 0,  -- E
  budget_available   DECIMAL(18,2) DEFAULT 0,  -- F
  PRIMARY KEY (project_id, code)
);

-- Work Orders + Purchase Orders (doc_type splits WO tab vs PO tab).
CREATE TABLE fact_powo_header (
  project_id             VARCHAR(16) NOT NULL,
  ref_no                 VARCHAR(40) NOT NULL,      -- e.g. PO/8/000468/23-24  (JOIN KEY)
  doc_type               VARCHAR(16) NOT NULL,      -- Work Order | Material | Department
  gl_date                DATE,
  invoice_date           DATE,
  department             VARCHAR(80),
  vendor_name            VARCHAR(160),
  memo                   VARCHAR(400),
  amount                 DECIMAL(18,2) DEFAULT 0,
  created_by             VARCHAR(80),
  approval_status        VARCHAR(24),
  status                 VARCHAR(40),               -- billing status
  retention_pct          DECIMAL(6,2),
  wct_retention_pct      DECIMAL(6,2),              -- NULL until export fixed (spec §7.1)
  retention_release_date DATE,
  quantity_billed        DECIMAL(18,3),
  PRIMARY KEY (project_id, ref_no)
);

-- PO/WO item lines (drill: Item Lines). JOIN to header on ref_no.
CREATE TABLE fact_powo_line (
  project_id           VARCHAR(16) NOT NULL,
  ref_no               VARCHAR(40) NOT NULL,
  line_no              INT,
  item_code            VARCHAR(40),
  description          VARCHAR(400),
  unit                 VARCHAR(24),
  quantity             DECIMAL(18,3),
  quantity_billed      DECIMAL(18,3),
  item_rate            DECIMAL(18,4),
  amount               DECIMAL(18,2),
  tax_amount           DECIMAL(18,2),
  wp_retention_pct     DECIMAL(6,2),               -- NULL until export fixed (spec §7.1)
  expense_account_code VARCHAR(12),                -- 7-series -> budget link
  expense_account_desc VARCHAR(160)
);

-- ASN / GRN headers (same table serves WO=ASN and PO=GRN).
CREATE TABLE fact_asn_header (
  project_id        VARCHAR(16) NOT NULL,
  asn_no            VARCHAR(24) NOT NULL,          -- e.g. ASN2564
  po_ref_no         VARCHAR(40),                   -- strip 'Purchase Order #' (JOIN KEY)
  challan_no        VARCHAR(40),
  asn_challan_date  DATE,
  application_date  DATE,
  approval_status   VARCHAR(24),
  department        VARCHAR(80),
  responsible       VARCHAR(80),
  amount            DECIMAL(18,2),                 -- DO NOT SUM as a value (spec §7.2)
  draft_inv_id      VARCHAR(24),
  PRIMARY KEY (project_id, asn_no)
);

-- ASN / GRN lines (two-level drill). De-duplicate exact rows (spec §4.6).
CREATE TABLE fact_asn_line (
  project_id        VARCHAR(16) NOT NULL,
  asn_no            VARCHAR(24) NOT NULL,
  po_ref_no         VARCHAR(40),
  doc_number        VARCHAR(40),
  draft_inv_id      VARCHAR(24),
  vendor_bill_link  VARCHAR(60),
  asn_qty           DECIMAL(18,3),
  approved_qty      DECIMAL(18,3),
  billed_qty        DECIMAL(18,3),
  responsible       VARCHAR(80)
);

-- Invoices (Invoices & RA).
CREATE TABLE fact_bill_header (
  project_id       VARCHAR(16) NOT NULL,
  transaction_no   VARCHAR(40) NOT NULL,          -- JOIN KEY to bill_line
  ref_no           VARCHAR(60),                   -- vendor invoice no
  bill_type        VARCHAR(40),                   -- Vendor Invoice | Retention Invoice | ...
  gl_date          DATE,
  invoice_date     DATE,
  department       VARCHAR(80),
  vendor_name      VARCHAR(160),
  memo             VARCHAR(400),
  amount           DECIMAL(18,2),                 -- Bill Amount
  approval_status  VARCHAR(24),
  status           VARCHAR(24),                   -- payment status
  retention_pct    DECIMAL(6,2),
  created_from_ref VARCHAR(40),                   -- WO/PO ref (strip prefix); drives C vs D
  amount_paid      DECIMAL(18,2),                 -- NULL -> derive from status (spec §7.3)
  amount_balance   DECIMAL(18,2),                 -- NULL -> derive from status
  PRIMARY KEY (project_id, transaction_no)
);

-- Invoice item lines (drill: Invoice Item Details).
CREATE TABLE fact_bill_line (
  project_id           VARCHAR(16) NOT NULL,
  transaction_no       VARCHAR(40) NOT NULL,
  line_no              INT,
  item_code            VARCHAR(40),
  description          VARCHAR(400),
  quantity             DECIMAL(18,3),
  item_rate            DECIMAL(18,4),
  amount               DECIMAL(18,2),
  tax_amount           DECIMAL(18,2),
  expense_account_code VARCHAR(12),               -- 7-series -> budget (C vs D split)
  expense_account_desc VARCHAR(160),
  created_from_ref     VARCHAR(40)                -- WO/PO ref (C if present, D if null)
);

-- BOQ comparison.
CREATE TABLE fact_boq (
  project_id   VARCHAR(16) NOT NULL,
  job_code     VARCHAR(40),
  expense_code VARCHAR(60),
  unit         VARCHAR(24),
  boq_qty      DECIMAL(18,3),                     -- design
  wo_qty       DECIMAL(18,3),                     -- ordered
  ssr_rate     DECIMAL(18,4),                     -- design rate
  wo_rate      DECIMAL(18,4),                     -- ordered rate
  boq_value    DECIMAL(18,2),                     -- design value
  wo_value     DECIMAL(18,2)                      -- ordered value
);

-- Retention ledger (optional; can be a view over bills).
CREATE TABLE fact_retention (
  project_id   VARCHAR(16) NOT NULL,
  vendor_name  VARCHAR(160),
  bills_count  INT,
  held_amt     DECIMAL(18,2),
  released_amt DECIMAL(18,2),
  release_due  VARCHAR(40),
  payment_hold BOOLEAN
);

-- Change requests / extra items (populate when export exists).
CREATE TABLE fact_change_request (
  project_id VARCHAR(16) NOT NULL,
  cr_no      VARCHAR(40),
  title      VARCHAR(200),
  cr_type    VARCHAR(40),
  vendor     VARCHAR(160),
  qty        DECIMAL(18,3),
  rate       DECIMAL(18,4),
  value      DECIMAL(18,2),
  status     VARCHAR(40),
  raised     DATE
);

-- Helpful indexes for the joins that drive the drills.
CREATE INDEX ix_powo_line_ref  ON fact_powo_line (project_id, ref_no);
CREATE INDEX ix_bill_line_txn  ON fact_bill_line (project_id, transaction_no);
CREATE INDEX ix_asn_hdr_po     ON fact_asn_header(project_id, po_ref_no);
CREATE INDEX ix_asn_line_asn   ON fact_asn_line  (project_id, asn_no);
CREATE INDEX ix_bill_hdr_cf    ON fact_bill_header(project_id, created_from_ref);
CREATE INDEX ix_powo_line_code ON fact_powo_line (project_id, expense_account_code);
CREATE INDEX ix_bill_line_code ON fact_bill_line (project_id, expense_account_code);


-- =====================================================================
--  DERIVED VIEWS  (computed — do not hand-populate)
-- =====================================================================

-- C vs D split per 7-series code (WO/PO-billed vs billed-without-PO/WO/JV).
CREATE VIEW v_billed_split AS
SELECT project_id,
       expense_account_code AS code,
       SUM(CASE WHEN created_from_ref IS NOT NULL AND created_from_ref <> ''
                THEN amount ELSE 0 END) AS wopo_billed_c,
       SUM(CASE WHEN created_from_ref IS NULL OR created_from_ref = ''
                THEN amount ELSE 0 END) AS billed_wo_powojv_d
FROM fact_bill_line
GROUP BY project_id, expense_account_code;

-- Invoice paid / balance (status-derived until real fields exist — spec §5.5, §7.3).
CREATE VIEW v_bill_paidbal AS
SELECT project_id, transaction_no, amount,
       COALESCE(amount_paid,
                CASE WHEN status LIKE 'Paid%' THEN amount ELSE 0 END)            AS paid,
       COALESCE(amount_balance,
                CASE WHEN status IN ('Open','Pending Approval') THEN amount ELSE 0 END) AS balance
FROM fact_bill_header;

-- Project totals (the `totals` object). Amounts here in BASE ₹; convert in serialization.
CREATE VIEW v_project_totals AS
SELECT b.project_id,
       SUM(b.budget_amt)         AS budget,
       SUM(b.wopo_issued)        AS committed,
       SUM(b.wopo_billed)        AS wo_billed,      -- C
       SUM(b.billed_wo_powo_jv)  AS direct_billed,  -- D
       SUM(b.wopo_balance)       AS balance,        -- E
       SUM(b.budget_available)   AS available       -- F
FROM fact_budget_finance b
GROUP BY b.project_id;

-- Secondary totals (counts + invoice + PO split + retention).
CREATE VIEW v_project_counts AS
SELECT p.project_id,
  (SELECT COUNT(*) FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Work Order') AS wo_cnt,
  (SELECT COUNT(*) FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Material')   AS po_mat_cnt,
  (SELECT COUNT(*) FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Department') AS po_dept_cnt,
  (SELECT COUNT(*) FROM fact_asn_header  a WHERE a.project_id=p.project_id)                              AS asn_cnt,
  (SELECT COUNT(*) FROM fact_bill_header f WHERE f.project_id=p.project_id)                              AS inv_cnt,
  (SELECT COUNT(DISTINCT vendor_name) FROM fact_powo_header h WHERE h.project_id=p.project_id)           AS vendor_cnt_orders,
  (SELECT COALESCE(SUM(amount),0) FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Material')   AS po_mat_val,
  (SELECT COALESCE(SUM(amount),0) FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Department') AS po_dept_val,
  (SELECT COALESCE(SUM(h.amount*COALESCE(h.retention_pct,0)/100),0)
     FROM fact_powo_header h WHERE h.project_id=p.project_id AND h.doc_type='Work Order')                AS retention_held
FROM dim_project p;

-- Invoice roll-up.
CREATE VIEW v_invoice_totals AS
SELECT project_id,
       SUM(amount)  AS inv_total,
       SUM(paid)    AS inv_paid,
       SUM(balance) AS inv_bal
FROM v_bill_paidbal
GROUP BY project_id;

-- Budget tree (parent + children) — serialize into budgetTree{}.
CREATE VIEW v_budget_tree AS
SELECT c.project_id, c.parent_code, p.description AS parent_desc,
       c.code, c.description AS child_desc,
       c.budget_amt AS a, c.wopo_issued AS b, c.wopo_billed AS c_,
       c.billed_wo_powo_jv AS d, c.wopo_balance AS e, c.budget_available AS f
FROM fact_budget_child c
LEFT JOIN fact_budget_finance p
       ON p.project_id=c.project_id AND p.code=c.parent_code;

-- Vendor performance (+ derived category). Amounts BASE ₹.
CREATE VIEW v_vendor_perf AS
WITH ord AS (
  SELECT project_id, vendor_name,
         COUNT(*) AS orders, SUM(amount) AS order_value,
         MAX(CASE WHEN doc_type='Work Order' THEN 1 ELSE 0 END) AS has_wo,
         MAX(CASE WHEN doc_type='Material'   THEN 1 ELSE 0 END) AS has_mat,
         MAX(CASE WHEN doc_type='Department' THEN 1 ELSE 0 END) AS has_dept,
         SUM(CASE WHEN doc_type='Work Order' THEN amount*COALESCE(retention_pct,0)/100 ELSE 0 END) AS retention
  FROM fact_powo_header GROUP BY project_id, vendor_name
),
bil AS (
  SELECT h.project_id, h.vendor_name,
         COUNT(*) AS invoices, SUM(h.amount) AS billed,
         SUM(pb.paid) AS paid,
         SUM(CASE WHEN h.status LIKE 'Reject%' OR h.status='Cancelled' THEN 1 ELSE 0 END) AS rej_bills,
         SUM(CASE WHEN h.approval_status='Approved' THEN 1 ELSE 0 END) AS appr_bills
  FROM fact_bill_header h
  JOIN v_bill_paidbal pb ON pb.project_id=h.project_id AND pb.transaction_no=h.transaction_no
  GROUP BY h.project_id, h.vendor_name
),
grn AS (
  SELECT project_id, vendor_name, COUNT(DISTINCT asn_no) AS grn
  FROM fact_asn_header a
  JOIN fact_powo_header h ON h.project_id=a.project_id AND h.ref_no=a.po_ref_no
  GROUP BY project_id, h.vendor_name
)
SELECT COALESCE(o.project_id,b.project_id) AS project_id,
       COALESCE(o.vendor_name,b.vendor_name) AS vendor_name,
       CASE WHEN o.has_wo=1 THEN 'Contractor'
            WHEN o.has_mat=1 THEN 'Material'
            WHEN o.has_dept=1 THEN 'Service'
            ELSE 'Other' END AS category,
       COALESCE(o.orders,0)       AS orders,
       COALESCE(o.order_value,0)  AS order_value,
       COALESCE(b.billed,0)       AS billed,
       COALESCE(b.paid,0)         AS paid,
       COALESCE(b.billed,0)-COALESCE(b.paid,0) AS balance,
       COALESCE(b.invoices,0)     AS invoices,
       COALESCE(o.retention,0)    AS retention,
       COALESCE(g.grn,0)          AS grn,
       CASE WHEN COALESCE(o.order_value,0)>0
            THEN ROUND(COALESCE(b.billed,0)/o.order_value*100) ELSE 0 END AS bill_progress,
       CASE WHEN COALESCE(b.invoices,0)>0
            THEN ROUND(b.appr_bills*100.0/b.invoices) ELSE 0 END AS appr_pct,
       COALESCE(b.rej_bills,0)    AS rej_bills
FROM ord o
FULL OUTER JOIN bil b ON b.project_id=o.project_id AND b.vendor_name=o.vendor_name
LEFT JOIN grn g ON g.project_id=COALESCE(o.project_id,b.project_id)
                AND g.vendor_name=COALESCE(o.vendor_name,b.vendor_name);
-- (MySQL: emulate FULL OUTER JOIN with LEFT UNION RIGHT.)

-- BOQ totals.
CREATE VIEW v_boq_totals AS
SELECT project_id, SUM(boq_value) AS design, SUM(wo_value) AS order_val
FROM fact_boq GROUP BY project_id;

-- Latest ASN/GRN per PO/WO (the "Last ASN/GRN" columns).
CREATE VIEW v_last_asn AS
SELECT h.project_id, h.po_ref_no, h.asn_no, h.challan_no, h.asn_challan_date, h.approval_status
FROM fact_asn_header h
JOIN (
  SELECT project_id, po_ref_no, MAX(asn_challan_date) AS max_dt
  FROM fact_asn_header GROUP BY project_id, po_ref_no
) m ON m.project_id=h.project_id AND m.po_ref_no=h.po_ref_no AND m.max_dt=h.asn_challan_date;

-- Project card (Consolidated roll-up cards + switcher). Convert to ₹Cr in serialization.
CREATE VIEW v_project_card AS
SELECT p.project_id, p.name, p.location, p.stage, p.is_real,
       t.budget, t.committed, (t.wo_billed + t.direct_billed) AS billed, t.available,
       c.retention_held AS retention, c.vendor_cnt_orders AS vendors, c.wo_cnt AS wos,
       bq.design AS boq_design, bq.order_val AS boq_order
FROM dim_project p
LEFT JOIN v_project_totals t ON t.project_id=p.project_id
LEFT JOIN v_project_counts c ON c.project_id=p.project_id
LEFT JOIN v_boq_totals   bq ON bq.project_id=p.project_id;

-- =====================================================================
--  END. See Dashboard_Data_Schema_Spec.md §6 for JSON serialization
--  (unit conversion ₹Cr/₹L/₹, date formatting, and exact JSON keys).
-- =====================================================================
