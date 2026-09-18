/**
 * serializer.js — REFERENCE serializer (SQL mart -> dashboard JSON).
 *
 * In production this is where you turn the SQL views (dashboard_mart.sql) into the
 * per-project JSON contract: run the views, then convert units (₹Cr / ₹L / ₹) and
 * format dates. For this mock we read db.json, which already holds assembled data,
 * and simply demonstrate the SHAPE + the unit/format hooks you would implement.
 */
const CR = 1e7, LAKH = 1e5;
const toCr  = (rupees) => Math.round((rupees / CR) * 100) / 100;   // v_project_totals etc.
const toL   = (rupees) => Math.round((rupees / LAKH) * 100) / 100; // PO/material values
const ddmmyy = (d) => d; // TODO: format a real DATE -> 'DD-MM-YY' here

/** Build the whole payload. Swap the db read for SQL queries + the map below. */
function buildPayload(db) {
  // db.data is already assembled for the demo. A real impl would look like:
  //   const totals = await sql(v_project_totals, {project_id});
  //   return { budget: toCr(totals.budget), committed: toCr(totals.committed), ... }
  return db; // passthrough for the mock
}

/** Return a single project's data object. */
function buildProject(db, id) {
  const d = db.data[id];
  if (!d) return null;
  // Example of where conversions/formatting would live:
  //   d.totals.budget = toCr(rowsFromSql.budget_base_rupees);
  return d;
}

module.exports = { buildPayload, buildProject, toCr, toL, ddmmyy };
