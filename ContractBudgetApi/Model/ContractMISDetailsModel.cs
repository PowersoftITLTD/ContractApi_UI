namespace ContractBudgetApi.Model
{
    public class ContractMISDetailsModel
    {
        // Entity
        public string? Entity_Id { get; set; }
        public string? EntityName { get; set; }
        public string? Grp { get; set; }
        public string? Gst { get; set; }

        // Project
        public string? Project_Id { get; set; }
        public string? ProjectName { get; set; }
        public string? Location { get; set; }
        public string? Stage { get; set; }
        public bool? Is_Real { get; set; }

        // ASN Header
        public string? Asn_No { get; set; }
        public string? Po_Ref_No { get; set; }
        public string? Challan_No { get; set; }
        public DateTime? Asn_Challan_Date { get; set; }
        public DateTime? Application_Date { get; set; }
        public string? ASN_approval_status { get; set; }
        public string? ASN_department { get; set; }
        public string? ASN_responsible { get; set; }
        public decimal? ASN_amount { get; set; }
        public string? ASN_draft_inv_id { get; set; }

        // ASN Line
        public string? FAL_Asn_no { get; set; }
        public string? FAL_po_ref_no { get; set; }
        public string? Doc_Number { get; set; }
        public string? FAL_draft_Inv_id { get; set; }
        public string? Vendor_Bill_Link { get; set; }
        public decimal? Asn_Qty { get; set; }
        public decimal? Approved_Qty { get; set; }
        public decimal? Billed_Qty { get; set; }
        public string? FAL_responsible { get; set; }

        // Bill Header
        public string? Transaction_No { get; set; }
        public string? Ref_No { get; set; }
        public string? Bill_Type { get; set; }
        public DateTime? Gl_Date { get; set; }
        public DateTime? Invoice_Date { get; set; }
        public string? Bill_department { get; set; }
        public string? Vendor_Name { get; set; }
        public string? Memo { get; set; }
        public decimal? Bill_amount { get; set; }
        public string? Bill_approval_status { get; set; }
        public string? Bill_status { get; set; }
        public decimal? Retention_pct { get; set; }
        public string? Created_From_Ref { get; set; }
        public decimal? Amount_Paid { get; set; }
        public decimal? Amount_Balance { get; set; }

        // Bill Line
        public string? FBL_transaction_no { get; set; }
        public int? Line_No { get; set; }
        public string? Item_Code { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Item_Rate { get; set; }
        public decimal? FBL_amount { get; set; }
        public decimal? Tax_Amount { get; set; }
        public string? Expense_Account_Code { get; set; }
        public string? Expense_Account_Desc { get; set; }
        public string? FBL_created_from_ref { get; set; }
    }

    //public class BoqTotalsModel
    //{
    //    public int Project_Id { get; set; }
    //    public string Design { get; set; }
    //    public decimal? Order_Val { get; set; }
    //}

    public class BudgetTree_ModelBinding
    {

        public string? Project_Id { get; set; }
        public string? parent_code { get; set; }
        public string? parent_desc { get; set; }
        public string? code { get; set; }
        public string? child_desc { get; set; }
        public decimal? a { get; set; }
        public decimal? b { get; set; }
        public decimal? c { get; set; }
        public decimal? d { get; set; }
        public decimal? e { get; set; }
        public decimal? f { get; set; }
    }


    public class V_project_card
    {
        public string? Project_Id { get; set; }
        public string? name { get; set; }
        public string? location { get; set; }
        public string? stage { get; set; }
        public bool? is_real { get; set; }
        public string? budget { get; set; }
        public string? committed { get; set; }
        public string? billed { get; set; }
        public string? available { get; set; }
        public string? retention { get; set; }
        public string? vendors { get; set; }
        public string? wos { get; set; }
        public string? boq_design { get; set; }
        public string? boq_order { get; set; }

    }

}
