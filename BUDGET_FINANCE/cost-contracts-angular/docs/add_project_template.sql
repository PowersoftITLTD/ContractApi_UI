-- =====================================================================
--  ADD A NEW PROJECT TO CockpitMart
--  Copy this file, replace the @Project* values, run section by section.
--  Nothing in the app or API changes — the dashboard picks the project up
--  from the data alone (project_id is the scope key everywhere).
-- =====================================================================

USE CockpitMart;
GO

------------------------------------------------------------------------
-- 0. Parameters for the new project  (EDIT THESE)
------------------------------------------------------------------------
DECLARE @ProjectId   VARCHAR(16)  = 'TWR';                     -- short stable id, e.g. FWG, TWR
DECLARE @EntityId    VARCHAR(16)  = 'JPPL';                    -- existing dim_entity
DECLARE @Name        VARCHAR(160) = 'Twenty Five South — Tower C';
DECLARE @Location    VARCHAR(120) = 'Mahalaxmi, Mumbai';
DECLARE @Stage       VARCHAR(60)  = 'Superstructure';
DECLARE @AreaSqft    INT          = 347000;                    -- saleable area → drives per-SqFt rates (NULL if unknown)

------------------------------------------------------------------------
-- 1. Register the project (dim_project)
------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dim_project WHERE project_id = @ProjectId)
  INSERT INTO dim_project (project_id, entity_id, name, location, stage, is_real /*, area if you added the column*/)
  VALUES (@ProjectId, @EntityId, @Name, @Location, @Stage, 1);
ELSE
  UPDATE dim_project SET name=@Name, location=@Location, stage=@Stage WHERE project_id=@ProjectId;
-- NOTE: if you keep project.area in the payload, store @AreaSqft on dim_project (add an `area INT NULL` column)
--       or in a small dim_project_ext table; the serializer reads it into project.area.

------------------------------------------------------------------------
-- 2. Budget chart (dim_budget_code) — usually SHARED across projects.
--    Only insert codes that don't already exist (new expense heads).
------------------------------------------------------------------------
-- INSERT INTO dim_budget_code (code, description, series, rollup_group, parent_code, alloc_pct)
-- SELECT ... FROM raw.<parent_child_source> WHERE code NOT IN (SELECT code FROM dim_budget_code);

------------------------------------------------------------------------
-- 3. Load the project's facts.
--    RECOMMENDED: run the ETL procs filtered to this project (they read the
--    raw NetSuite exports for the project and INSERT into fact_* with project_id).
--    Ensure each raw export is tagged/joined to @ProjectId in the proc.
------------------------------------------------------------------------
-- EXEC etl.Load_Budget_Finance  @ProjectId;
-- EXEC etl.Load_Budget_Child    @ProjectId;
-- EXEC etl.Load_PoWo_Header     @ProjectId;
-- EXEC etl.Load_PoWo_Line       @ProjectId;
-- EXEC etl.Load_Asn_Header      @ProjectId;
-- EXEC etl.Load_Asn_Line        @ProjectId;
-- EXEC etl.Load_Bill_Header     @ProjectId;
-- EXEC etl.Load_Bill_Line       @ProjectId;
-- EXEC etl.Load_Boq             @ProjectId;
-- EXEC etl.Load_Dim_Vendor      @ProjectId;
--
-- (If you don't yet have per-project ETL params, the simplest interim path is a
--  staging table per export, then INSERT ... SELECT ... WHERE project_id = @ProjectId.)

------------------------------------------------------------------------
-- 4. Grant access (dim_user_project) — who can see this project
------------------------------------------------------------------------
-- INSERT INTO dim_user_project (user_login, project_id) VALUES
--   ('HUBTOWN\\manish', @ProjectId),
--   ('HUBTOWN\\accountsteam', @ProjectId);   -- or map an AD group name

------------------------------------------------------------------------
-- 5. VERIFY (must reconcile before you consider the project live)
------------------------------------------------------------------------
-- Totals the dashboard will show, straight from the mart (base ₹; divide by 1e7 for ₹Cr):
SELECT project_id,
       SUM(budget_amt)/1e7        AS budget_cr,
       SUM(wopo_issued)/1e7       AS committed_cr,
       SUM(wopo_billed)/1e7       AS wopo_billed_C_cr,
       SUM(billed_wo_powo_jv)/1e7 AS billed_woPOWO_D_cr,
       SUM(budget_available)/1e7  AS available_cr
FROM fact_budget_finance WHERE project_id = @ProjectId GROUP BY project_id;

-- Row counts sanity
SELECT 'powo_header' t, COUNT(*) n FROM fact_powo_header WHERE project_id=@ProjectId
UNION ALL SELECT 'bill_header', COUNT(*) FROM fact_bill_header WHERE project_id=@ProjectId
UNION ALL SELECT 'asn_header',  COUNT(*) FROM fact_asn_header  WHERE project_id=@ProjectId
UNION ALL SELECT 'boq',         COUNT(*) FROM fact_boq         WHERE project_id=@ProjectId;

-- ✔ Compare budget_cr / committed_cr / billed against the NetSuite Budget Finance report.
--   Variance must be ~0 (±₹0.01 Cr) before go-live. Then it appears automatically in the
--   project switcher and the Consolidated roll-up — no app or API change needed.
GO
