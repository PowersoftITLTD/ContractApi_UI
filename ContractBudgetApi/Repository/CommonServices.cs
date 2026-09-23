using ContractBudgetApi.Interface;
using ContractBudgetApi.Model;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ContractBudgetApi.Repository
{
    public class CommonServices : ICommonServices
    {
        private readonly IConfiguration _configuration;
        private readonly SqlConnection _connection;
        private readonly IDapperDbConnection _dbConnection;
        private readonly FileSettings _fileSettings;
        private readonly HostEnvironment _env;
        public CommonServices(IConfiguration configuration, SqlConnection sqlConnection, IDapperDbConnection dbConnection, IOptions<FileSettings> fileSettings, IOptions<HostEnvironment> env)
        {
            _configuration = configuration;
            _connection = sqlConnection;
            _dbConnection = dbConnection;
            _fileSettings = fileSettings.Value;
            //_env = (HostEnvironment)env.Value;
            _env = env.Value;
        }

        private byte[] GetKey(string keyString, int requiredLength)     // Static
        {
            if (keyString == null) keyString = string.Empty;
            byte[] key = Encoding.UTF8.GetBytes(keyString);

            if (key.Length == requiredLength)
                return key;

            var resized = new byte[requiredLength];
            Array.Copy(key, resized, Math.Min(key.Length, requiredLength));
            // If key is shorter: remaining bytes are zero (default)
            return resized;
        }

        public string EncryptionObje<T>(T obj, string keyString)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(obj);
            byte[] key = GetKey(keyString, 32);
            byte[] iv = new byte[16];

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return Convert.ToBase64String(cipherBytes);
            }
        }
        public T DecryptObject<T>(string encryptedBase64, string keyString)
        {
            byte[] key = GetKey(keyString, 32);
            byte[] iv = new byte[16];

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] cipherBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                string json = Encoding.UTF8.GetString(plainBytes);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
        }
        // Files Decryption Method To Decrypt Files 
        public byte[] DecryptFileBytes(string encryptedFileBase64, string keyString)   // Static
        {
            if (string.IsNullOrEmpty(encryptedFileBase64))
                throw new ArgumentException("encryptedFileBase64 is null or empty", nameof(encryptedFileBase64));

            // Same key logic as your DecryptPassword
            byte[] key = GetKey(keyString, 32); // Ensure 32 bytes for AES-256

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;
                aesAlg.IV = new byte[16]; // Zero IV (must match encryption used in Angular)

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                byte[] cipherBytes = Convert.FromBase64String(encryptedFileBase64);

                using (MemoryStream msDecrypt = new MemoryStream())
                {
                    using (CryptoStream csDecrypt = new CryptoStream(new MemoryStream(cipherBytes), decryptor, CryptoStreamMode.Read))
                    {
                        csDecrypt.CopyTo(msDecrypt); // Copy decrypted bytes
                    }
                    return msDecrypt.ToArray(); // return file bytes
                }
            }
        }
        byte[] ICommonServices.GetKey(string keyString, int requiredLength)
        {
            return GetKey(keyString, requiredLength);
        }
        public string SendEmail(string sp_to, string sp_cc, string sp_bcc, string sp_subject, string sp_body, string sp_mailtype, string sp_display_name, List<string> lp_attachment, MailDetailsNT mailDetailsNT)
        {
            string strerror = string.Empty;
            try
            {
                if (_env.env == "Production")
                {
                    using (MailMessage mail1 = new MailMessage())
                    {
                        mail1.From = new System.Net.Mail.MailAddress(mailDetailsNT.MAIL_FROM, sp_display_name.ToUpper());//, sp_display_name == "" ? dt.Rows[0]["MAIL_DISPLAY_NAME"].ToString() : sp_display_name
                                                                                                                         //mail1.To.Add("narendrakumar.soni@powersoft.in");
                        foreach (var to_address in sp_to.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            mail1.To.Add(new MailAddress(to_address));
                            //mail1.To.Add(new MailAddress("narendrakumar.soni@powersoft.in"));
                            //mail.To.Add("ashish.tripathi@powersoft.in");
                            //mail.CC.Add("brijesh.tiwari@powersoft.in");
                        }
                        if (sp_cc != null)
                            foreach (var cc_address in sp_cc.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                mail1.CC.Add(new MailAddress(cc_address));
                                // mail.CC.Add("brijesh.tiwari@powersoft.in");
                            }
                        if (sp_bcc != null)
                            foreach (var bcc_address in sp_bcc.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                mail1.Bcc.Add(new MailAddress(bcc_address));
                            }

                        mail1.Subject = sp_subject;
                        mail1.Body = sp_body;
                        mail1.IsBodyHtml = true;
                        //mail1.Attachments.Add(new Attachment("C:\\file.zip"));

                        using (SmtpClient smtp1 = new SmtpClient(mailDetailsNT.SMTP_HOST.ToString(), Convert.ToInt32(mailDetailsNT.SMTP_PORT)))
                        {
                            smtp1.Credentials = new NetworkCredential(mailDetailsNT.MAIL_FROM, mailDetailsNT.SMTP_PASS.ToString());
                            //new NetworkCredential("autosupport@powersoft.in", "yivz qklg jsbv ttso");
                            smtp1.EnableSsl = mailDetailsNT.SMTP_ESSL.ToString() == "true" ? true : false;

                            if (lp_attachment != null)
                                foreach (var attach in lp_attachment)
                                {
                                    mail1.Attachments.Add(new Attachment(attach));
                                }

                            smtp1.Send(mail1);
                        }
                        foreach (Attachment attachment in mail1.Attachments)
                        {
                            attachment.Dispose();
                        }

                    }
                }
                strerror = "Sent Email";
                return strerror;


                /*MailMessage mail = new MailMessage();


                foreach (var to_address in sp_to.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    // mail.To.Add(new MailAddress(to_address));
                    mail.To.Add(new MailAddress("narendrakumar.soni@powersoft.in"));
                    //mail.To.Add("ashish.tripathi@powersoft.in");
                    //mail.CC.Add("brijesh.tiwari@powersoft.in");
                }
                if (sp_cc != null)
                    foreach (var cc_address in sp_cc.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mail.CC.Add(new MailAddress(cc_address));
                        // mail.CC.Add("brijesh.tiwari@powersoft.in");
                    }
                if (sp_bcc != null)
                    foreach (var bcc_address in sp_bcc.Replace(",", ";").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mail.Bcc.Add(new MailAddress(bcc_address));
                    }

                mail.Subject = sp_subject;
                //mail.From = new System.Net.Mail.MailAddress(mailDetailsNT.MAIL_FROM, sp_display_name);//, sp_display_name == "" ? dt.Rows[0]["MAIL_DISPLAY_NAME"].ToString() : sp_display_name
                mail.From = new System.Net.Mail.MailAddress("autosupport@powersoft.in");//, sp_display_name == "" ? dt.Rows[0]["MAIL_DISPLAY_NAME"].ToString() : sp_display_name
                SmtpClient smtp = new SmtpClient();
                smtp.Timeout = Convert.ToInt32(mailDetailsNT.SMTP_TIMEOUT);
                smtp.Port = Convert.ToInt32(mailDetailsNT.SMTP_PORT);
                smtp.UseDefaultCredentials = true;
                smtp.Host = mailDetailsNT.SMTP_HOST.ToString();
//                sc.Credentials = basicAuthenticationInfo;
                smtp.Credentials = new NetworkCredential("autosupport@powersoft.in", "yivz qklg jsbv ttso");
                smtp.EnableSsl = mailDetailsNT.SMTP_ESSL.ToString() == "true" ? true : false;
                mail.IsBodyHtml = true;
                mail.Body = sp_body;
                if (lp_attachment != null)
                    foreach (var attach in lp_attachment)
                    {
                        mail.Attachments.Add(new Attachment(attach));
                    }
                smtp.Send(mail);*/


            }
            catch (Exception ex)
            {
                string FileName = string.Empty;
                string strFolder = string.Empty;

                strFolder = _fileSettings.FilePath; // "D:\\Application\\TaskDeployment" + "\\ErrorFolder";
                if (!Directory.Exists(strFolder))
                {
                    Directory.CreateDirectory(strFolder);
                }

                if (File.Exists(strFolder + "\\ErrorLog.txt") == false)
                {
                    using (System.IO.StreamWriter sw = File.CreateText(strFolder + "\\ErrorLog.txt"))
                    {
                        sw.Write("\n");
                        sw.WriteLine("--------------------------------------------------------------" + "\n");
                        sw.WriteLine(System.DateTime.Now);
                        sw.WriteLine(FileName + "--> " + ex.Message.ToString() + "\n");
                        sw.WriteLine("--------------------------------------------------------------" + "\n");
                    }
                }
                else
                {
                    using (System.IO.StreamWriter sw = File.AppendText(strFolder + "\\ErrorLog.txt"))
                    {
                        sw.Write("\n");
                        sw.WriteLine("--------------------------------------------------------------" + "\n");
                        sw.WriteLine(System.DateTime.Now);
                        sw.WriteLine(FileName + "--> " + ex.Message.ToString() + "\n");
                        sw.WriteLine("--------------------------------------------------------------" + "\n");
                    }
                }

                strerror = "Error Sending Email : " + ex.Message;
                return strerror;
            }
        }

        //public async Task<string> InsertInvoice_DOC_TRl(DocumentUploadModel documentUpload)
        //{
        //    try
        //    {
        //        using (IDbConnection db = _dbConnection.CreateConnection())
        //        {
        //            db.Open(); // Ensure connection is open
        //            using (var transaction = db.BeginTransaction())
        //            {
        //                try
        //                {
        //                    //byte[] fileBytes = Base64ToVarbinarySafe(documentUpload.FILECONTENTS); 
        //                    byte[] fileBytes = Base64ToVarbinarySafe(documentUpload.FILECONTENTS);
        //                    var parameters = new DynamicParameters();
        //                    parameters.Add("@MKEY", documentUpload.MKEY);
        //                    parameters.Add("@DOC_NAME", string.IsNullOrEmpty(documentUpload.DOC_NAME) ? null : documentUpload.DOC_NAME);
        //                    parameters.Add("@DOC_TYPE", string.IsNullOrEmpty(documentUpload.DOC_TYPE) ? null : documentUpload.DOC_TYPE);
        //                    parameters.Add("@FILE_NAME", string.IsNullOrEmpty(documentUpload.FILE_NAME) ? null : documentUpload.FILE_NAME);
        //                    parameters.Add("@FILECONTENTS", (fileBytes == null || fileBytes.Length == 0) ? (object)DBNull.Value : fileBytes, DbType.Binary);
        //                    //parameters.Add("@FILECONTENTS", fileBytes,DbType.Binary); // or DBNull.Value
        //                    parameters.Add("@FILECONTENTVAR", string.IsNullOrEmpty(documentUpload.FILECONTENTVAR) ? null : documentUpload.FILECONTENTVAR, DbType.String, size: -1);
        //                    parameters.Add("@UPLOADED_BY", documentUpload.UPLOADED_BY > 0 ? documentUpload.UPLOADED_BY : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@IS_MANDATORY", string.IsNullOrEmpty(documentUpload.IS_MANDATORY) ? "Y" : documentUpload.IS_MANDATORY);
        //                    parameters.Add("@STATUS_FLAG", string.IsNullOrEmpty(documentUpload.STATUS_FLAG) ? "P" : documentUpload.STATUS_FLAG);
        //                    parameters.Add("@APPROVER_ID", documentUpload.APPROVER_ID > 0 ? documentUpload.APPROVER_ID : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@ATTRIBUTE1", string.IsNullOrEmpty(documentUpload.ATTRIBUTE1) ? null : documentUpload.ATTRIBUTE1);
        //                    parameters.Add("@ATTRIBUTE2", string.IsNullOrEmpty(documentUpload.ATTRIBUTE2) ? null : documentUpload.ATTRIBUTE2);
        //                    parameters.Add("@ATTRIBUTE3", string.IsNullOrEmpty(documentUpload.ATTRIBUTE3) ? null : documentUpload.ATTRIBUTE3);
        //                    parameters.Add("@ATTRIBUTE4", string.IsNullOrEmpty(documentUpload.ATTRIBUTE4) ? null : documentUpload.ATTRIBUTE4);
        //                    parameters.Add("@ATTRIBUTE5", string.IsNullOrEmpty(documentUpload.ATTRIBUTE5) ? null : documentUpload.ATTRIBUTE5);
        //                    parameters.Add("@CREATED_BY", documentUpload.CREATED_BY);
        //                    parameters.Add("@LAST_UPDATED_BY", documentUpload.LAST_UPDATED_BY > 0 ? documentUpload.LAST_UPDATED_BY : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@LAST_UPDATE_DATE", documentUpload.LAST_UPDATE_DATE.HasValue ? documentUpload.LAST_UPDATE_DATE.Value : (object)DBNull.Value, DbType.DateTime); // Output parameters
        //                    parameters.Add("@NewSrNo", dbType: DbType.Int64, direction: ParameterDirection.Output);
        //                    parameters.Add("@ResponseMessage", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
        //                    // Execute stored procedure within transaction
        //                    await db.ExecuteAsync("InsertDocumentWithSrNo", parameters, commandType: CommandType.StoredProcedure, transaction: transaction);
        //                    // Get output values
        //                    var srNo = parameters.Get<long?>("@NewSrNo");
        //                    var message = parameters.Get<string>("@ResponseMessage");
        //                    var logMessage = $"MKEY: {documentUpload.MKEY}, SR_NO: {srNo}, Message: {message}";
        //                    if (!srNo.HasValue)
        //                    {
        //                        // Rollback if insert failed
        //                        transaction.Rollback();
        //                        return logMessage;
        //                        // Return logMessage even on failure
        //                    }
        //                    // Commit transaction if everything is fine
        //                    transaction.Commit();
        //                    return logMessage; // Return logMessage on success
        //                }
        //                catch (Exception exTrans)
        //                {
        //                    // Rollback on any exception
        //                    transaction.Rollback();
        //                    return $"Transaction failed and rolled back. Exception: {exTrans.Message}";
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"Connection/Execution failed: {ex.Message}";
        //    }
        //}

        //public async Task<InvoiceDocDto> GetInvoiceDocAsync(decimal mkey, decimal srNo)
        //{
        //    const string query = @"SELECT MKEY,SR_NO,DOC_NAME,DOC_TYPE,FILE_NAME,FILECONTENTS,FILECONTENTVAR, ATTRIBUTE5
        //                         FROM INVOICE_DOC_TRL 
        //                         WHERE MKEY = @MKEY
        //                         AND SR_NO = @SR_NO";

        //    using (IDbConnection db = _dbConnection.CreateConnection())
        //    {
        //        if (db.State != ConnectionState.Open)
        //            db.Open();

        //        var result = await db.QueryFirstOrDefaultAsync<InvoiceDocDto>(
        //            query,
        //            new
        //            {
        //                MKEY = mkey,
        //                SR_NO = srNo
        //            });

        //        return result;
        //    }
        //}

        //public async Task<string> UpdateInvoice_DOC_TRl(DocumentUploadModel documentUpload)
        //{
        //    try
        //    {
        //        using (IDbConnection db = _dbConnection.CreateConnection())
        //        {
        //            db.Open(); // Ensure connection is open
        //            using (var transaction = db.BeginTransaction())
        //            {
        //                try
        //                {
        //                    //byte[] fileBytes = Base64ToVarbinarySafe(documentUpload.FILECONTENTS); 
        //                    byte[] fileBytes = Base64ToVarbinarySafe(documentUpload.FILECONTENTS);
        //                    var parameters = new DynamicParameters();
        //                    parameters.Add("@MKEY", documentUpload.MKEY);
        //                    parameters.Add("@SrNo", documentUpload.SR_NO);
        //                    parameters.Add("@DOC_NAME", string.IsNullOrEmpty(documentUpload.DOC_NAME) ? null : documentUpload.DOC_NAME);
        //                    parameters.Add("@DOC_TYPE", string.IsNullOrEmpty(documentUpload.DOC_TYPE) ? null : documentUpload.DOC_TYPE);
        //                    parameters.Add("@FILE_NAME", string.IsNullOrEmpty(documentUpload.FILE_NAME) ? null : documentUpload.FILE_NAME);
        //                    parameters.Add("@FILECONTENTS", (fileBytes == null || fileBytes.Length == 0) ? (object)DBNull.Value : fileBytes, DbType.Binary);
        //                    //parameters.Add("@FILECONTENTS", fileBytes,DbType.Binary); // or DBNull.Value
        //                    parameters.Add("@FILECONTENTVAR", string.IsNullOrEmpty(documentUpload.FILECONTENTVAR) ? null : documentUpload.FILECONTENTVAR, DbType.String, size: -1);
        //                    parameters.Add("@UPLOADED_BY", documentUpload.UPLOADED_BY > 0 ? documentUpload.UPLOADED_BY : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@IS_MANDATORY", string.IsNullOrEmpty(documentUpload.IS_MANDATORY) ? "Y" : documentUpload.IS_MANDATORY);
        //                    parameters.Add("@STATUS_FLAG", string.IsNullOrEmpty(documentUpload.STATUS_FLAG) ? "P" : documentUpload.STATUS_FLAG);
        //                    parameters.Add("@APPROVER_ID", documentUpload.APPROVER_ID > 0 ? documentUpload.APPROVER_ID : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@ATTRIBUTE1", string.IsNullOrEmpty(documentUpload.ATTRIBUTE1) ? null : documentUpload.ATTRIBUTE1);
        //                    parameters.Add("@ATTRIBUTE2", string.IsNullOrEmpty(documentUpload.ATTRIBUTE2) ? null : documentUpload.ATTRIBUTE2);
        //                    parameters.Add("@ATTRIBUTE3", string.IsNullOrEmpty(documentUpload.ATTRIBUTE3) ? null : documentUpload.ATTRIBUTE3);
        //                    parameters.Add("@ATTRIBUTE4", string.IsNullOrEmpty(documentUpload.ATTRIBUTE4) ? null : documentUpload.ATTRIBUTE4);
        //                    parameters.Add("@ATTRIBUTE5", string.IsNullOrEmpty(documentUpload.ATTRIBUTE5) ? null : documentUpload.ATTRIBUTE5);
        //                    parameters.Add("@CREATED_BY", documentUpload.CREATED_BY);
        //                    parameters.Add("@LAST_UPDATED_BY", documentUpload.LAST_UPDATED_BY > 0 ? documentUpload.LAST_UPDATED_BY : (object)DBNull.Value, DbType.Int64);
        //                    parameters.Add("@LAST_UPDATE_DATE", documentUpload.LAST_UPDATE_DATE.HasValue ? documentUpload.LAST_UPDATE_DATE.Value : (object)DBNull.Value, DbType.DateTime); // Output parameters
        //                    parameters.Add("@NewSrNo", dbType: DbType.Int64, direction: ParameterDirection.Output);
        //                    parameters.Add("@ResponseMessage", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
        //                    // Execute stored procedure within transaction
        //                    await db.ExecuteAsync("InsertDocumentWithSrNo", parameters, commandType: CommandType.StoredProcedure, transaction: transaction);
        //                    // Get output values
        //                    var srNo = parameters.Get<long?>("@NewSrNo");
        //                    var message = parameters.Get<string>("@ResponseMessage");
        //                    var logMessage = $"MKEY: {documentUpload.MKEY}, SR_NO: {srNo}, Message: {message}";
        //                    if (!srNo.HasValue)
        //                    {
        //                        // Rollback if insert failed
        //                        transaction.Rollback();
        //                        return logMessage;
        //                        // Return logMessage even on failure
        //                    }
        //                    // Commit transaction if everything is fine
        //                    transaction.Commit();
        //                    return logMessage; // Return logMessage on success
        //                }
        //                catch (Exception exTrans)
        //                {
        //                    // Rollback on any exception
        //                    transaction.Rollback();
        //                    return $"Transaction failed and rolled back. Exception: {exTrans.Message}";
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"Connection/Execution failed: {ex.Message}";
        //    }
        //}

        //public static byte[] Base64ToVarbinarySafe(string base64)
        //{
        //    if (string.IsNullOrWhiteSpace(base64))
        //        throw new ArgumentException("FILECONTENTVAR is empty");

        //    // Remove data URI prefix if present
        //    if (base64.Contains(","))
        //        base64 = base64.Substring(base64.IndexOf(",") + 1);

        //    // Remove whitespace
        //    base64 = base64
        //        .Replace("\r", "")
        //        .Replace("\n", "")
        //        .Replace(" ", "");

        //    // Fix padding
        //    base64 = base64.PadRight(
        //        base64.Length + (4 - base64.Length % 4) % 4, '=');

        //    // Validate Base64
        //    if (!Convert.TryFromBase64String(base64, new Span<byte>(new byte[base64.Length]), out _))
        //        throw new FormatException("FILECONTENTVAR is not valid Base64");

        //    return Convert.FromBase64String(base64);
        //}

        //public static byte[] Base64ToVarbinarySafe(string base64)
        //{
        //    if (string.IsNullOrWhiteSpace(base64))
        //        return Array.Empty<byte>();

        //    // Remove data URI prefix
        //    int commaIndex = base64.IndexOf(',');
        //    if (commaIndex >= 0)
        //        base64 = base64.Substring(commaIndex + 1);

        //    base64 = base64.Trim();

        //    try
        //    {
        //        return Convert.FromBase64String(base64);
        //    }
        //    catch (FormatException ex)
        //    {
        //        throw new FormatException("Invalid Base64 FILECONTENTVAR", ex);
        //    }
        //}

        public static byte[] Base64ToVarbinarySafe(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return Array.Empty<byte>();

            // Remove data URI prefix (data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,)
            int commaIndex = base64.IndexOf(',');
            if (commaIndex >= 0)
                base64 = base64.Substring(commaIndex + 1);

            // Remove whitespace & line breaks
            base64 = base64
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Replace(" ", "");

            // Fix padding if missing
            int padding = base64.Length % 4;
            if (padding > 0)
                base64 = base64.PadRight(base64.Length + (4 - padding), '=');

            // Handle URL-safe Base64
            base64 = base64
                .Replace('-', '+')
                .Replace('_', '/');

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                throw new FormatException(
                    $"Invalid Base64 content. Length={base64.Length}", ex);
            }
        }

        public async Task<CommonResponse> GetProjectDataAsync(string sessionUserId, string parameter1)
        {
            var commonresponse = new CommonResponse();
            var root = new Root();
            try
            {
                using (var _connection = _dbConnection.CreateConnection())
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("@Session_UserId", sessionUserId, DbType.Int32);
                    parameters.Add("@Parameter1", parameter1, DbType.String);
                    var result = await _connection.QueryAsync<ContractMISDetailsModel>("SP_GET_Contract_MIS_DETAILS", parameters, commandType: CommandType.StoredProcedure, commandTimeout: 180);
                    var boqtotallist = await GetBoqTotalsAsync();
                    var projectcardlist = await GetprojectcardSummaryDataAsync();
                    var factretentionlist = await Getfact_RetentionAsync();
                    var boglist = await Getfact_BudgetItemAsync();
                    var wolineList = await Getfact_WoLineItemAsync();
                    var polineList = await Getfact_POLineItemAsync();
                    var boDetailsList = await Getfact_WoDetailsAsync();
                    var budgetTree = await GetBudgetTreeDataAsync();
                    var budgetlist = await GetBudgetDataAsync();
                    var vendorpre = await GetVendorPerformanceDataAsync();
                    var wosumary = await GetWoSummaryDataAsync();
                    var posummary = await GetPoSummaryDataAsync();
                    var invoicelist = await GetInvoicesItemDataAsync();
                    var billedDetailsList = await GetBilledDetailsDataAsync();
                    var vendorList = await GetVendorsDataAsync();
                    var projectCount = await GetProjectTotalCountDataAsync();
                    //BoqTot? boq = boqResponse.Data as BoqTot;
                    if (result != null && result.Any())
                    {
                        root.Entity = result.GroupBy(x => new{x.Entity_Id,x.EntityName})
                                                     .Select(g => new Entity
                                                     {
                                                         entityId = g.Key.Entity_Id,
                                                         Name = g.Key.EntityName,
                                                         Group = g.First().Grp,
                                                         Gst = g.First().Gst
                                                     })
                                                     .FirstOrDefault() ??  new Entity();

                        root.Projects = projectcardlist.GroupBy(x => x.Project_Id)
                                                     .Select(g => new ProjectSummary
                                                     {
                                                         Id = g.Key,
                                                         Name = g.First().name,
                                                         Loc = g.First().location,
                                                         Stage = g.First().stage,
                                                         IsReal = g.First().is_real ?? false,
                                                         Budget = Convert.ToDecimal(g.First().budget),
                                                         Committed = Convert.ToDecimal(g.First().committed),
                                                         Billed = Convert.ToDecimal(g.First().billed),
                                                         Available = Convert.ToDecimal(g.First().available),
                                                         Retention = Convert.ToDecimal(g.First().retention),
                                                         Vendors = Convert.ToInt32(g.First().vendors),
                                                         Wos = Convert.ToInt32(g.First().wos),
                                                         BoqDesign = Convert.ToDecimal(g.First().boq_design),
                                                         BoqOrder = Convert.ToDecimal(g.First().boq_order),
                                                         DirectExpense = g.First().directExpense ,  //"XX" ,
                                                         ConstructionArea = g.First().constructionArea,  //"XX" ,
                                                         ConstructionRate = g.First().constructionRate,  //"XX" ,
                                                         CarpetArea = g.First().carpetArea,
                                                         CarpetRate = g.First().CarpetRate,
                                                         ProjectType = g.First().projectType ,
                                                         BilledPerc= g.First().billedPerc ,
                                                         CommittedPerc= g.First().committedPerc ,
                                                         ConstructionAreaRate= g.First().constructionAreaRate ,
                                                         CarpetAreaRate = g.First().carpetAreaRate ,
                                                         Utilized= g.First().utilized,
                                                         Balance= g.First().balance,
                                                         OverAllRate= g.First().overAllRate,
                                                         InclMigration= g.First().inclMigration,

                                                         // Alerts= 
                                                     })
                                                     .ToList();

                        root.Data = result.GroupBy(x => x.Project_Id!)
                                                          .ToDictionary(
                                                              g => g.Key,
                                                              g => new DetailedProjectData
                                                              {
                                                                  Project = new ProjectInfo
                                                                  {
                                                                      Id = g.Key,
                                                                      Name = g.First().ProjectName,
                                                                      Loc = g.First().Location,
                                                                      Entity = g.First().EntityName,
                                                                      Group = g.First().Grp
                                                                  },
                                                                  Budget= budgetlist.Where(x=>x.project_id==g.Key).ToList(),
                                                                  BudgetTree = BuildBudgetTreeItem(budgetTree.Where(x => x.Project_Id == g.Key)),
                                                                  Wos= wosumary.Where(x=>x.project_id== g.Key).ToList(),
                                                                  Pos= posummary.Where(x=>x.project_id== g.Key).ToList(),
                                                                  AsnHeaders = BuildAsnHeaders(g),
                                                                  AsnLines = BuildAsnLines(g),
                                                                  InvLines = BuildInvoicesLineItem(g),
                                                                  VendorInv = BuildVendorInvoices(g),
                                                                  BoqTot = boqtotallist.FirstOrDefault(b => b.Project_Id == g.Key)!,
                                                                  Boq = boglist.Where(b => b.Project_Id == g.Key).ToList()!,
                                                                  Retention = factretentionlist.Where(b => b.project_id == g.Key).ToList()!,
                                                                  WoDetails = boDetailsList.Where(b => b.project_id == g.Key).ToList()!,
                                                                  WoLines = BuildWoLineItems(wolineList.Where(x=>x.project_id== g.Key)),
                                                                  PoLines= BuildPoLineItems(polineList.Where(x=>x.project_id== g.Key)),
                                                                  VendorPerf= vendorpre.Where(x=>x.project_id== g.Key).ToList() ,
                                                                  Invoices= invoicelist.Where(x=>x.project_id== g.Key).ToList() ,
                                                                  BilledDetails= BuildBilledDetails(billedDetailsList.Where(x=>x.project_id== g.Key)) ,
                                                                  Vendors= vendorList.Where(x=>x.project_id== g.Key).ToList(),
                                                                  projectTotal= projectCount
                                                              });



                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        string jsonString = System.Text.Json.JsonSerializer.Serialize(root);
                        var first = result.First();
                        root.Entity= new Entity
                        {
                            Name = first.EntityName,
                            Group = first.Grp,
                            Gst = first.Gst ,
                            entityId = first.Entity_Id!,
                        };
                        commonresponse.Status = "Success";
                        commonresponse.Message = "Data retrieved successfully.";
                        commonresponse.Data = root;
                    }
                    else
                    {
                        commonresponse.Status = "NoData";
                        commonresponse.Message = "No data found for the given parameters.";
                        commonresponse.Data = null;
                    }

                    return commonresponse;
                }
            }
            catch (Exception ex)
            {
                commonresponse.Status = "Error";
                commonresponse.Message = $"An error occurred: {ex.Message}";
                commonresponse.Data = null;
                return commonresponse;
            }
        }
        private Dictionary<string, List<AsnHeader>> BuildAsnHeaders(IEnumerable<ContractMISDetailsModel> data)
        {
            return data.GroupBy(x => x.Po_Ref_No!)
                .ToDictionary(
                    g => g.Key,
                    g => new List<AsnHeader>
                    {
                        new AsnHeader
                        {
                            AsnNo = g.First().Asn_No,
                            Challan = g.First().Challan_No,
                            Date = g.First().Asn_Challan_Date?.ToString("yyyy-MM-dd"),
                            AppDate = g.First().Application_Date?.ToString("yyyy-MM-dd"),
                            Status = g.First().ASN_approval_status,
                            AsnQty = g.Sum(x => x.Asn_Qty ?? 0),
                            ApprQty = g.Sum(x => x.Approved_Qty ?? 0),
                            BillQty = g.Sum(x => x.Billed_Qty ?? 0),
                            DraftInv = g.First().ASN_draft_inv_id,
                            Responsible = g.First().ASN_responsible,
                            LineCount = g
                                .Select(x => x.Doc_Number)
                                .Where(x => !string.IsNullOrEmpty(x))
                                .Distinct()
                                .Count()
                        }
                    })!;



        }
        private Dictionary<string, List<AsnLine>> BuildAsnLines(IEnumerable<ContractMISDetailsModel> data)
        {
            return data.GroupBy(x => x.Asn_No!)
                .ToDictionary(
                              g => g.Key,
                              g => g
                                  .Where(x => !string.IsNullOrEmpty(x.Doc_Number))
                                  .GroupBy(x => x.Doc_Number)
                                  .Select(x => new AsnLine
                                  {
                                      DocNo = x.Key,
                                      DraftInv = x.First().FAL_draft_Inv_id,
                                      VendBill = x.First().Vendor_Bill_Link,
                                      AsnQty = x.Sum(a => a.Asn_Qty ?? 0),
                                      ApprQty = x.Sum(a => a.Approved_Qty ?? 0),
                                      BillQty = x.Sum(a => a.Billed_Qty ?? 0),
                                      Responsible = x.First().FAL_responsible,
                                      status = x.First().ASN_approval_status,
                                  }).ToList());
                
        
        }
        private Dictionary<string, List<InvLineItem>> BuildInvoicesLineItem(IEnumerable<ContractMISDetailsModel> data)
        {
            return data.Where(x => !string.IsNullOrEmpty(x.Transaction_No))
                       .GroupBy(x => x.Vendor_Name ?? "Unknown")
                       .ToDictionary(
                           g => g.Key,
                           g => g
                               .GroupBy(x => x.Transaction_No)
                               .Select(x => new InvLineItem
                               {
                      
                                   Item= x.First().Item_Code,
                                   Desc= x.First().Description,
                                   Qty= x.First().Quantity ?? 0,
                                   Rate= x.First().Item_Rate ?? 0,
                                   Tax= x.First().Tax_Amount ?? 0,
                                   Code= x.First().Expense_Account_Code,
                                   Cdesc= x.First().Expense_Account_Desc
                               })
                               .ToList());
        }
        private Dictionary<string, List<VendorInvoiceSummary>> BuildVendorInvoices(IEnumerable<ContractMISDetailsModel> data)
        {
            return data
                .Where(x => !string.IsNullOrEmpty(x.Transaction_No))
                .GroupBy(x => x.Vendor_Name ?? "Unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .GroupBy(x => x.Transaction_No)
                        .Select(x => new VendorInvoiceSummary
                        {
                            No = x.First().Transaction_No,
                            GlDate = x.First().Gl_Date?.ToString("yyyy-MM-dd"),
                            Dept = x.First().Bill_department,
                            Appr = x.First().Bill_approval_status,
                            PayStatus = x.First().Bill_status,
                            Type = x.First().Bill_Type,
                            Amt = x.First().Bill_amount ?? 0,
                            Paid = x.First().Amount_Paid ?? 0,
                            Balance = x.First().Amount_Balance ?? 0,
                            Wopo = x.First().Created_From_Ref,
                            //Memo = x.First().Memo
                        })
                        .ToList()
                );
        }
        private Dictionary<string, BudgetTreeItem> BuildBudgetTreeItem(IEnumerable<BudgetTree_ModelBinding> data)
        {
            return data
                .Where(x => !string.IsNullOrEmpty(x.parent_code))
                .GroupBy(x => x.parent_code!)
                .ToDictionary(
                    g => g.Key,
                    g => new BudgetTreeItem
                    {
                        Code = g.Key,
                        Desc = g.First().parent_desc,
                        Children = g.Select(x => new BudgetTreeChild
                        {
                            Code = x.code,
                            Desc = x.child_desc,
                            A = x.a ?? 0,
                            B = x.b ?? 0,
                            C = x.c ?? 0,
                            D = x.d ?? 0,
                            E = x.e ?? 0,
                            F = x.f ?? 0 ,
                            Unappr = x.Unappr ?? 0,
                            Unposted = x.Unposted ?? 0
                        }).ToList()
                    });
        }
        private Dictionary<string, List<WoLineItem>> BuildWoLineItems(IEnumerable<WoLineItem> data)
{
    return data
        .Where(x => !string.IsNullOrEmpty(x.ref_no))
        .GroupBy(x => x.ref_no!)
        .ToDictionary(
            g => g.Key,
            g => g.Select(x => new WoLineItem
            {
                Item = x.Item,
                Desc = x.Desc,
                Unit = x.Unit,
                Qty = x.Qty,
                Rate = x.Rate,
                Amt = x.Amt,
                Tax = x.Tax,
                Bqty = x.Bqty,
                WpRet = x.WpRet,
                Code = x.Code,
                Cdesc = x.Cdesc
            }).ToList()
        );
}
        public Dictionary<string ,List<BilledDetail>>BuildBilledDetails(IEnumerable<BilledDetail> data)
        {
            return data
                .Where(x => !string.IsNullOrEmpty(x.expense_account_code))
                .GroupBy(x => x.expense_account_code!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new BilledDetail
                    {
                        No = x.No,
                        Status = x.Status,
                        Date = x.Date,
                        Acct = x.Acct,
                        Amt = x.Amt,
                    }).ToList()
                );
        }
        private Dictionary<string, List<PoLineItem>> BuildPoLineItems(IEnumerable<PoLineItem> data)
        {
            return data
                .Where(x => !string.IsNullOrEmpty(x.ref_no))
                .GroupBy(x => x.ref_no)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new PoLineItem
                    {
                        Item = x.Item,
                        Desc = x.Desc,
                        Unit = x.Unit,
                        Qty = x.Qty,
                        Rate = x.Rate,
                        Amt = x.Amt,
                        Tax = x.Tax,
                        Bqty = x.Bqty,
                        WpRet = x.WpRet,
                        Code = x.Code,
                        Cdesc = x.Cdesc
                    }).ToList()
                );
        }
        private async Task<List<BoqTot>> GetBoqTotalsAsync()
        {
            try
            {
                string query = @"
            SELECT
                project_id,
                design,
                order_val
            FROM [CockpitMart].[dbo].[v_boq_totals];";

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<BoqTot>(query);

                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public  async Task<List<RetentionItem>> Getfact_RetentionAsync()
        {
            try
            {
                string query = @"
            SELECT
                project_id,
                vendor_name,
                held_amt,release_due,bills_count,payment_hold
               
            FROM [CockpitMart].[dbo].[v_fact_Retention];";

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<RetentionItem>(query);

                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<BoqItem>> Getfact_BudgetItemAsync()
        {
            try
            {
                string query = @"
                               SELECT 
                                   project_Id ,
                                   Code,
                                   [Description],   -- 'Description' is a reserved keyword, so we bracket it
                                   U,
                                   Dq,
                                   Oq,
                                   Dr,
                                   Bv,
                                   Wv
                               FROM [CockpitMart].[dbo].[vw_fact_boq]
                               ORDER BY Code DESC;";   // matches your original ordering (by first column descending)

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<BoqItem>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<WoDetail>> Getfact_WoDetailsAsync()
        {
            try
            {
                string query = @"
                               SELECT 
                                   project_Id ,
                                   No,
                                    Convert(varchar(10),Date,103) As Date ,
                                   Status,
                                   Vendor,
                                   Amt,
                                   Bqty,
                                   Item
                                   [Desc],   -- 'Description' is a reserved keyword, so we bracket it
                                   Unit,
                                   Qty,
                                   Rate,
                                   Code
                               FROM [CockpitMart].[dbo].[vw_woDetails]
                               ORDER BY Vendor ASC;";   // matches your original ordering (by first column descending)

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<WoDetail>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<IEnumerable<WoLineItem>> Getfact_WoLineItemAsync()
        {
            try
            {
                string query = @"
                               SELECT 
                                   project_Id ,
                                   ref_no,
                                    line_no,
                                   Item,
                                   [Desc], -- 'Description' is a reserved keyword, so we bracket it
                                   Unit,
                                   Qty,
                                   Bqty
                                   Rate,   
                                   Amt,
                                   Tax,
                                   WpRet,
                                   Code ,
                                   Cdesc
                               FROM [CockpitMart].[dbo].[vw_fact_powo_line]
                               Order by ref_no DESC ,Line_No DESc;";   // matches your original ordering (by first column descending)

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<WoLineItem>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<IEnumerable<PoLineItem>> Getfact_POLineItemAsync()
        {
            try
            {
                string query = @"
                               SELECT 
                                   project_Id ,
                                   ref_no,
                                    line_no,
                                   Item,
                                   [Desc], -- 'Description' is a reserved keyword, so we bracket it
                                   Unit,
                                   Qty,
                                   Bqty
                                   Rate,   
                                   Amt,
                                   Tax,
                                   WpRet,
                                   Code ,
                                   Cdesc
                               FROM [CockpitMart].[dbo].[vw_fact_powo_line]
                               Order by ref_no DESC ,Line_No DESc;";   // matches your original ordering (by first column descending)

                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<PoLineItem>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<IEnumerable<BudgetTree_ModelBinding>> GetBudgetTreeDataAsync()
        {
            try
            {
                string query = @"
                               SELECT 
                                   Project_Id,
                                   parent_code,
                                   parent_desc,
                                   code,
                                   child_desc,
                                   a,
                                   b,
                                   c_,
                                   d,
                                   e,
                                   f
                               FROM [CockpitMart].[dbo].[v_budget_tree]
                               ORDER BY Project_Id, parent_code, code;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<BudgetTree_ModelBinding>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<IEnumerable<BudgetItem>> GetBudgetDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_Budget]
                               ORDER BY Code DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<BudgetItem>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<VendorPerformance>> GetVendorPerformanceDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_vendor_perf]
                               ORDER BY vendor_name ASC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<VendorPerformance>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<WoSummary>> GetWoSummaryDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_wos_Summery]
                               ORDER BY GlDate DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<WoSummary>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<PoSummary>> GetPoSummaryDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_PO_Summery]
                               ORDER BY GlDate DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<PoSummary>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<InvoiceItem>> GetInvoicesItemDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_invoices_Bill_headerT]
                               ORDER BY GlDate DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<InvoiceItem>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<V_project_card>> GetprojectcardSummaryDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_project_card]
                               ORDER BY name DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<V_project_card>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<BilledDetail>> GetBilledDetailsDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[vw_billed_Details]
                               ORDER BY Date DESC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<BilledDetail>(query);
                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<List<VendorSummary>> GetVendorsDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_vendor_perf]
                               ORDER BY vendor_name ASC;";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryAsync<VendorSummary>(query);
                    // Three Value is missing in this View AsnNo , wo ,po, woval 

                    return result.ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<ProjectTotalCount> GetProjectTotalCountDataAsync()
        {
            try
            {
                string query = @"
                               SELECT * FROM [CockpitMart].[dbo].[v_project_totalCount];";   // matches your original ordering (by first column descending)
                using (var connection = _dbConnection.CreateConnection())
                {
                    var result = await connection.QueryFirstAsync<ProjectTotalCount>(query);
                    // Three Value is missing in this View AsnNo , wo ,po, woval 

                    return result;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
    
}
