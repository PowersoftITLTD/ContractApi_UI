using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Formats.Asn1;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContractBudgetApi.Model
{

    public class Root
    {
        [JsonPropertyName("entity")]
        public Entity Entity { get; set; }

        [JsonPropertyName("projects")]
        public List<ProjectSummary> Projects { get; set; }

        [JsonPropertyName("data")]
        public Dictionary<string, DetailedProjectData> Data { get; set; }
    }
    // -------------------- Entity --------------------
    public class Entity
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("group")]
        public string Group { get; set; }

        [JsonPropertyName("gst")]
        public string Gst { get; set; }

        [JsonPropertyName("entity_id")]
        public string entityId { get; set; }
    }

    // -------------------- Project Summary (top-level array) --------------------
    public class ProjectSummary
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("loc")]
        public string Loc { get; set; }

        [JsonPropertyName("stage")]
        public string Stage { get; set; }

        [JsonPropertyName("isReal")]
        public bool IsReal { get; set; }

        [JsonPropertyName("budget")]
        public decimal Budget { get; set; }

        [JsonPropertyName("committed")]
        public decimal Committed { get; set; }

        [JsonPropertyName("billed")]
        public decimal Billed { get; set; }

        [JsonPropertyName("available")]
        public decimal Available { get; set; }

        [JsonPropertyName("retention")]
        public decimal Retention { get; set; }

        [JsonPropertyName("vendors")]
        public int Vendors { get; set; }

        [JsonPropertyName("wos")]
        public int Wos { get; set; }

        [JsonPropertyName("alerts")]
        public int Alerts { get; set; }

        [JsonPropertyName("boqDesign")]
        public decimal BoqDesign { get; set; }

        [JsonPropertyName("boqOrder")]
        public decimal BoqOrder { get; set; }

        //[JsonPropertyName("totalBudget")]
        //public decimal? totalBudget { get; set; }

        //[JsonPropertyName("totalCommitted")]
        //public decimal? totalCommitted { get; set; }

        //[JsonPropertyName("totalAvailable")]
        //public decimal? totalAvailable { get; set; }

        //[JsonPropertyName("totalVendor")]
        //public decimal? totalVendor { get; set; }

        //[JsonPropertyName("totalAlert")]
        //public decimal? totalAlert { get; set; }


    }

    // -------------------- Detailed Project Data (per project id) --------------------
    public class DetailedProjectData
    {
        [JsonPropertyName("project")]
        public ProjectInfo Project { get; set; }

        [JsonPropertyName("totals")]
        public Totals Totals { get; set; }

        [JsonPropertyName("budget")]
        public List<BudgetItem> Budget { get; set; }

        [JsonPropertyName("budgetTree")]
        public Dictionary<string, BudgetTreeItem> BudgetTree { get; set; }

        [JsonPropertyName("woDetails")]
        public List<WoDetail> WoDetails { get; set; }

        [JsonPropertyName("billedDetails")]
        public Dictionary<string, List<BilledDetail>> BilledDetails { get; set; }

        [JsonPropertyName("vendors")]
        public List<VendorSummary> Vendors { get; set; }

        [JsonPropertyName("wos")]
        public List<WoSummary> Wos { get; set; }

        [JsonPropertyName("pos")]
        public List<PoSummary> Pos { get; set; }

        [JsonPropertyName("grn")]
        public List<GrnItem> Grn { get; set; }

        [JsonPropertyName("grnUnit")]
        public string GrnUnit { get; set; } = "₹ Lakh";

        [JsonPropertyName("invoices")]
        public List<InvoiceItem> Invoices { get; set; }

        [JsonPropertyName("invUnit")]
        public string InvUnit { get; set; } = "₹";

        [JsonPropertyName("retention")]
        public List<RetentionItem> Retention { get; set; }

        [JsonPropertyName("tds")]
        public List<TdsItem> Tds { get; set; }

        [JsonPropertyName("boq")]
        public List<BoqItem> Boq { get; set; }

        [JsonPropertyName("boqTot")]
        public BoqTot BoqTot { get; set; }

        [JsonPropertyName("cr")]
        public List<object> Cr { get; set; } // unknown structure, can be object

        [JsonPropertyName("woLines")]
        public Dictionary<string, List<WoLineItem>> WoLines { get; set; }

        [JsonPropertyName("poLines")]
        public Dictionary<string, List<PoLineItem>> PoLines { get; set; }

        [JsonPropertyName("asnHeaders")]
        public Dictionary<string, List<AsnHeader>> AsnHeaders { get; set; }

        [JsonPropertyName("asnLines")]
        public Dictionary<string, List<AsnLine>> AsnLines { get; set; }

        [JsonPropertyName("invLines")]
        public Dictionary<string, List<InvLineItem>> InvLines { get; set; }

        [JsonPropertyName("vendorPerf")]
        public List<VendorPerformance> VendorPerf { get; set; }

        [JsonPropertyName("vendorInv")]
        public Dictionary<string, List<VendorInvoiceSummary>> VendorInv { get; set; }

        // Additional fields found in TWR but not FWG (like poUnit, etc.) – we can add optional
        [JsonPropertyName("poUnit")]
        public string PoUnit { get; set; } = "₹ Lakh";

        [JsonPropertyName("projecttotalCount")]
        public ProjectTotalCount projectTotal { get; set; }


    }
    // -------------------- ProjectInfo --------------------
    public class ProjectInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("loc")]
        public string Loc { get; set; }

        [JsonPropertyName("entity")]
        public string Entity { get; set; }

        [JsonPropertyName("group")]
        public string Group { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("area")]
        public decimal? Area { get; set; } // nullable
    }

    // -------------------- Totals --------------------
    public class Totals
    {
        [JsonPropertyName("budget")]
        public decimal Budget { get; set; }

        [JsonPropertyName("committed")]
        public decimal Committed { get; set; }

        [JsonPropertyName("woBilled")]
        public decimal WoBilled { get; set; }

        [JsonPropertyName("directBilled")]
        public decimal DirectBilled { get; set; }

        [JsonPropertyName("balance")]
        public decimal Balance { get; set; }

        [JsonPropertyName("available")]
        public decimal Available { get; set; }

        [JsonPropertyName("retentionHeld")]
        public decimal RetentionHeld { get; set; }

        [JsonPropertyName("tdsYtd")]
        public decimal TdsYtd { get; set; }

        [JsonPropertyName("gstInput")]
        public decimal GstInput { get; set; }

        [JsonPropertyName("poValue")]
        public decimal PoValue { get; set; }

        [JsonPropertyName("grnValue")]
        public decimal GrnValue { get; set; }

        [JsonPropertyName("counts")]
        public Counts Counts { get; set; }

        [JsonPropertyName("unapproved")]
        public decimal? Unapproved { get; set; }

        [JsonPropertyName("unposted")]
        public decimal? Unposted { get; set; }

        [JsonPropertyName("poValueMat")]
        public decimal PoValueMat { get; set; }

        [JsonPropertyName("poValueSrv")]
        public decimal PoValueSrv { get; set; }

        [JsonPropertyName("woValueWO")]
        public decimal WoValueWO { get; set; }

        [JsonPropertyName("woValueDept")]
        public decimal WoValueDept { get; set; }

        [JsonPropertyName("poValueDept")]
        public decimal PoValueDept { get; set; }

        [JsonPropertyName("invPaid")]
        public decimal InvPaid { get; set; }

        [JsonPropertyName("invBal")]
        public decimal InvBal { get; set; }

        [JsonPropertyName("invTotal")]
        public decimal InvTotal { get; set; }

        [JsonPropertyName("vendorCount")]
        public int VendorCount { get; set; }

        [JsonPropertyName("vendorOrderValue")]
        public decimal VendorOrderValue { get; set; }

        [JsonPropertyName("vendorBilled")]
        public decimal VendorBilled { get; set; }

        [JsonPropertyName("vendorPaid")]
        public decimal VendorPaid { get; set; }

        [JsonPropertyName("vendorBalance")]
        public decimal VendorBalance { get; set; }

        [JsonPropertyName("vendorRetention")]
        public decimal VendorRetention { get; set; }

        [JsonPropertyName("vendorCats")]
        public VendorCats VendorCats { get; set; }

        [JsonPropertyName("vendorContractors")]
        public int VendorContractors { get; set; }
    }

    public class Counts
    {
        [JsonPropertyName("asn")]
        public int Asn { get; set; }

        [JsonPropertyName("inv")]
        public int Inv { get; set; }

        [JsonPropertyName("wo")]
        public int Wo { get; set; }

        [JsonPropertyName("po")]
        public int Po { get; set; }

        [JsonPropertyName("vendor")]
        public int Vendor { get; set; }

        [JsonPropertyName("dept")]
        public int Dept { get; set; }

        [JsonPropertyName("poMat")]
        public int PoMat { get; set; }

        [JsonPropertyName("poDept")]
        public int PoDept { get; set; }
    }

    public class VendorCats
    {
        [JsonPropertyName("contractor")]
        public int Contractor { get; set; }

        [JsonPropertyName("Other")]
        public int Other { get; set; }

        [JsonPropertyName("service")]
        public int Service { get; set; }

        [JsonPropertyName("Material")]
        public int Material { get; set; }
    }

    // -------------------- BudgetItem --------------------
    public class BudgetItem
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]

        public string? project_id { get; set; }

        [JsonPropertyName("grp")]
        public string Grp { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("A")]
        public decimal A { get; set; }

        [JsonPropertyName("B")]
        public decimal B { get; set; }

        [JsonPropertyName("C")]
        public decimal C { get; set; }

        [JsonPropertyName("D")]
        public decimal D { get; set; }

        [JsonPropertyName("E")]
        public decimal E { get; set; }

        [JsonPropertyName("unappr")]
        public decimal? Unappr { get; set; }

        [JsonPropertyName("unposted")]
        public decimal? Unposted { get; set; }

        [JsonPropertyName("avail")]
        public decimal Avail { get; set; }
    }

    // -------------------- BudgetTreeItem --------------------
    public class BudgetTreeItem
    {

        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("code")]
        [JsonIgnore]

        public string? Code { get; set; }


        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("children")]
        public List<BudgetTreeChild> Children { get; set; }
    }

    public class BudgetTreeChild
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("A")]
        public decimal? A { get; set; }

        [JsonPropertyName("B")]
        public decimal? B { get; set; }

        [JsonPropertyName("C")]
        public decimal? C { get; set; }

        [JsonPropertyName("D")]
        public decimal? D { get; set; }

        [JsonPropertyName("E")]
        public decimal? E { get; set; }

        [JsonPropertyName("F")]
        public decimal? F { get; set; }
    }

    // -------------------- WoDetail --------------------
    public class WoDetail
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }
        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } // may be empty string

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; }

        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("qty")]
        public decimal Qty { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("bqty")]
        public decimal Bqty { get; set; }

        [JsonPropertyName("bamt")]
        public decimal Bamt { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }
    }

    // -------------------- BilledDetail --------------------
    public class BilledDetail
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string project_id { get; set; }
        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("acct")]
        public string Acct { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }


        [JsonPropertyName("code")]
        [JsonIgnore]
        public string? expense_account_code { get; set; }

    }
    public class VendorSummary
    {

        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }
        [JsonPropertyName("name")]
        public string vendor_name { get; set; }

        [JsonPropertyName("cat")]
        public string category { get; set; }

        [JsonPropertyName("orderVal")]
        public decimal order_value { get; set; }

        [JsonPropertyName("woVal")]
        public decimal WoVal { get; set; }

        [JsonPropertyName("billed")]
        public decimal billed { get; set; }

        [JsonPropertyName("outstanding")]
        public decimal balance { get; set; }

        [JsonPropertyName("retention")]
        public decimal retention { get; set; }

        [JsonPropertyName("wo")]
        public int Wo { get; set; }

        [JsonPropertyName("po")]
        public int Po { get; set; }

        [JsonPropertyName("asn")]
        public int Asn { get; set; }

        [JsonPropertyName("inv")]
        public int invoices { get; set; }

        [JsonPropertyName("billPct")]
        public decimal? bill_progress { get; set; }
    }

    // -------------------- WoSummary --------------------
    public class WoSummary
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("glDate")]
        public string GlDate { get; set; }

        [JsonPropertyName("dept")]
        public string Dept { get; set; }

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("ret")]
        public decimal? Ret { get; set; }

        [JsonPropertyName("wctRet")]
        public object WctRet { get; set; } // null or decimal?

        [JsonPropertyName("retDate")]
        public string RetDate { get; set; }

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; }

        [JsonPropertyName("appr")]
        public string Appr { get; set; }

        [JsonPropertyName("billStatus")]
        public string BillStatus { get; set; }

        [JsonPropertyName("asnNo")]
        public string AsnNo { get; set; }

        [JsonPropertyName("asnDate")]
        public string AsnDate { get; set; }

        [JsonPropertyName("asnStatus")]
        public string AsnStatus { get; set; }

        [JsonPropertyName("memo")]
        public string Memo { get; set; }
    }

    // -------------------- PoSummary --------------------
    public class PoSummary
    {

        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("cat")]
        public string Cat { get; set; }

        [JsonPropertyName("glDate")]
        public string GlDate { get; set; }

        [JsonPropertyName("dept")]
        public string Dept { get; set; }

        [JsonPropertyName("supplier")]
        public string Supplier { get; set; }

        [JsonPropertyName("item")]
        public string Item { get; set; } = "_"; // default value if missing

        [JsonPropertyName("val")]
        public decimal Val { get; set; }

        [JsonPropertyName("ret")]
        public decimal? Ret { get; set; }

        [JsonPropertyName("wctRet")]
        public object WctRet { get; set; }

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("appr")]
        public string Appr { get; set; }

        [JsonPropertyName("memo")]
        public string Memo { get; set; }

        [JsonPropertyName("grnNo")]
        public string GrnNo { get; set; }

        [JsonPropertyName("grnDate")]
        public string GrnDate { get; set; }

        [JsonPropertyName("grnStatus")]
        public string GrnStatus { get; set; }
    }

    // -------------------- GrnItem --------------------
    public class GrnItem
    {
        [JsonPropertyName("grn")]
        public string Grn { get; set; }

        [JsonPropertyName("po")]
        public string Po { get; set; }

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; }

        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("qty")]
        public string Qty { get; set; } // might be dash or number

        [JsonPropertyName("val")]
        public decimal Val { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("by")]
        public string By { get; set; }
    }

    // -------------------- InvoiceItem --------------------
    public class InvoiceItem
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; }

        [JsonPropertyName("glDate")]
        public string GlDate { get; set; }

        [JsonPropertyName("dept")]
        public string Dept { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("appr")]
        public string Appr { get; set; }

        [JsonPropertyName("payStatus")]
        public string PayStatus { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("paid")]
        public decimal Paid { get; set; }

        [JsonPropertyName("balance")]
        public decimal Balance { get; set; }

        [JsonPropertyName("ret")]
        public decimal? Ret { get; set; }

        [JsonPropertyName("wopo")]
        public string Wopo { get; set; }

        [JsonPropertyName("memo")]
        public string Memo { get; set; }
    }

    // -------------------- RetentionItem --------------------
    public class RetentionItem
    {
        [JsonPropertyName("projectId")]
        [JsonIgnore]
        public string? project_id { get; set; }
        [JsonPropertyName("vendor")]
        public string vendor_name { get; set; }

        [JsonPropertyName("bills")]
        public int bills_count { get; set; }

        [JsonPropertyName("held")]
        public decimal held_amt { get; set; }

        //[JsonPropertyName("released")]
        //public decimal Released { get; set; }

        [JsonPropertyName("due")]
        public string release_due { get; set; }

        [JsonPropertyName("hold")]
        public string payment_hold { get; set; }
    }

    // -------------------- TdsItem --------------------
    public class TdsItem
    {
        [JsonPropertyName("section")]
        public string Section { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("base")]
        public decimal Base { get; set; }

        [JsonPropertyName("rate")]
        public string Rate { get; set; }

        [JsonPropertyName("tds")]
        public decimal Tds { get; set; }

        [JsonPropertyName("deductees")]
        public int Deductees { get; set; }
    }

    // -------------------- BoqItem --------------------
    public class BoqItem
    {

        [JsonPropertyName("Project_Id")]
        [JsonIgnore]
        public string? Project_Id { get; set; }
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("desc")]
        public string Description { get; set; }

        [JsonPropertyName("u")]
        public string U { get; set; }

        [JsonPropertyName("dq")]
        public decimal Dq { get; set; }

        [JsonPropertyName("oq")]
        public decimal Oq { get; set; }

        [JsonPropertyName("dr")]
        public decimal Dr { get; set; }

        [JsonPropertyName("or_")]
        public decimal Or_ { get; set; }

        [JsonPropertyName("bv")]
        public decimal Bv { get; set; }

        [JsonPropertyName("wv")]
        public decimal Wv { get; set; }
    }

    // -------------------- BoqTot --------------------
    public class BoqTot
    {
        [JsonPropertyName("design")]
        public decimal Design { get; set; }

        [JsonPropertyName("order")]
        public decimal Order_Val { get; set; }
        [JsonPropertyName("projectid")]
        [JsonIgnore]
        public string? Project_Id { get; set; }
    }

    // -------------------- WoLineItem --------------------
    public class WoLineItem
    {
        [JsonPropertyName("project_Id")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("refno")]
        [JsonIgnore]
        public string? ref_no { get; set; }

        [JsonPropertyName("line_no")]
        [JsonIgnore]
        public string? line_no { get; set; }


        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("qty")]
        public decimal Qty { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("tax")]
        public decimal Tax { get; set; }

        [JsonPropertyName("bqty")]
        public decimal Bqty { get; set; }

        [JsonPropertyName("wpRet")]
        public object WpRet { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("cdesc")]
        public string Cdesc { get; set; }
    }

    // -------------------- PoLineItem --------------------
    public class PoLineItem
    {

        [JsonPropertyName("project_Id")]
        [JsonIgnore]
        public string? project_id { get; set; }

        [JsonPropertyName("refno")]
        [JsonIgnore]
        public string? ref_no { get; set; }

        [JsonPropertyName("line_no")]
        [JsonIgnore]
        public string? line_no { get; set; }


        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("qty")]
        public decimal Qty { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("tax")]
        public decimal Tax { get; set; }

        [JsonPropertyName("bqty")]
        public decimal Bqty { get; set; }

        [JsonPropertyName("wpRet")]
        public object WpRet { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("cdesc")]
        public string Cdesc { get; set; }
    }

    // -------------------- AsnHeader --------------------
    public class AsnHeader
    {
        [JsonPropertyName("asnNo")]
        public string AsnNo { get; set; }

        [JsonPropertyName("challan")]
        public string Challan { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("appDate")]
        public string AppDate { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("asnQty")]
        public decimal AsnQty { get; set; }

        [JsonPropertyName("apprQty")]
        public decimal ApprQty { get; set; }

        [JsonPropertyName("billQty")]
        public decimal BillQty { get; set; }

        [JsonPropertyName("draftInv")]
        public string DraftInv { get; set; }

        [JsonPropertyName("responsible")]
        public string Responsible { get; set; }

        [JsonPropertyName("lineCount")]
        public int LineCount { get; set; }
    }

    // -------------------- AsnLine --------------------
    public class AsnLine
    {
        [JsonPropertyName("docNo")]
        public string DocNo { get; set; }

        [JsonPropertyName("draftInv")]
        public string DraftInv { get; set; }

        [JsonPropertyName("vendBill")]
        public string VendBill { get; set; }

        [JsonPropertyName("asnQty")]
        public decimal AsnQty { get; set; }

        [JsonPropertyName("apprQty")]
        public decimal ApprQty { get; set; }

        [JsonPropertyName("billQty")]
        public decimal BillQty { get; set; }

        [JsonPropertyName("responsible")]
        public string Responsible { get; set; }
        [JsonPropertyName("status")]
        public string? status { get; set; }

        //[JsonPropertyName("lineCount")]
        //public int? lineCount { get; set; }
    }

    // -------------------- InvLineItem --------------------
    public class InvLineItem
    {
        [JsonPropertyName("item")]
        public string? Item { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("qty")]
        public decimal? Qty { get; set; }

        [JsonPropertyName("rate")]
        public decimal? Rate { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("tax")]
        public decimal? Tax { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("cdesc")]
        public string? Cdesc { get; set; }
    }

    // -------------------- VendorPerformance --------------------
    public class VendorPerformance
    {

        [JsonPropertyName("projectid")]
        [JsonIgnore]
        public string? project_id { get; set; }


        [JsonPropertyName("name")]
        public string vendor_name { get; set; }

        [JsonPropertyName("cat")]
        public string category { get; set; }

        [JsonPropertyName("orders")]
        public int orders { get; set; }

        [JsonPropertyName("orderValue")]
        public decimal order_value { get; set; }

        [JsonPropertyName("billed")]
        public decimal billed { get; set; }

        [JsonPropertyName("paid")]
        public decimal paid { get; set; }

        [JsonPropertyName("balance")]
        public decimal balance { get; set; }

        [JsonPropertyName("invoices")]
        public int invoices { get; set; }

        [JsonPropertyName("retention")]
        public decimal retention { get; set; }

        [JsonPropertyName("grn")]
        public int grn { get; set; }

        [JsonPropertyName("billProgress")]
        public decimal? bill_progress { get; set; }

        [JsonPropertyName("apprPct")]
        public decimal? appr_pct { get; set; }

        [JsonPropertyName("rejBills")]
        public int rej_bills { get; set; }
    }

    // -------------------- VendorInvoiceSummary --------------------
    public class VendorInvoiceSummary
    {
        [JsonPropertyName("no")]
        public string No { get; set; }

        [JsonPropertyName("glDate")]
        public string GlDate { get; set; }

        [JsonPropertyName("dept")]
        public string Dept { get; set; }

        [JsonPropertyName("appr")]
        public string Appr { get; set; }

        [JsonPropertyName("payStatus")]
        public string PayStatus { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("amt")]
        public decimal Amt { get; set; }

        [JsonPropertyName("paid")]
        public decimal Paid { get; set; }

        [JsonPropertyName("balance")]
        public decimal Balance { get; set; }

        [JsonPropertyName("wopo")]
        public string Wopo { get; set; }

        // Convert Model to JSON string
        //var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        //Root root = JsonSerializer.Deserialize<Root>(jsonString, options);
    }

    public class ProjectTotalCount
    {
        [JsonPropertyName("totalbudget")]
        public decimal? TotalBudget { get; set; }

        [JsonPropertyName("totalcommitted")]
        public decimal? TotalCommited { get; set; }
        [JsonPropertyName("totalAvailable")]
        public decimal? TotalAvailable { get; set; }
        [JsonPropertyName("totalvendor")]
        public decimal? TotalVendor { get; set; }

        [JsonPropertyName("totalalert")]
        public decimal? totalAlert { get; set; }

        [JsonPropertyName("committedPer")]
        public decimal? CommittedPer { get; set;}

        [JsonPropertyName("UncommittedPer")]
        public decimal? UnCommitedPer { get; set; }


        [JsonPropertyName("totalbilled")]
        public decimal? TotalBilled { get; set; }


        [JsonPropertyName("billedPer")]
        public decimal? BilledPer { get; set; }
    }


}
