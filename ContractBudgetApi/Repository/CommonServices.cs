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

        public async Task<Root> GetBudgetSatementData()
        {
            string jsonPayload = @"{
  ""entity"": {
    ""name"": ""Joynest Premises Pvt Ltd"",
    ""group"": ""Hubtown Group"",
    ""gst"": """"
  },
  ""projects"": [
    {
      ""id"": ""FWG"",
      ""name"": ""Hubtown Seasons - F Wing"",
      ""loc"": ""Chembur, Mumbai"",
      ""stage"": ""Finishing"",
      ""isReal"": true,
      ""budget"": 64.41,
      ""committed"": 53.11,
      ""billed"": 51.519999999999996,
      ""available"": 5.67,
      ""retention"": 1.34,
      ""vendors"": 122,
      ""wos"": 40,
      ""alerts"": 9,
      ""boqDesign"": 30.13,
      ""boqOrder"": 53.99
    },
    {
      ""id"": ""TWR"",
      ""name"": ""Twenty Five South — Tower C"",
      ""loc"": ""Mahalaxmi, Mumbai"",
      ""stage"": ""Superstructure"",
      ""isReal"": false,
      ""budget"": 285.1,
      ""committed"": 254,
      ""billed"": 249.45,
      ""available"": 16.6,
      ""retention"": 10.2,
      ""vendors"": 18,
      ""wos"": 4,
      ""alerts"": 5,
      ""boqDesign"": 121,
      ""boqOrder"": 132.3
    }
  ],
  ""data"": {
    ""FWG"": {
      ""project"": {
        ""name"": ""Hubtown Seasons - F Wing"",
        ""loc"": ""Chembur, Mumbai"",
        ""entity"": ""Joynest Premises Pvt Ltd"",
        ""group"": ""Hubtown"",
        ""id"": ""FWG"",
        ""area"": null
      },
      ""totals"": {
        ""budget"": 64.41,
        ""committed"": 53.11,
        ""woBilled"": 45.93,
        ""directBilled"": 5.59,
        ""balance"": 7.18,
        ""available"": 5.67,
        ""retentionHeld"": 1.34,
        ""tdsYtd"": 0.42,
        ""gstInput"": 8.43,
        ""poValue"": 1.17,
        ""grnValue"": 1.17,
        ""counts"": {
          ""asn"": 3223,
          ""inv"": 672,
          ""wo"": 40,
          ""po"": 58,
          ""vendor"": 122,
          ""dept"": 45,
          ""poMat"": 13,
          ""poDept"": 45
        },
        ""unapproved"": 0,
        ""unposted"": 0,
        ""poValueMat"": 116.61,
        ""poValueSrv"": 1123.94,
        ""woValueWO"": 51.25,
        ""woValueDept"": 11.24,
        ""poValueDept"": 1123.94,
        ""invPaid"": 87.96,
        ""invBal"": 5,
        ""invTotal"": 93.63,
        ""vendorCount"": 122,
        ""vendorOrderValue"": 63.67,
        ""vendorBilled"": 93.62,
        ""vendorPaid"": 87.95,
        ""vendorBalance"": 5,
        ""vendorRetention"": 2.55,
        ""vendorCats"": {
          ""Contractor"": 28,
          ""Other"": 62,
          ""Service"": 25,
          ""Material"": 7
        },
        ""vendorContractors"": 28
      },
      ""budget"": [
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90101"",
          ""desc"": ""Foundation"",
          ""A"": 2.39,
          ""B"": 3.46,
          ""C"": 3.45,
          ""D"": -1.07,
          ""E"": 0.01,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90151"",
          ""desc"": ""Basement"",
          ""A"": 0.16,
          ""B"": 0.16,
          ""C"": 0.15,
          ""D"": 0,
          ""E"": 0.01,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90201"",
          ""desc"": ""Podium"",
          ""A"": 0.05,
          ""B"": 0.05,
          ""C"": 0.05,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90251"",
          ""desc"": ""RCC Superstructure"",
          ""A"": 12.7,
          ""B"": 12.92,
          ""C"": 12.48,
          ""D"": -0.24,
          ""E"": 0.44,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90301"",
          ""desc"": ""Finishing Work"",
          ""A"": 18.61,
          ""B"": 18.65,
          ""C"": 15.01,
          ""D"": 0,
          ""E"": 3.64,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.03
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90351"",
          ""desc"": ""Entrance and typical lobbies"",
          ""A"": 0.42,
          ""B"": 0.43,
          ""C"": 0.41,
          ""D"": 0,
          ""E"": 0.02,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.01
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90401"",
          ""desc"": ""Glazing and facade work"",
          ""A"": 3.23,
          ""B"": 3.21,
          ""C"": 3.17,
          ""D"": 0,
          ""E"": 0.04,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90451"",
          ""desc"": ""Plumbing"",
          ""A"": 1.39,
          ""B"": 1.37,
          ""C"": 1.03,
          ""D"": 0,
          ""E"": 0.34,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.01
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90501"",
          ""desc"": ""Electrical works"",
          ""A"": 3.1,
          ""B"": 3.08,
          ""C"": 1.89,
          ""D"": 0.01,
          ""E"": 1.19,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90551"",
          ""desc"": ""Mechanical Works"",
          ""A"": 1.12,
          ""B"": 1.13,
          ""C"": 0.96,
          ""D"": 0,
          ""E"": 0.17,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.01
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90601"",
          ""desc"": ""Fire fighting"",
          ""A"": 1.52,
          ""B"": 1.51,
          ""C"": 1.27,
          ""D"": 0,
          ""E"": 0.24,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90651"",
          ""desc"": ""Infraworks"",
          ""A"": 0.24,
          ""B"": 0.2,
          ""C"": 0.19,
          ""D"": 0.03,
          ""E"": 0.01,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90701"",
          ""desc"": ""Amenities"",
          ""A"": 0.26,
          ""B"": 0.26,
          ""C"": 0.26,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90751"",
          ""desc"": ""Sample Flat"",
          ""A"": 1.68,
          ""B"": 1.66,
          ""C"": 1.52,
          ""D"": 0,
          ""E"": 0.14,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90801"",
          ""desc"": ""Miscellaneous"",
          ""A"": 1.32,
          ""B"": 1.35,
          ""C"": 1.07,
          ""D"": 0,
          ""E"": 0.28,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.03
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90851"",
          ""desc"": ""Material"",
          ""A"": 1.43,
          ""B"": 1.29,
          ""C"": 1.06,
          ""D"": 0.14,
          ""E"": 0.22,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90901"",
          ""desc"": ""Purchase of Unit / Flat"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90951"",
          ""desc"": ""Lumpsum for Construction Con"",
          ""A"": 0.18,
          ""B"": 0.17,
          ""C"": 0.07,
          ""D"": 0,
          ""E"": 0.1,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91001"",
          ""desc"": ""Escalation & Material Basic "",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": -0.03,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.03
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91051"",
          ""desc"": ""Construction Claims"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91101"",
          ""desc"": ""Demolition & Dismantling"",
          ""A"": 0.03,
          ""B"": 0.03,
          ""C"": 0.03,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.01
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91151"",
          ""desc"": ""Machinery Hire Charges"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91201"",
          ""desc"": ""Renewable Energy Related Wor"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91251"",
          ""desc"": ""Building Automation"",
          ""A"": 0.06,
          ""B"": 0.04,
          ""C"": 0.02,
          ""D"": 0,
          ""E"": 0.02,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.01
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91351"",
          ""desc"": ""Security Expenses"",
          ""A"": 0.04,
          ""B"": 0.08,
          ""C"": 0.04,
          ""D"": 0,
          ""E"": 0.04,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": -0.04
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91401"",
          ""desc"": ""Electricity Expenses"",
          ""A"": 0.21,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0.19,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91451"",
          ""desc"": ""Site establishment & Admin"",
          ""A"": 0.03,
          ""B"": 0,
          ""C"": 0,
          ""D"": -0.25,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.27
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""91951"",
          ""desc"": ""Co-Ordination Fees"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""92001"",
          ""desc"": ""Project Management Consultan"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""92251"",
          ""desc"": ""Review Consultancy"",
          ""A"": 0.06,
          ""B"": 0.06,
          ""C"": 0.03,
          ""D"": 0,
          ""E"": 0.04,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""92551"",
          ""desc"": ""Leased Assets"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""92601"",
          ""desc"": ""Capital Work-In-Progress"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""92651"",
          ""desc"": ""Capital Work-In-Progress - C"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget for Approvals - Infra Services"",
          ""code"": ""91801"",
          ""desc"": ""Approvals - Infra Services"",
          ""A"": 0.05,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.05
        },
        {
          ""grp"": ""Budget for Approvals - Land & Property"",
          ""code"": ""91701"",
          ""desc"": ""Approvals - Land & Property"",
          ""A"": 0.74,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0.39,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.35
        },
        {
          ""grp"": ""Budget for Approvals Building"",
          ""code"": ""91751"",
          ""desc"": ""Approvals Building"",
          ""A"": 6.35,
          ""B"": 0,
          ""C"": 0,
          ""D"": 4.93,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 1.42
        },
        {
          ""grp"": ""Budget for Environmental Consultant Charges"",
          ""code"": ""92151"",
          ""desc"": ""Environmental Consultant Cha"",
          ""A"": 0.04,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.04
        },
        {
          ""grp"": ""Budget for Other Construction related consultants"",
          ""code"": ""92201"",
          ""desc"": ""Other Construction related C"",
          ""A"": 0.79,
          ""B"": 0.79,
          ""C"": 0.71,
          ""D"": 0,
          ""E"": 0.08,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.01
        },
        {
          ""grp"": ""Budget group for Architect Expenses"",
          ""code"": ""91901"",
          ""desc"": ""Architect Fees"",
          ""A"": 1.11,
          ""B"": 1,
          ""C"": 0.9,
          ""D"": 0,
          ""E"": 0.1,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.11
        },
        {
          ""grp"": ""Budget group for Brokerage"",
          ""code"": ""94001"",
          ""desc"": ""Brokerage"",
          ""A"": 4.23,
          ""B"": 0,
          ""C"": 0,
          ""D"": 1.38,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 2.85
        },
        {
          ""grp"": ""Budget group for Legal Fees - Others"",
          ""code"": ""92351"",
          ""desc"": ""Legal Fees -Others"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget group for Legal Fees -Sales"",
          ""code"": ""92301"",
          ""desc"": ""Legal Fees -Sales"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget group for Marketing Expenses"",
          ""code"": ""93001"",
          ""desc"": ""Marketing Expenses"",
          ""A"": 0.53,
          ""B"": 0.01,
          ""C"": 0.01,
          ""D"": 0.08,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.43
        },
        {
          ""grp"": ""Budget group for RCC Consultants"",
          ""code"": ""92101"",
          ""desc"": ""RCC Consultants"",
          ""A"": 0.26,
          ""B"": 0.13,
          ""C"": 0.12,
          ""D"": 0,
          ""E"": 0.01,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.13
        },
        {
          ""grp"": ""Budget group for Service Consultancy (MEP)"",
          ""code"": ""92051"",
          ""desc"": ""Service Consultancy (MEP)"",
          ""A"": 0.08,
          ""B"": 0.07,
          ""C"": 0.03,
          ""D"": 0,
          ""E"": 0.04,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.02
        },
        {
          ""grp"": ""Other / Ungrouped"",
          ""code"": ""91301"",
          ""desc"": ""LR Cost"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Other / Ungrouped"",
          ""code"": ""91601"",
          ""desc"": ""Land Acquisition Cost"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Other / Ungrouped"",
          ""code"": ""91651"",
          ""desc"": ""Land Conversion cost"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Other / Ungrouped"",
          ""code"": ""92501"",
          ""desc"": ""Owned Assets"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Other / Ungrouped"",
          ""code"": ""93011"",
          ""desc"": ""Sales Promotion"",
          ""A"": 0,
          ""B"": 0,
          ""C"": 0,
          ""D"": 0,
          ""E"": 0,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        }
      ],
      ""budgetTree"": {
        ""90101"": {
          ""desc"": ""Foundation"",
          ""children"": [
            {
              ""code"": ""70405"",
              ""desc"": ""Excavation and Backfilling"",
              ""A"": 9560000,
              ""B"": 8107670,
              ""C"": 7673411.94,
              ""D"": 25000,
              ""E"": 434258.06,
              ""F"": 1018071.94
            },
            {
              ""code"": ""70407"",
              ""desc"": ""Foundation RCC Work"",
              ""A"": 4780000,
              ""B"": 26394704,
              ""C"": 15967100.97,
              ""D"": 0,
              ""E"": 10427603.03,
              ""F"": -32042307.03
            },
            {
              ""code"": ""70406"",
              ""desc"": ""Foundation Building Protection"",
              ""A"": 4780000,
              ""B"": 51821,
              ""C"": 51751.7,
              ""D"": 0,
              ""E"": 69.3,
              ""F"": 4728109.7
            },
            {
              ""code"": ""70404"",
              ""desc"": ""Foundation Piling"",
              ""A"": 4780000,
              ""B"": 12500,
              ""C"": 10700,
              ""D"": 0,
              ""E"": 1800,
              ""F"": 4765700
            }
          ]
        },
        ""90151"": {
          ""desc"": ""Basement"",
          ""children"": [
            {
              ""code"": ""70410"",
              ""desc"": ""Basement Finishing Work"",
              ""A"": 533280,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 533280
            },
            {
              ""code"": ""70408"",
              ""desc"": ""Basement RCC"",
              ""A"": 533280,
              ""B"": 1486080,
              ""C"": 1412188.8,
              ""D"": 0,
              ""E"": 73891.2,
              ""F"": -1026691.2
            },
            {
              ""code"": ""70409"",
              ""desc"": ""Basement Building Protection"",
              ""A"": 533440,
              ""B"": 71914,
              ""C"": 69417.31,
              ""D"": 0,
              ""E"": 2496.69,
              ""F"": 459029.31
            }
          ]
        },
        ""90201"": {
          ""desc"": ""Podium"",
          ""children"": [
            {
              ""code"": ""70412"",
              ""desc"": ""Podium Finishing"",
              ""A"": 166650,
              ""B"": 267789,
              ""C"": 267789,
              ""D"": 0,
              ""E"": 0,
              ""F"": -101139
            },
            {
              ""code"": ""70413"",
              ""desc"": ""Podium Facade"",
              ""A"": 166700,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 166700
            },
            {
              ""code"": ""70411"",
              ""desc"": ""Podium RCC"",
              ""A"": 166650,
              ""B"": 198850,
              ""C"": 198850,
              ""D"": 0,
              ""E"": 0,
              ""F"": -32200
            }
          ]
        },
        ""90251"": {
          ""desc"": ""RCC Superstructure"",
          ""children"": [
            {
              ""code"": ""70415"",
              ""desc"": ""Special Shuttering"",
              ""A"": 42329100,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 42329100
            },
            {
              ""code"": ""70416"",
              ""desc"": ""Structural Fabrication Superstruct"",
              ""A"": 42329100,
              ""B"": 12302675.7,
              ""C"": 10933037.64,
              ""D"": 22600,
              ""E"": 1369638.06,
              ""F"": 28656786.24
            },
            {
              ""code"": ""70414"",
              ""desc"": ""RCC Superstructure"",
              ""A"": 42341800,
              ""B"": 116896440,
              ""C"": 113916035.04,
              ""D"": 0,
              ""E"": 2980404.96,
              ""F"": -77535044.96
            }
          ]
        },
        ""90301"": {
          ""desc"": ""Finishing Work"",
          ""children"": [
            {
              ""code"": ""70418"",
              ""desc"": ""Finishing Work Tiling and Flooring"",
              ""A"": 46525000,
              ""B"": 76469789.05,
              ""C"": 62197108.87,
              ""D"": 0,
              ""E"": 14272680.18,
              ""F"": -44217469.23
            },
            {
              ""code"": ""70421"",
              ""desc"": ""Doors"",
              ""A"": 46525000,
              ""B"": 26025261.79,
              ""C"": 21668142.99,
              ""D"": 0,
              ""E"": 4357118.8,
              ""F"": 16142619.41
            },
            {
              ""code"": ""70419"",
              ""desc"": ""Finishing Work Civil Misc. Interio"",
              ""A"": 46525000,
              ""B"": 40151972.57,
              ""C"": 28661906.68,
              ""D"": 0,
              ""E"": 11490065.89,
              ""F"": -5117038.46
            },
            {
              ""code"": ""70417"",
              ""desc"": ""Finishing Masonary Work"",
              ""A"": 46525000,
              ""B"": 43824755,
              ""C"": 39673847.63,
              ""D"": 0,
              ""E"": 4150907.37,
              ""F"": -1450662.37
            }
          ]
        },
        ""90351"": {
          ""desc"": ""Entrance and typical lobbies"",
          ""children"": [
            {
              ""code"": ""70422"",
              ""desc"": ""Entrance and Typical Lobbies"",
              ""A"": 4200000,
              ""B"": 4292334,
              ""C"": 4100592.96,
              ""D"": 0,
              ""E"": 191741.04,
              ""F"": -284075.04
            }
          ]
        },
        ""90401"": {
          ""desc"": ""Glazing and facade work"",
          ""children"": [
            {
              ""code"": ""70420"",
              ""desc"": ""Windows"",
              ""A"": 16150000,
              ""B"": 26533419,
              ""C"": 26455110.31,
              ""D"": 0,
              ""E"": 78308.69,
              ""F"": -10461727.69
            },
            {
              ""code"": ""70423"",
              ""desc"": ""Facade Work"",
              ""A"": 16150000,
              ""B"": 5574200,
              ""C"": 5202577.5,
              ""D"": 0,
              ""E"": 371622.5,
              ""F"": 10204177.5
            }
          ]
        },
        ""90451"": {
          ""desc"": ""Plumbing"",
          ""children"": [
            {
              ""code"": ""70425"",
              ""desc"": ""Plumbing Material"",
              ""A"": 6950000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 6950000
            },
            {
              ""code"": ""70424"",
              ""desc"": ""Plumbing Internal Work"",
              ""A"": 6950000,
              ""B"": 13740771.89,
              ""C"": 10755426,
              ""D"": 0,
              ""E"": 2985345.89,
              ""F"": -9776117.78
            }
          ]
        },
        ""90501"": {
          ""desc"": ""Electrical works"",
          ""children"": [
            {
              ""code"": ""70427"",
              ""desc"": ""Electrical Material"",
              ""A"": 15500000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 141135,
              ""E"": 0,
              ""F"": 15500000
            },
            {
              ""code"": ""70426"",
              ""desc"": ""Electrical Internal Work"",
              ""A"": 15500000,
              ""B"": 30751474,
              ""C"": 20673545.44,
              ""D"": 0,
              ""E"": 10077928.56,
              ""F"": -25329402.56
            }
          ]
        },
        ""90551"": {
          ""desc"": ""Mechanical Works"",
          ""children"": [
            {
              ""code"": ""70429"",
              ""desc"": ""Mechanical DG Sets & Pumps"",
              ""A"": 2800000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2800000
            },
            {
              ""code"": ""70430"",
              ""desc"": ""Mechanical Lifts & Escalators"",
              ""A"": 2800000,
              ""B"": 7938983,
              ""C"": 7938991.5,
              ""D"": 30795,
              ""E"": -8.5,
              ""F"": -5138974.5
            },
            {
              ""code"": ""70428"",
              ""desc"": ""Mechanical HVAC"",
              ""A"": 2800000,
              ""B"": 3322565,
              ""C"": 1648748,
              ""D"": 0,
              ""E"": 1673817,
              ""F"": -2196382
            },
            {
              ""code"": ""70431"",
              ""desc"": ""Mechanical Car Parking"",
              ""A"": 2800000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2800000
            }
          ]
        },
        ""90601"": {
          ""desc"": ""Fire fighting"",
          ""children"": [
            {
              ""code"": ""70432"",
              ""desc"": ""Fire Fighting"",
              ""A"": 15200000,
              ""B"": 15128715,
              ""C"": 12681042.85,
              ""D"": 0,
              ""E"": 2447672.15,
              ""F"": -2376387.15
            }
          ]
        },
        ""90651"": {
          ""desc"": ""Infraworks"",
          ""children"": [
            {
              ""code"": ""70457"",
              ""desc"": ""STP Septic tank"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70402"",
              ""desc"": ""Electric Receiving Stn"",
              ""A"": 200880,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 200880
            },
            {
              ""code"": ""70452"",
              ""desc"": ""Water supply networks"",
              ""A"": 199920,
              ""B"": 30000,
              ""C"": 30000,
              ""D"": 0,
              ""E"": 0,
              ""F"": 169920
            },
            {
              ""code"": ""70454"",
              ""desc"": ""Rain water harvesting  Recharge pi"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70434"",
              ""desc"": ""Infra Electrical"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70436"",
              ""desc"": ""Infra Car Parking"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70438"",
              ""desc"": ""Other Infrastructure Works"",
              ""A"": 199920,
              ""B"": 1540889,
              ""C"": 1598714.76,
              ""D"": 0,
              ""E"": -57825.76,
              ""F"": -1283143.24
            },
            {
              ""code"": ""70433"",
              ""desc"": ""Infra Plumbing"",
              ""A"": 199920,
              ""B"": 425000,
              ""C"": 425000,
              ""D"": 0,
              ""E"": 0,
              ""F"": -225080
            },
            {
              ""code"": ""70435"",
              ""desc"": ""Infra Road Network"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70437"",
              ""desc"": ""Landscaping and Tree"",
              ""A"": 199920,
              ""B"": 146750,
              ""C"": 146750,
              ""D"": 0,
              ""E"": 0,
              ""F"": 53170
            },
            {
              ""code"": ""70458"",
              ""desc"": ""Substation"",
              ""A"": 199920,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 199920
            },
            {
              ""code"": ""70453"",
              ""desc"": ""Borewell"",
              ""A"": 199920,
              ""B"": 144980,
              ""C"": 0,
              ""D"": 0,
              ""E"": 144980,
              ""F"": -90040
            }
          ]
        },
        ""90701"": {
          ""desc"": ""Amenities"",
          ""children"": [
            {
              ""code"": ""70439"",
              ""desc"": ""Clubhouse"",
              ""A"": 866840,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 866840
            },
            {
              ""code"": ""70440"",
              ""desc"": ""Swimming Pool"",
              ""A"": 866580,
              ""B"": 2579635,
              ""C"": 2574475,
              ""D"": 0,
              ""E"": 5160,
              ""F"": -1718215
            },
            {
              ""code"": ""70448"",
              ""desc"": ""Material kids play Equipment"",
              ""A"": 866580,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 866580
            }
          ]
        },
        ""90751"": {
          ""desc"": ""Sample Flat"",
          ""children"": [
            {
              ""code"": ""70447"",
              ""desc"": ""Sales office, Sample flat, Dream f"",
              ""A"": 8400000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 8400000
            },
            {
              ""code"": ""70441"",
              ""desc"": ""Sales Office, Sample Flat and Drea"",
              ""A"": 8400000,
              ""B"": 16644492,
              ""C"": 15231620.83,
              ""D"": 1000,
              ""E"": 1412871.17,
              ""F"": -9657363.17
            }
          ]
        },
        ""90801"": {
          ""desc"": ""Miscellaneous"",
          ""children"": [
            {
              ""code"": ""70442"",
              ""desc"": ""Civil Miscellaneous Works"",
              ""A"": 13200000,
              ""B"": 15087938,
              ""C"": 12793156.24,
              ""D"": 14097,
              ""E"": 2294781.76,
              ""F"": -4182719.76
            }
          ]
        },
        ""90851"": {
          ""desc"": ""Material"",
          ""children"": [
            {
              ""code"": ""70025"",
              ""desc"": ""Custom Duty"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70026"",
              ""desc"": ""Purchase of Material - Trading"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70021"",
              ""desc"": ""Materials Cement Water Proofing wo"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70019"",
              ""desc"": ""Materials Cement Finishing works-C"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70017"",
              ""desc"": ""Materials Cement RCC Works-Consump"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70015"",
              ""desc"": ""Tiles and Marble Consumption Accou"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70013"",
              ""desc"": ""Cement Consumption Account"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70022"",
              ""desc"": ""Materials Cement Misc. Works-Consu"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70011"",
              ""desc"": ""Other Materials-consumption"",
              ""A"": 893750,
              ""B"": 6168904.91,
              ""C"": 0,
              ""D"": 0,
              ""E"": 6168904.91,
              ""F"": -11444059.82
            },
            {
              ""code"": ""70020"",
              ""desc"": ""Materials Cement Flooring works-Co"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70018"",
              ""desc"": ""Materials Cement Masonry works-Con"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""70016"",
              ""desc"": ""RMC Consumption Account"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            },
            {
              ""code"": ""71415"",
              ""desc"": ""Loading and Unloading Charges"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 7000,
              ""D"": 11000,
              ""E"": -7000,
              ""F"": 900750
            },
            {
              ""code"": ""12718"",
              ""desc"": ""Inventory Material at Site"",
              ""A"": 893750,
              ""B"": 12883443.25,
              ""C"": 0,
              ""D"": 43875,
              ""E"": 12883443.25,
              ""F"": -24873136.5
            },
            {
              ""code"": ""70012"",
              ""desc"": ""Transport Charges Others"",
              ""A"": 893750,
              ""B"": 80720,
              ""C"": 50180,
              ""D"": 0,
              ""E"": 30540,
              ""F"": 782490
            },
            {
              ""code"": ""70014"",
              ""desc"": ""Steel Consumption Account"",
              ""A"": 893750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 893750
            }
          ]
        },
        ""90951"": {
          ""desc"": ""Lumpsum for Construction Con"",
          ""children"": [
            {
              ""code"": ""70443"",
              ""desc"": ""Lumpsum for Construction Contracts"",
              ""A"": 1800000,
              ""B"": 1729201.2,
              ""C"": 732456.05,
              ""D"": 0,
              ""E"": 996745.15,
              ""F"": -925946.35
            }
          ]
        },
        ""91101"": {
          ""desc"": ""Demolition & Dismantling"",
          ""children"": [
            {
              ""code"": ""70401"",
              ""desc"": ""Demolition and Dismantling"",
              ""A"": 300000,
              ""B"": 302050,
              ""C"": 252752.5,
              ""D"": 0,
              ""E"": 49297.5,
              ""F"": -51347.5
            }
          ]
        },
        ""91251"": {
          ""desc"": ""Building Automation"",
          ""children"": [
            {
              ""code"": ""70450"",
              ""desc"": ""LV System"",
              ""A"": 150000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 150000
            },
            {
              ""code"": ""70455"",
              ""desc"": ""Security & Servilence Systems"",
              ""A"": 150000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 150000
            },
            {
              ""code"": ""70456"",
              ""desc"": ""Building Automation"",
              ""A"": 150000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 150000
            },
            {
              ""code"": ""70027"",
              ""desc"": ""CCTV Camera and Video Door system"",
              ""A"": 150000,
              ""B"": 420650,
              ""C"": 176400,
              ""D"": 0,
              ""E"": 244250,
              ""F"": -514900
            }
          ]
        },
        ""91351"": {
          ""desc"": ""Security Expenses"",
          ""children"": [
            {
              ""code"": ""71416"",
              ""desc"": ""Security Charges Site"",
              ""A"": 400000,
              ""B"": 782000,
              ""C"": 399226.48,
              ""D"": 0,
              ""E"": 382773.52,
              ""F"": -764773.52
            }
          ]
        },
        ""91401"": {
          ""desc"": ""Electricity Expenses"",
          ""children"": [
            {
              ""code"": ""71414"",
              ""desc"": ""Electricity Expenses"",
              ""A"": 2100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 1886773,
              ""E"": 0,
              ""F"": 2100000
            }
          ]
        },
        ""91451"": {
          ""desc"": ""Site establishment & Admin"",
          ""children"": [
            {
              ""code"": ""71423"",
              ""desc"": ""Warehousing Charges"",
              ""A"": 46140,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 46140
            },
            {
              ""code"": ""71426"",
              ""desc"": ""Corpus\\hardship compensation"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71425"",
              ""desc"": ""CAR & Other Insurance Policies"",
              ""A"": 23160,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23160
            },
            {
              ""code"": ""71422"",
              ""desc"": ""Pest Control Charges"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 824290,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71420"",
              ""desc"": ""WATER CHARGES"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71428"",
              ""desc"": ""Contractors Entitlements Expenses"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71418"",
              ""desc"": ""Testing charges, QA,QC,SHE,etc"",
              ""A"": 23070,
              ""B"": 43500,
              ""C"": 43500,
              ""D"": 24500,
              ""E"": 0,
              ""F"": -20430
            },
            {
              ""code"": ""71424"",
              ""desc"": ""Labour Provident fund Reimbursemen"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71413"",
              ""desc"": ""Diesel Expenses (Civil)"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71427"",
              ""desc"": ""Recovery of Expenses"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71417"",
              ""desc"": ""Project Establishment  Cost"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            },
            {
              ""code"": ""71412"",
              ""desc"": ""Clearing and Forwarding Chgs"",
              ""A"": 23070,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 23070
            }
          ]
        },
        ""91701"": {
          ""desc"": ""Approvals - Land & Property"",
          ""children"": [
            {
              ""code"": ""71802"",
              ""desc"": ""Environmental Charges"",
              ""A"": 1850000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 1850000
            },
            {
              ""code"": ""71610"",
              ""desc"": ""Property Tax - Vergin Land"",
              ""A"": 1850000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 3938578,
              ""E"": 0,
              ""F"": 1850000
            },
            {
              ""code"": ""71801"",
              ""desc"": ""Land Record Charges"",
              ""A"": 1850000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 1850000
            },
            {
              ""code"": ""71811"",
              ""desc"": ""N.A. Tax"",
              ""A"": 1850000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 1850000
            }
          ]
        },
        ""91751"": {
          ""desc"": ""Approvals Building"",
          ""children"": [
            {
              ""code"": ""71826"",
              ""desc"": ""User Change Charges"",
              ""A"": 2184400,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2184400
            },
            {
              ""code"": ""71828"",
              ""desc"": ""SWD Pro rata Charges"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71830"",
              ""desc"": ""Podium Charges"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71832"",
              ""desc"": ""Labour Cess"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 1194000,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71835"",
              ""desc"": ""Solid Waste Management (SWM) NOC"",
              ""A"": 2184400,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2184400
            },
            {
              ""code"": ""71836"",
              ""desc"": ""Development Cess"",
              ""A"": 2184400,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2184400
            },
            {
              ""code"": ""71837"",
              ""desc"": ""Pest Control Operator (PCO) charge"",
              ""A"": 2184400,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2184400
            },
            {
              ""code"": ""71833"",
              ""desc"": ""Fungible  FSI"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 43430000,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71803"",
              ""desc"": ""Balcony Enclosure Fees"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71805"",
              ""desc"": ""BMC Expenses"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 22000,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71807"",
              ""desc"": ""Debris Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71809"",
              ""desc"": ""Layout Infrastructure Charges"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71813"",
              ""desc"": ""Revalidation Fees"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71815"",
              ""desc"": ""Scrutiny Fees"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 492000,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71817"",
              ""desc"": ""Staircase Premium"",
              ""A"": 2184400,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2184400
            },
            {
              ""code"": ""71823"",
              ""desc"": ""IOD Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71825"",
              ""desc"": ""Royalty Expenses"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 1099832,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71827"",
              ""desc"": ""Premium on Tender\\fsi"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71829"",
              ""desc"": ""Approvals from Statutory Authority"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 1653112,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71831"",
              ""desc"": ""Concesionare Fees"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71834"",
              ""desc"": ""Contribution to Slum Dwellers Soci"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71804"",
              ""desc"": ""BMC Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71806"",
              ""desc"": ""Capitation Fees"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71808"",
              ""desc"": ""Development Charges"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 2247000,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71810"",
              ""desc"": ""IOA Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71812"",
              ""desc"": ""Open Space Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71818"",
              ""desc"": ""Slumdwellers~ Maintenance  Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71822"",
              ""desc"": ""CFO Charges"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            },
            {
              ""code"": ""71824"",
              ""desc"": ""MMRDA Deposit"",
              ""A"": 2190750,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 2190750
            }
          ]
        },
        ""91801"": {
          ""desc"": ""Approvals - Infra Services"",
          ""children"": [
            {
              ""code"": ""71819"",
              ""desc"": ""Water Connection Expenses"",
              ""A"": 100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 100000
            },
            {
              ""code"": ""71821"",
              ""desc"": ""Security Deposit-Water Connection"",
              ""A"": 100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 100000
            },
            {
              ""code"": ""71820"",
              ""desc"": ""Security Deposit-Electricity"",
              ""A"": 100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 100000
            },
            {
              ""code"": ""71814"",
              ""desc"": ""Road Cutting Charges"",
              ""A"": 100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 100000
            },
            {
              ""code"": ""71816"",
              ""desc"": ""Sewerage/Extra Sewerage Charges"",
              ""A"": 100000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 100000
            }
          ]
        },
        ""91901"": {
          ""desc"": ""Architect Fees"",
          ""children"": [
            {
              ""code"": ""72001"",
              ""desc"": ""Architect Fees"",
              ""A"": 11100000,
              ""B"": 10024821,
              ""C"": 8985531.49,
              ""D"": 0,
              ""E"": 1039289.51,
              ""F"": 35889.49
            }
          ]
        },
        ""92051"": {
          ""desc"": ""Service Consultancy (MEP)"",
          ""children"": [
            {
              ""code"": ""72004"",
              ""desc"": ""Service Consultancy (MEP)"",
              ""A"": 800000,
              ""B"": 657486,
              ""C"": 631183.25,
              ""D"": 0,
              ""E"": 26302.75,
              ""F"": 116211.25
            }
          ]
        },
        ""92101"": {
          ""desc"": ""RCC Consultants"",
          ""children"": [
            {
              ""code"": ""72005"",
              ""desc"": ""RCC Consultants"",
              ""A"": 2600000,
              ""B"": 1300600,
              ""C"": 1185525.27,
              ""D"": 0,
              ""E"": 115074.73,
              ""F"": 1184325.27
            }
          ]
        },
        ""92151"": {
          ""desc"": ""Environmental Consultant Cha"",
          ""children"": [
            {
              ""code"": ""72008"",
              ""desc"": ""Environmental Consultant Charges"",
              ""A"": 200000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 200000
            },
            {
              ""code"": ""72010"",
              ""desc"": ""Consultants~ Fees Reimbursement (A"",
              ""A"": 200000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 200000
            }
          ]
        },
        ""92201"": {
          ""desc"": ""Other Construction related C"",
          ""children"": [
            {
              ""code"": ""72012"",
              ""desc"": ""Other Construction related Consult"",
              ""A"": 2633860,
              ""B"": 3803183,
              ""C"": 2993559.31,
              ""D"": 40000,
              ""E"": 809623.69,
              ""F"": -1978946.69
            },
            {
              ""code"": ""72007"",
              ""desc"": ""Professional Fees (Projects)"",
              ""A"": 2633070,
              ""B"": 3850000,
              ""C"": 3849998.8,
              ""D"": 0,
              ""E"": 1.2,
              ""F"": -1216931.2
            },
            {
              ""code"": ""72011"",
              ""desc"": ""Other D&D Consultants"",
              ""A"": 2633070,
              ""B"": 842100,
              ""C"": 518000,
              ""D"": 0,
              ""E"": 324100,
              ""F"": 1466870
            }
          ]
        },
        ""92251"": {
          ""desc"": ""Review Consultancy"",
          ""children"": [
            {
              ""code"": ""72015"",
              ""desc"": ""Liasioning Consultancy"",
              ""A"": 300000,
              ""B"": 619000,
              ""C"": 269000,
              ""D"": 0,
              ""E"": 350000,
              ""F"": -669000
            },
            {
              ""code"": ""72013"",
              ""desc"": ""Review Consultancy"",
              ""A"": 300000,
              ""B"": 163000,
              ""C"": 0,
              ""D"": 0,
              ""E"": 163000,
              ""F"": -26000
            }
          ]
        },
        ""93001"": {
          ""desc"": ""Marketing Expenses"",
          ""children"": [
            {
              ""code"": ""75122"",
              ""desc"": ""Sponsorship"",
              ""A"": 265000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 265000
            },
            {
              ""code"": ""75123"",
              ""desc"": ""Exhibition Expenses"",
              ""A"": 530000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 530000
            },
            {
              ""code"": ""75111"",
              ""desc"": ""Advertising Expenses"",
              ""A"": 1590000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 80072,
              ""E"": 0,
              ""F"": 1590000
            },
            {
              ""code"": ""75112"",
              ""desc"": ""Sales Brochure Stationery Exp"",
              ""A"": 530000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 530000
            },
            {
              ""code"": ""75114"",
              ""desc"": ""Sales Promotion Expenses"",
              ""A"": 265000,
              ""B"": 112000,
              ""C"": 112000,
              ""D"": 700000,
              ""E"": 0,
              ""F"": 153000
            },
            {
              ""code"": ""75117"",
              ""desc"": ""Beautification expenses (Garden/Ro"",
              ""A"": 530000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 0,
              ""E"": 0,
              ""F"": 530000
            },
            {
              ""code"": ""75113"",
              ""desc"": ""Marketing Consultancy Charges"",
              ""A"": 1590000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 55000,
              ""E"": 0,
              ""F"": 1590000
            }
          ]
        },
        ""94001"": {
          ""desc"": ""Brokerage"",
          ""children"": [
            {
              ""code"": ""75116"",
              ""desc"": ""Brokerage on Sale"",
              ""A"": 42300000,
              ""B"": 0,
              ""C"": 0,
              ""D"": 16183303.92,
              ""E"": 0,
              ""F"": 42300000
            }
          ]
        }
      },
      ""woDetails"": [
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS951"",
          ""desc"": ""P/L Rubble soling"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 57,
          ""rate"": 1777,
          ""amt"": 101289,
          ""bqty"": 56.42,
          ""bamt"": 100258.34,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS951"",
          ""desc"": ""RMC M15 grade"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 108,
          ""rate"": 7580,
          ""amt"": 818640,
          ""bqty"": 107.84,
          ""bamt"": 817427.2,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBCON003"",
          ""desc"": ""RMC M30 grade"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 1231,
          ""rate"": 8967,
          ""amt"": 11038377,
          ""bqty"": 1230.07,
          ""bamt"": 11030037.69,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS061"",
          ""desc"": ""Miscellaneous items for Reinforcement steel  work"",
          ""unit"": ""Metric Ton"",
          ""qty"": 135,
          ""rate"": 83000,
          ""amt"": 11205000,
          ""bqty"": 134.84,
          ""bamt"": 11192135,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS950"",
          ""desc"": ""Plywood Shuttering works"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 2969,
          ""rate"": 642,
          ""amt"": 1906098,
          ""bqty"": 2968.23,
          ""bamt"": 1905603.79,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS939"",
          ""desc"": ""Breaking of 150mm/100mm/75mm Thick Brick wall with Electrica"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 100,
          ""rate"": 350,
          ""amt"": 35000,
          ""bqty"": 41.33,
          ""bamt"": 14465.5,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBCON005"",
          ""desc"": ""Providing & laying in position approved RMC including vibrat"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 69,
          ""rate"": 18700,
          ""amt"": 1290300,
          ""bqty"": 68.09,
          ""bamt"": 1273348.45,
          ""code"": ""70407""
        },
        {
          ""no"": ""PURCHORD9487"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""AADISHAKTI WATER SUPPLIERS"",
          ""item"": ""XJOBMIS924"",
          ""desc"": ""Miscellaneous items for Water supply networks [Supply of Bor"",
          ""unit"": ""Number"",
          ""qty"": 20,
          ""rate"": 1500,
          ""amt"": 30000,
          ""bqty"": 20,
          ""bamt"": 30000,
          ""code"": ""70452""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBPLR102"",
          ""desc"": ""Extra and Over Item No 14 of Main Work Items Single Coat Pla"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 10760,
          ""rate"": 48,
          ""amt"": 516480,
          ""bqty"": 9243.07,
          ""bamt"": 443667.36,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBPLR107"",
          ""desc"": ""Extra and Over Item No 12 of Main Work Items Double Coat Pla"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 13750,
          ""rate"": 98,
          ""amt"": 1347500,
          ""bqty"": 13330.71,
          ""bamt"": 1306409.58,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBWPR107"",
          ""desc"": ""Providing Block bats / Brickbats in the Chajja To create the"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 32,
          ""rate"": 5200,
          ""amt"": 166400,
          ""bqty"": 31.58,
          ""bamt"": 164237.84,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS238"",
          ""desc"": ""Providing And Applying Two Coats of Polywalk WP Coating Of S"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 580,
          ""rate"": 365,
          ""amt"": 211700,
          ""bqty"": 552.01,
          ""bamt"": 201483.65,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS238"",
          ""desc"": ""Extra & Over Item No of Main Work Order Item No 17 Providing"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 606,
          ""rate"": 1262,
          ""amt"": 764772,
          ""bqty"": 570,
          ""bamt"": 719340,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS238"",
          ""desc"": ""Providing and applying an average 6 to 8 mm thick gypsum pla"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 2745,
          ""rate"": 399,
          ""amt"": 1095255,
          ""bqty"": 1666.98,
          ""bamt"": 665125.02,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBPAI102"",
          ""desc"": ""Providing & applying of two coats of Premium plastic emulsio"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 23326,
          ""rate"": 210,
          ""amt"": 4898460,
          ""bqty"": 12061.34,
          ""bamt"": 2532881.4,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBPAI106"",
          ""desc"": ""Providing & applying two coats of Asian Paints make Apex Ult"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 3808,
          ""rate"": 220,
          ""amt"": 837760,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBPAI107"",
          ""desc"": ""Providing & applying 2 or more coats of White washing in all"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 1800,
          ""rate"": 70,
          ""amt"": 126000,
          ""bqty"": 1686.44,
          ""bamt"": 118050.8,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS914"",
          ""desc"": ""Waterproofing of Toilets- Providing and applying waterproofi"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 1380,
          ""rate"": 4250,
          ""amt"": 5865000,
          ""bqty"": 1351.7,
          ""bamt"": 5744725,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS237"",
          ""desc"": ""Providing and fixing of white Indian Marble 18 to 20mm thick"",
          ""unit"": ""R Meter"",
          ""qty"": 1750,
          ""rate"": 375,
          ""amt"": 656250,
          ""bqty"": 1703,
          ""bamt"": 638625,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS237"",
          ""desc"": ""Providing and fixing of white Indian Marble 18 to 20mm thick"",
          ""unit"": ""R Meter"",
          ""qty"": 3100,
          ""rate"": 437,
          ""amt"": 1354700,
          ""bqty"": 3049,
          ""bamt"": 1332413,
          ""code"": ""70419""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBTRA021"",
          ""desc"": ""Providing Welding and Fixing Template in RCC Beam for Fabric"",
          ""unit"": ""Kilograms"",
          ""qty"": 125,
          ""rate"": 150,
          ""amt"": 18750,
          ""bqty"": 105.27,
          ""bamt"": 15790.5,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD5347"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KREATIVE ENGINEERING AND ELECT"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Installation Testing and Commissionin"",
          ""unit"": ""Lumpsum"",
          ""qty"": 2721600,
          ""rate"": 1,
          ""amt"": 2721600,
          ""bqty"": 2633020,
          ""bamt"": 2633020,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD5372"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R-LITE ELECTRICALS"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 23906292,
          ""rate"": 1,
          ""amt"": 23906292,
          ""bqty"": 12817397.5,
          ""bamt"": 12817397.5,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD6479"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TREEN TECHNOLOGIES"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 84050,
          ""rate"": 1,
          ""amt"": 84050,
          ""bqty"": 84050,
          ""bamt"": 84050,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD6514"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R-LITE ELECTRICALS"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 62212,
          ""rate"": 1,
          ""amt"": 62212,
          ""bqty"": 57152,
          ""bamt"": 57152,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD7564"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNGRID ENERGY SYSTEMS PRIVATE"",
          ""item"": ""XJOBMIS803"",
          ""desc"": ""Miscellaneous work for Low side cable termination as per att"",
          ""unit"": ""Number"",
          ""qty"": 721875,
          ""rate"": 1,
          ""amt"": 721875,
          ""bqty"": 577500,
          ""bamt"": 577500,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD7564"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNGRID ENERGY SYSTEMS PRIVATE"",
          ""item"": ""XJOBMIS803"",
          ""desc"": ""Miscellaneous work for Low side cable termination as per att"",
          ""unit"": ""Number"",
          ""qty"": 309375,
          ""rate"": 1,
          ""amt"": 309375,
          ""bqty"": 247500,
          ""bamt"": 247500,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD8004"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""BRIDGEWAY ELECTRIK SOLUTIONS P"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 351720,
          ""rate"": 1,
          ""amt"": 351720,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD8165"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TREEN TECHNOLOGIES"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 127035,
          ""rate"": 1,
          ""amt"": 127035,
          ""bqty"": 127035,
          ""bamt"": 127035,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD8348"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ESFRO SOLUTIONS PRIVATE LIMITE"",
          ""item"": ""XJOBMIS837"",
          ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common area,"",
          ""unit"": ""Lumpsum"",
          ""qty"": 2448565,
          ""rate"": 1,
          ""amt"": 2448565,
          ""bqty"": 2338125.44,
          ""bamt"": 2338125.44,
          ""code"": ""70426""
        },
        {
          ""no"": ""PURCHORD5541"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HRUTURAJ GREEN"",
          ""item"": ""XJOBMIS400"",
          ""desc"": ""i)Cleaning all garden area_x000D_ ii)filling red soil_x000D_"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 650,
          ""rate"": 127,
          ""amt"": 82550,
          ""bqty"": 650,
          ""bamt"": 82550,
          ""code"": ""70437""
        },
        {
          ""no"": ""PURCHORD5541"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HRUTURAJ GREEN"",
          ""item"": ""XJOBLSP005"",
          ""desc"": ""i)Cleaning all garden area_x000D_ ii)filling red soil_x000D_"",
          ""unit"": ""Number"",
          ""qty"": 250,
          ""rate"": 30,
          ""amt"": 7500,
          ""bqty"": 250,
          ""bamt"": 7500,
          ""code"": ""70437""
        },
        {
          ""no"": ""PURCHORD5541"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HRUTURAJ GREEN"",
          ""item"": ""XJOBMIS191"",
          ""desc"": ""Providing Paspalum carpet Lawn"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 1050,
          ""rate"": 54,
          ""amt"": 56700,
          ""bqty"": 1050,
          ""bamt"": 56700,
          ""code"": ""70437""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [H. E. NOC Remarks]"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Remarks from M.S. for existing water"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 9000,
          ""amt"": 9000,
          ""bqty"": 1,
          ""bamt"": 9000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Dry fitting permission (Terrace loop"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 50000,
          ""amt"": 50000,
          ""bqty"": 1,
          ""bamt"": 50000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Wet fitting P-form for permanent wat"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 80000,
          ""amt"": 80000,
          ""bqty"": 1,
          ""bamt"": 80000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Capacity of water tank from water wo"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 15000,
          ""amt"": 15000,
          ""bqty"": 1,
          ""bamt"": 15000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Certification and Permanent water co"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 75000,
          ""amt"": 75000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Down take approval from (P&R)]"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 50000,
          ""amt"": 50000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Road opening permission from A.E. (M"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [270 - A Certificate]"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [D.C.C. for  B.P.]"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 35000,
          ""amt"": 35000,
          ""bqty"": 1,
          ""bamt"": 35000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Smoke test Certificate (For Drainage"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 15000,
          ""amt"": 15000,
          ""bqty"": 1,
          ""bamt"": 15000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD7745"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""XCONSER073"",
          ""desc"": ""Liasioning Consultancy [Ponding test Certificate (For Draina"",
          ""unit"": ""Percentage"",
          ""qty"": 1,
          ""rate"": 15000,
          ""amt"": 15000,
          ""bqty"": 1,
          ""bamt"": 15000,
          ""code"": ""72015""
        },
        {
          ""no"": ""PURCHORD6204"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBFAC012"",
          ""desc"": ""Spider Glazing System: SPIDER GLAZING WITH ENTRANCE DOORS- P"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 159,
          ""rate"": 14290,
          ""amt"": 2272110,
          ""bqty"": 152.14,
          ""bamt"": 2174080.6,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD6204"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBFAC013"",
          ""desc"": ""Vision Glass : 6mm+1.52 Sentry glass +6mm, Extra over on the"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 159,
          ""rate"": 6810,
          ""amt"": 1082790,
          ""bqty"": 152.14,
          ""bamt"": 1036073.4,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD6204"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBSPL028"",
          ""desc"": ""Curtain wall system- Design, Fabrication, Installation, Prot"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 114,
          ""rate"": 8150,
          ""amt"": 929100,
          ""bqty"": 112.95,
          ""bamt"": 920542.5,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD6204"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBFAC013"",
          ""desc"": ""Vision Glass : 6mm+1.52 Sentry glass +6mm, Extra over on the"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 114,
          ""rate"": 7680,
          ""amt"": 875520,
          ""bqty"": 112.95,
          ""bamt"": 867456,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD6204"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBFAC011"",
          ""desc"": ""Patch Fitting Doors : Extra over on the item 1- The entrance"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 21.6,
          ""rate"": 11050,
          ""amt"": 238680,
          ""bqty"": 18.5,
          ""bamt"": 204425,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD6354"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KREEM CREATIONS"",
          ""item"": ""XJOBMIS084"",
          ""desc"": ""Providing and fixing H frame scaffolding. size 6.5ft x 3.3ft"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 8000,
          ""rate"": 22,
          ""amt"": 176000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70423""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS917"",
          ""desc"": ""APPLICATION OF BONDING AGENT AT JUNCTION OF NEW & OLD CONCRE"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 331,
          ""rate"": 688,
          ""amt"": 227728,
          ""bqty"": 330.8,
          ""bamt"": 227590.4,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS917"",
          ""desc"": ""Providing and Applying Curing Compound to Concrete Surface a"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 185,
          ""rate"": 50,
          ""amt"": 9250,
          ""bqty"": 180.61,
          ""bamt"": 9030.5,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLB002"",
          ""desc"": ""Providing and fixing of approved Restile Tile of Size 600mm "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 250,
          ""rate"": 2201,
          ""amt"": 550250,
          ""bqty"": 93.38,
          ""bamt"": 205529.38,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBENT010"",
          ""desc"": ""Hilux - False Ceiling- Supply & installation of hilux 8mm th"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 878,
          ""rate"": 1350,
          ""amt"": 1185300,
          ""bqty"": 694.56,
          ""bamt"": 937656,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLB004"",
          ""desc"": ""\""Providing and fixing Italian marble cladding of 16-18 mm th"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 80,
          ""rate"": 2500,
          ""amt"": 200000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLB004"",
          ""desc"": ""\""Providing and fixing Italian marble cladding of 16-18 mm th"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 115,
          ""rate"": 3315,
          ""amt"": 381225,
          ""bqty"": 77,
          ""bamt"": 255255,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD5388"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PAINTTECH INFRA LLP"",
          ""item"": ""XJOBMIS244"",
          ""desc"": ""Providing and applying Elastoroof PU Single Component, Liqui"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 995,
          ""rate"": 855,
          ""amt"": 850725,
          ""bqty"": 992.68,
          ""bamt"": 848740.54,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD7352"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS918"",
          ""desc"": ""Miscellaneous items for Sample flat [Civil Work-Tiling And F"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1267628,
          ""rate"": 1,
          ""amt"": 1267628,
          ""bqty"": 1267627,
          ""bamt"": 1267627,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD7352"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS918"",
          ""desc"": ""Miscellaneous items for Sample flat [Pop / False Ceiling Wor"",
          ""unit"": ""Lumpsum"",
          ""qty"": 305750,
          ""rate"": 1,
          ""amt"": 305750,
          ""bqty"": 210178,
          ""bamt"": 210178,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD7352"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS918"",
          ""desc"": ""Miscellaneous items for Sample flat [Carpentry Work]"",
          ""unit"": ""Lumpsum"",
          ""qty"": 6495904,
          ""rate"": 1,
          ""amt"": 6495904,
          ""bqty"": 6446454,
          ""bamt"": 6446454,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD7352"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS918"",
          ""desc"": ""Miscellaneous items for Sample flat [Painting & Polishing Wo"",
          ""unit"": ""Lumpsum"",
          ""qty"": 791850,
          ""rate"": 1,
          ""amt"": 791850,
          ""bqty"": 464230,
          ""bamt"": 464230,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD7352"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS917"",
          ""desc"": ""Miscellaneous items for Specialised work [Plumbing Work]"",
          ""unit"": ""Lumpsum"",
          ""qty"": 899020,
          ""rate"": 1,
          ""amt"": 899020,
          ""bqty"": 899020,
          ""bamt"": 899020,
          ""code"": ""70441""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBPW016"",
          ""desc"": ""Providing and injecting pre-constructional anti-termite trea"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 260,
          ""rate"": 97,
          ""amt"": 25220,
          ""bqty"": 258.33,
          ""bamt"": 25058.01,
          ""code"": ""70409""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBPW001"",
          ""desc"": ""Providing Box type cement based Waterproofing treatment for "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 14,
          ""rate"": 1262,
          ""amt"": 17668,
          ""bqty"": 13.32,
          ""bamt"": 16809.84,
          ""code"": ""70409""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBPW002"",
          ""desc"": ""Providing Box type cement based Waterproofing treatment for "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 23,
          ""rate"": 1262,
          ""amt"": 29026,
          ""bqty"": 21.83,
          ""bamt"": 27549.46,
          ""code"": ""70409""
        },
        {
          ""no"": ""PURCHORD10588"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""RAIN WATER PROJECT INFRA"",
          ""item"": ""XJOBWELL01"",
          ""desc"": ""nan"",
          ""unit"": ""Lumpsum"",
          ""qty"": 144980,
          ""rate"": 1,
          ""amt"": 144980,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70453""
        },
        {
          ""no"": ""PURCHORD9601"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""AZONIC INTERNATIONAL"",
          ""item"": ""XJOBMIS925"",
          ""desc"": ""Miscellaneous items for Nallah diversion works"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1,
          ""rate"": 425000,
          ""amt"": 425000,
          ""bqty"": 1,
          ""bamt"": 425000,
          ""code"": ""70433""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBEAR001"",
          ""desc"": ""Excavation in all types of soil except hard rock for depth 0"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 3787,
          ""rate"": 696,
          ""amt"": 2635752,
          ""bqty"": 3786.68,
          ""bamt"": 2635529.28,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBEAR026"",
          ""desc"": ""Backfilling with available earth"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 2134,
          ""rate"": 187,
          ""amt"": 399058,
          ""bqty"": 2133.18,
          ""bamt"": 398904.66,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBEAR001"",
          ""desc"": ""Excavation in all types of soil / rock  (Gravel, sand, soft "",
          ""unit"": ""Cu. Meter"",
          ""qty"": 1777,
          ""rate"": 805,
          ""amt"": 1430485,
          ""bqty"": 1776.11,
          ""bamt"": 1429768.55,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEAR001"",
          ""desc"": ""Excavation in all types of soil  by mechanical means over th"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 350,
          ""rate"": 525,
          ""amt"": 183750,
          ""bqty"": 328.21,
          ""bamt"": 172310.25,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEAR026"",
          ""desc"": ""Manually Backfilling with material available at site or eart"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 34,
          ""rate"": 500,
          ""amt"": 17000,
          ""bqty"": 31.99,
          ""bamt"": 15995,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEAR001"",
          ""desc"": ""Earth work in excavation in all kinds of soils for pile cap,"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 1230,
          ""rate"": 2000,
          ""amt"": 2460000,
          ""bqty"": 1219.02,
          ""bamt"": 2438040,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEAR007"",
          ""desc"": ""Excavation in all types of soil  by mechanical means over th"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 750,
          ""rate"": 375,
          ""amt"": 281250,
          ""bqty"": 747.03,
          ""bamt"": 280136.79,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEAR026"",
          ""desc"": ""Manually Backfilling with material available at site or eart"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 2155,
          ""rate"": 325,
          ""amt"": 700375,
          ""bqty"": 2149.93,
          ""bamt"": 698727.25,
          ""code"": ""70405""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBPIL204"",
          ""desc"": ""Providing & laying 230 mm thick dry rubble stone soling, voi"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 5,
          ""rate"": 2500,
          ""amt"": 12500,
          ""bqty"": 4.28,
          ""bamt"": 10700,
          ""code"": ""70404""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBWOD101"",
          ""desc"": ""Red miranti door frames"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 25,
          ""rate"": 141123,
          ""amt"": 3528075,
          ""bqty"": 21.36,
          ""bamt"": 3014810.65,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBWOD111"",
          ""desc"": ""Providing and fixing One Hour Fire Rated 45mm thick Fire ret"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 119,
          ""rate"": 12750,
          ""amt"": 1517250,
          ""bqty"": 96.21,
          ""bamt"": 1226677.5,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBWOD112"",
          ""desc"": ""Providing and fixing factory made 35mm thick solid core flus"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 42,
          ""rate"": 10250,
          ""amt"": 430500,
          ""bqty"": 41.28,
          ""bamt"": 423120,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS261"",
          ""desc"": ""Shera Board Ceiling Providing & fixing 8mm thick Shera board"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 540,
          ""rate"": 3500,
          ""amt"": 1890000,
          ""bqty"": 528.32,
          ""bamt"": 1849120,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS261"",
          ""desc"": ""Providing and Fixing Window with Heavy duty tarpaulin with s"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 2790,
          ""rate"": 470,
          ""amt"": 1311300,
          ""bqty"": 2734.69,
          ""bamt"": 1285304.3,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBWOD112"",
          ""desc"": ""Providing and fixing superior quality factory made flush doo"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 312,
          ""rate"": 8755,
          ""amt"": 2731560,
          ""bqty"": 203.03,
          ""bamt"": 1777527.65,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBWOD112"",
          ""desc"": ""Providing and fixing superior quality factory made flush doo"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 548,
          ""rate"": 8265,
          ""amt"": 4529220,
          ""bqty"": 440.76,
          ""bamt"": 3642881.4,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBWOD112"",
          ""desc"": ""Providing and fixing superior quality factory made flush doo"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 70,
          ""rate"": 8755,
          ""amt"": 612850,
          ""bqty"": 64.8,
          ""bamt"": 567324,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS255"",
          ""desc"": ""Miscellaneous items for Wood work (R Meter)"",
          ""unit"": ""R Meter"",
          ""qty"": 240,
          ""rate"": 502,
          ""amt"": 120480,
          ""bqty"": 186,
          ""bamt"": 93372,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS260"",
          ""desc"": ""Miscellaneous items for Wood work (Number)"",
          ""unit"": ""Number"",
          ""qty"": 380,
          ""rate"": 2900,
          ""amt"": 1102000,
          ""bqty"": 261,
          ""bamt"": 756900,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS255"",
          ""desc"": ""Miscellaneous items for Wood work (R Meter)"",
          ""unit"": ""R Meter"",
          ""qty"": 2700,
          ""rate"": 555,
          ""amt"": 1498500,
          ""bqty"": 2073.5,
          ""bamt"": 1150792.5,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS255"",
          ""desc"": ""Miscellaneous items for Wood work (R Meter)"",
          ""unit"": ""R Meter"",
          ""qty"": 490,
          ""rate"": 716,
          ""amt"": 350840,
          ""bqty"": 456.57,
          ""bamt"": 326904.12,
          ""code"": ""70421""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""Advance"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 360000,
          ""amt"": 360000,
          ""bqty"": 1,
          ""bamt"": 360000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""1st interim after approval of Camera angle or Modeling"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 360000,
          ""amt"": 360000,
          ""bqty"": 1,
          ""bamt"": 360000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""2nd Interim After Delivering all the renders or approval of "",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 360000,
          ""amt"": 360000,
          ""bqty"": 1.46,
          ""bamt"": 524998.8,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""Final payment after delivering the movie."",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 120000,
          ""amt"": 120000,
          ""bqty"": 0.54,
          ""bamt"": 64800,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""Advance"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 165000,
          ""amt"": 165000,
          ""bqty"": 1,
          ""bamt"": 165000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""1st interim, after approval of Camera angle or Modeling"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 165000,
          ""amt"": 165000,
          ""bqty"": 0.67,
          ""bamt"": 110550,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""2nd Interim, After Delivering all the renders or approval of"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 165000,
          ""amt"": 165000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3157"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""XCONSER058"",
          ""desc"": ""Final payment after delivering the movie."",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 55000,
          ""amt"": 55000,
          ""bqty"": 3,
          ""bamt"": 165000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3854"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""THE DESIGN CORE"",
          ""item"": ""XCONSER058"",
          ""desc"": ""Advance"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 300000,
          ""amt"": 300000,
          ""bqty"": 1,
          ""bamt"": 300000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD3854"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""THE DESIGN CORE"",
          ""item"": ""XCONSER058"",
          ""desc"": ""On every month from March 2024 for 18 Months"",
          ""unit"": ""Number"",
          ""qty"": 18,
          ""rate"": 100000,
          ""amt"": 1800000,
          ""bqty"": 18,
          ""bamt"": 1800000,
          ""code"": ""72007""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 10200,
          ""rate"": 675,
          ""amt"": 6885000,
          ""bqty"": 10179.99,
          ""bamt"": 6871496.11,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 10200,
          ""rate"": 110,
          ""amt"": 1122000,
          ""bqty"": 10179.97,
          ""bamt"": 1119796.7,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 10200,
          ""rate"": 390,
          ""amt"": 3978000,
          ""bqty"": 10179.08,
          ""bamt"": 3969842.37,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 7589,
          ""rate"": 671,
          ""amt"": 5092219,
          ""bqty"": 7581.03,
          ""bamt"": 5086871.13,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 7589,
          ""rate"": 110,
          ""amt"": 834790,
          ""bqty"": 7581.03,
          ""bamt"": 833913.3,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 7589,
          ""rate"": 390,
          ""amt"": 2959710,
          ""bqty"": 7581.03,
          ""bamt"": 2956601.7,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 5147,
          ""rate"": 650,
          ""amt"": 3345550,
          ""bqty"": 5105.99,
          ""bamt"": 3318893.5,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 5147,
          ""rate"": 80,
          ""amt"": 411760,
          ""bqty"": 5105.99,
          ""bamt"": 408479.2,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD5677"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""item"": ""XJOBALU110"",
          ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & windows"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 5147,
          ""rate"": 370,
          ""amt"": 1904390,
          ""bqty"": 5105.99,
          ""bamt"": 1889216.3,
          ""code"": ""70420""
        },
        {
          ""no"": ""PURCHORD4826"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS943"",
          ""desc"": ""Providing labour, concrete, steel, breaker etc as required t"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1,
          ""rate"": 198850,
          ""amt"": 198850,
          ""bqty"": 1,
          ""bamt"": 198850,
          ""code"": ""70411""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS065"",
          ""desc"": ""Providing & laying in position approved RMC including vibrat"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 216,
          ""rate"": 6880,
          ""amt"": 1486080,
          ""bqty"": 205.26,
          ""bamt"": 1412188.8,
          ""code"": ""70408""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""10% As Advance."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 6367.09,
          ""amt"": 63670.9,
          ""bqty"": 10,
          ""bamt"": 63670.9,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""20% On LOD 200 LOD 300 LOD 400."",
          ""unit"": ""Percentage"",
          ""qty"": 20,
          ""rate"": 6367.09,
          ""amt"": 127341.8,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""15% On Clash Report & BOQ."",
          ""unit"": ""Percentage"",
          ""qty"": 15,
          ""rate"": 6367.09,
          ""amt"": 95506.35,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""15% On Clash Free Report & Final BOQ."",
          ""unit"": ""Percentage"",
          ""qty"": 15,
          ""rate"": 6367.09,
          ""amt"": 95506.35,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""20% On Final GFC."",
          ""unit"": ""Percentage"",
          ""qty"": 20,
          ""rate"": 6367.09,
          ""amt"": 127341.8,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""10% On Final BOQ."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 6367.09,
          ""amt"": 63670.9,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3010"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""item"": ""XCONSER006"",
          ""desc"": ""10% On Built Drawing."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 6367.09,
          ""amt"": 63670.9,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3036"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NAVNIRMITI CONSTRUCTION"",
          ""item"": ""XCONSER060"",
          ""desc"": ""Parking Layout Planning Remarks & Certification."",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 60000,
          ""amt"": 60000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER008"",
          ""desc"": ""on Shorlisting and finalisation of vendor 10%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 16437,
          ""amt"": 16437,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER008"",
          ""desc"": ""pro rata during execution 40%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 65749,
          ""amt"": 65749,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER008"",
          ""desc"": ""on handing over of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 8219,
          ""amt"": 8219,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER008"",
          ""desc"": ""on completion of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 8219,
          ""amt"": 8219,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72012""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFAB003"",
          ""desc"": ""Supply and installation of 2Hrs Fire Rated Doors with 44mm t"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 66,
          ""rate"": 10333,
          ""amt"": 681978,
          ""bqty"": 55.76,
          ""bamt"": 576168.08,
          ""code"": ""70432""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFAB003"",
          ""desc"": ""Supply and installation of 2Hrs Fire Rated Doors with 44mm t"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 51,
          ""rate"": 10333,
          ""amt"": 526983,
          ""bqty"": 50.4,
          ""bamt"": 520783.2,
          ""code"": ""70432""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFAB003"",
          ""desc"": ""Providing & Installing of steel single leaf door shutter of "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 93,
          ""rate"": 10333,
          ""amt"": 960969,
          ""bqty"": 84.89,
          ""bamt"": 877168.37,
          ""code"": ""70432""
        },
        {
          ""no"": ""PURCHORD5485"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""CRESCENDO"",
          ""item"": ""XJOBMIS828"",
          ""desc"": ""Miscellaneous Work for Fire Fighting Sytem & Accessories as "",
          ""unit"": ""Number"",
          ""qty"": 12627285,
          ""rate"": 1,
          ""amt"": 12627285,
          ""bqty"": 10502574.7,
          ""bamt"": 10502574.7,
          ""code"": ""70432""
        },
        {
          ""no"": ""PURCHORD8821"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""OYSTER SAFETY SOLUTIONS"",
          ""item"": ""XJOBMIS243"",
          ""desc"": ""Miscellaneous Work for Fire Fighting(Sq. Ft)"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 390,
          ""rate"": 850,
          ""amt"": 331500,
          ""bqty"": 240.41,
          ""bamt"": 204348.5,
          ""code"": ""70432""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Mango (Girth 1' - Ht 30')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 1,
          ""bamt"": 25000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Ashoka (Girth 1' - Ht 35')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 15000,
          ""amt"": 15000,
          ""bqty"": 1,
          ""bamt"": 15000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Nilgiri (Girth 4' - Ht 30')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 1,
          ""bamt"": 25000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Mango (Girth 1' - Ht 22')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 20000,
          ""amt"": 20000,
          ""bqty"": 1,
          ""bamt"": 20000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Chinch (Girth 9\"" - Ht 22')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 12000,
          ""amt"": 12000,
          ""bqty"": 1,
          ""bamt"": 12000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Gulmohar (Girth 1' - Ht 30')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 22000,
          ""amt"": 22000,
          ""bqty"": 1,
          ""bamt"": 22000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Pipal (Girth 5' - Ht 45')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 45000,
          ""amt"": 45000,
          ""bqty"": 1,
          ""bamt"": 45000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Mango (Girth 1' - Ht 25')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 20000,
          ""amt"": 20000,
          ""bqty"": 1,
          ""bamt"": 20000,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 7500,
          ""amt"": 7500,
          ""bqty"": 1,
          ""bamt"": 7500,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 7500,
          ""amt"": 7500,
          ""bqty"": 1,
          ""bamt"": 7500,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 7500,
          ""amt"": 7500,
          ""bqty"": 1,
          ""bamt"": 7500,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3039"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""item"": ""XCONSER066"",
          ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 7500,
          ""amt"": 7500,
          ""bqty"": 1,
          ""bamt"": 7500,
          ""code"": ""72011""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & Fixing Hilti Catridge RE-500 V3 o"",
          ""unit"": ""Number"",
          ""qty"": 1300,
          ""rate"": 215,
          ""amt"": 279500,
          ""bqty"": 1276,
          ""bamt"": 274340,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 2241,
          ""rate"": 303,
          ""amt"": 679023,
          ""bqty"": 2241,
          ""bamt"": 679023,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 62,
          ""rate"": 512,
          ""amt"": 31744,
          ""bqty"": 62,
          ""bamt"": 31744,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 124,
          ""rate"": 605,
          ""amt"": 75020,
          ""bqty"": 124,
          ""bamt"": 75020,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 53,
          ""rate"": 671,
          ""amt"": 35563,
          ""bqty"": 53,
          ""bamt"": 35563,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 65,
          ""rate"": 902,
          ""amt"": 58630,
          ""bqty"": 65,
          ""bamt"": 58630,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 75,
          ""rate"": 660,
          ""amt"": 49500,
          ""bqty"": 75,
          ""bamt"": 49500,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 60,
          ""rate"": 919,
          ""amt"": 55140,
          ""bqty"": 60,
          ""bamt"": 55140,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 35,
          ""rate"": 915,
          ""amt"": 32025,
          ""bqty"": 35,
          ""bamt"": 32025,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 19,
          ""rate"": 1250,
          ""amt"": 23750,
          ""bqty"": 19,
          ""bamt"": 23750,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 33,
          ""rate"": 875,
          ""amt"": 28875,
          ""bqty"": 33,
          ""bamt"": 28875,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS919"",
          ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 500 V"",
          ""unit"": ""Number"",
          ""qty"": 57,
          ""rate"": 1550,
          ""amt"": 88350,
          ""bqty"": 57,
          ""bamt"": 88350,
          ""code"": ""70440""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""10% On Approval of Concept."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 32968.96,
          ""amt"": 329689.6,
          ""bqty"": 10,
          ""bamt"": 329689.6,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""10% On Schematic Design."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 32968.96,
          ""amt"": 329689.6,
          ""bqty"": 10,
          ""bamt"": 329689.6,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""10% On Drawings Submission to BMC."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 32968.96,
          ""amt"": 329689.6,
          ""bqty"": 10,
          ""bamt"": 329689.6,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""5% On Marketing materials as required by the clients."",
          ""unit"": ""Percentage"",
          ""qty"": 5,
          ""rate"": 32968.96,
          ""amt"": 164844.8,
          ""bqty"": 5,
          ""bamt"": 164844.8,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""10% On Design Development."",
          ""unit"": ""Percentage"",
          ""qty"": 10,
          ""rate"": 32968.96,
          ""amt"": 329689.6,
          ""bqty"": 32.83,
          ""bamt"": 1082479.75,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""15% On GFC including finishing."",
          ""unit"": ""Percentage"",
          ""qty"": 15,
          ""rate"": 32968.96,
          ""amt"": 494534.4,
          ""bqty"": 15,
          ""bamt"": 494534.4,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""35% During stages of Construction at Site."",
          ""unit"": ""Percentage"",
          ""qty"": 35,
          ""rate"": 32968.96,
          ""amt"": 1153913.6,
          ""bqty"": 12.17,
          ""bamt"": 401122.13,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2975"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""XCONSER019"",
          ""desc"": ""5% On Project Completion & Handover."",
          ""unit"": ""Percentage"",
          ""qty"": 5,
          ""rate"": 32968.96,
          ""amt"": 164844.8,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2996"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ESSENCE OF ART"",
          ""item"": ""XCONSER019"",
          ""desc"": ""15% As Advance."",
          ""unit"": ""Percentage"",
          ""qty"": 15,
          ""rate"": 18771.9,
          ""amt"": 281578.5,
          ""bqty"": 15,
          ""bamt"": 281578.5,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2996"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ESSENCE OF ART"",
          ""item"": ""XCONSER019"",
          ""desc"": ""15% On Concept Finalization."",
          ""unit"": ""Percentage"",
          ""qty"": 15,
          ""rate"": 18771.9,
          ""amt"": 281578.5,
          ""bqty"": 15,
          ""bamt"": 281578.5,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2996"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ESSENCE OF ART"",
          ""item"": ""XCONSER019"",
          ""desc"": ""30% On Issue of working drawings (GFC)."",
          ""unit"": ""Percentage"",
          ""qty"": 30,
          ""rate"": 18771.9,
          ""amt"": 563157,
          ""bqty"": 30,
          ""bamt"": 563157,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD2996"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ESSENCE OF ART"",
          ""item"": ""XCONSER019"",
          ""desc"": ""40% As per work progress on Site."",
          ""unit"": ""Percentage"",
          ""qty"": 40,
          ""rate"": 18771.9,
          ""amt"": 750876,
          ""bqty"": 15.74,
          ""bamt"": 295530.71,
          ""code"": ""72001""
        },
        {
          ""no"": ""PURCHORD6337"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""LOTUS AIRCON"",
          ""item"": ""XJOBMIS832"",
          ""desc"": ""Miscellaneous Work for Air  Conditioner work as per annexure"",
          ""unit"": ""Number"",
          ""qty"": 1807180,
          ""rate"": 1,
          ""amt"": 1807180,
          ""bqty"": 1028030,
          ""bamt"": 1028030,
          ""code"": ""70428""
        },
        {
          ""no"": ""PURCHORD6337"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""LOTUS AIRCON"",
          ""item"": ""XJOBMIS832"",
          ""desc"": ""Miscellaneous Work for Air  Conditioner work as per annexure"",
          ""unit"": ""Number"",
          ""qty"": 54400,
          ""rate"": 1,
          ""amt"": 54400,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70428""
        },
        {
          ""no"": ""PURCHORD6338"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""LOTUS AIRCON"",
          ""item"": ""XJOBMIS829"",
          ""desc"": ""Miscellaneous Work for HVAC System for VRV system, ductabale"",
          ""unit"": ""Number"",
          ""qty"": 1368485,
          ""rate"": 1,
          ""amt"": 1368485,
          ""bqty"": 620718,
          ""bamt"": 620718,
          ""code"": ""70428""
        },
        {
          ""no"": ""PURCHORD6338"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""LOTUS AIRCON"",
          ""item"": ""XJOBMIS829"",
          ""desc"": ""Miscellaneous Work for HVAC System for VRV system, ductabale"",
          ""unit"": ""Number"",
          ""qty"": 92500,
          ""rate"": 1,
          ""amt"": 92500,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70428""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBCON019"",
          ""desc"": ""RMC M20/M25/M30 grade"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 19,
          ""rate"": 9000,
          ""amt"": 171000,
          ""bqty"": 18.53,
          ""bamt"": 166770,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS164"",
          ""desc"": ""Miscellaneous items for Reinforcement steel  work"",
          ""unit"": ""Metric Ton"",
          ""qty"": 16,
          ""rate"": 85000,
          ""amt"": 1360000,
          ""bqty"": 15.37,
          ""bamt"": 1306598.75,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS078"",
          ""desc"": ""Plywood formwork Above Plinth level upto 8th flr. Level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 136,
          ""rate"": 706,
          ""amt"": 96016,
          ""bqty"": 135.58,
          ""bamt"": 95719.48,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS076"",
          ""desc"": ""Miscellaneous works"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 630,
          ""rate"": 161,
          ""amt"": 101430,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBCON019"",
          ""desc"": ""RMC M20/M25/M30 grade"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 3600,
          ""rate"": 9187,
          ""amt"": 33073200,
          ""bqty"": 3559.11,
          ""bamt"": 32697531.9,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS164"",
          ""desc"": ""Miscellaneous items for Marble / Granite / Tile work"",
          ""unit"": ""Metric Ton"",
          ""qty"": 690,
          ""rate"": 85505,
          ""amt"": 58998450,
          ""bqty"": 669,
          ""bamt"": 57202836.45,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS078"",
          ""desc"": ""Plywood formwork Above Plinth level upto 8th flr. Level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 26960,
          ""rate"": 748,
          ""amt"": 20166080,
          ""bqty"": 26537.38,
          ""bamt"": 19849960.99,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBCON218"",
          ""desc"": ""Miscellaneous works"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 702,
          ""rate"": 167,
          ""amt"": 117234,
          ""bqty"": 695.11,
          ""bamt"": 116082.54,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS078"",
          ""desc"": ""Providing and Fixing Monofilament Net along the entire exter"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 6150,
          ""rate"": 48,
          ""amt"": 295200,
          ""bqty"": 6140.02,
          ""bamt"": 294720.96,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBSTP020"",
          ""desc"": ""SHUTTERING & FORMWORK_x000D_ Providing, erecting, fixing in "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 227,
          ""rate"": 1320,
          ""amt"": 299640,
          ""bqty"": 226.06,
          ""bamt"": 298397.88,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBSEP019"",
          ""desc"": ""Providing & laying 230 mm thick dry rubble stone soling, voi"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 52,
          ""rate"": 2000,
          ""amt"": 104000,
          ""bqty"": 51.45,
          ""bamt"": 102900,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBSEP014"",
          ""desc"": ""Providing & laying in position plain cement concrete (PCC) u"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 46,
          ""rate"": 7290,
          ""amt"": 335340,
          ""bqty"": 44.29,
          ""bamt"": 322874.1,
          ""code"": ""70414""
        },
        {
          ""no"": ""PURCHORD7427"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK005"",
          ""desc"": ""SECURITY GUARD MALE [Being 1+1=2 Nos. Security Guards Work O"",
          ""unit"": ""Monthly"",
          ""qty"": 22,
          ""rate"": 17000,
          ""amt"": 374000,
          ""bqty"": 21.48,
          ""bamt"": 365226.3,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [APRIL2026 ( 1NOS FOR DAY DUTY & 1 NOS"",
          ""unit"": ""Daily"",
          ""qty"": 60,
          ""rate"": 566.67,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [MAY2026 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 62,
          ""bamt"": 34000,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [JUNE2026 ( 1NOS FOR DAY DUTY & 1 NOS "",
          ""unit"": ""Daily"",
          ""qty"": 60,
          ""rate"": 566.67,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [JULY2026 ( 1NOS FOR DAY DUTY & 1 NOS "",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [AUGUST2026 ( 1NOS FOR DAY DUTY & 1 NO"",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [SEPT2026 ( 1NOS FOR DAY DUTY & 1 NOS "",
          ""unit"": ""Daily"",
          ""qty"": 60,
          ""rate"": 566.67,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [OCT2026 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [NOV2026 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 60,
          ""rate"": 566.67,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [DEC2026 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [JAN2027 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 62,
          ""rate"": 548.39,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD10278"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""SSECWRK002"",
          ""desc"": ""SECURITY WORK (Daily) [FEB2027 ( 1NOS FOR DAY DUTY & 1 NOS F"",
          ""unit"": ""Daily"",
          ""qty"": 56,
          ""rate"": 607.14,
          ""amt"": 34000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""71416""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS966"",
          ""desc"": ""Anti-termite"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 673,
          ""rate"": 77,
          ""amt"": 51821,
          ""bqty"": 672.1,
          ""bamt"": 51751.7,
          ""code"": ""70406""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBDEM021"",
          ""desc"": ""Existing Concrete Breaking"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 37,
          ""rate"": 7150,
          ""amt"": 264550,
          ""bqty"": 35.35,
          ""bamt"": 252752.5,
          ""code"": ""70401""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBDEM025"",
          ""desc"": ""Demolition and Dismantling (Sq. Meter)"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 150,
          ""rate"": 250,
          ""amt"": 37500,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70401""
        },
        {
          ""no"": ""PURCHORD5275"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SANATAN & BROTHERS"",
          ""item"": ""XJOBMIS915"",
          ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
          ""unit"": ""Lumpsum"",
          ""qty"": 12562013.89,
          ""rate"": 1,
          ""amt"": 12562013.89,
          ""bqty"": 9144912,
          ""bamt"": 9144912,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD5546"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""BHAVANI ENTERPRISES (PROP.SANT"",
          ""item"": ""XJOBMIS915"",
          ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
          ""unit"": ""Lumpsum"",
          ""qty"": 182716,
          ""rate"": 1,
          ""amt"": 182716,
          ""bqty"": 162856,
          ""bamt"": 162856,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD5547"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""BHAVANI ENTERPRISES (PROP.SANT"",
          ""item"": ""XJOBMIS915"",
          ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
          ""unit"": ""Lumpsum"",
          ""qty"": 53477,
          ""rate"": 1,
          ""amt"": 53477,
          ""bqty"": 53477,
          ""bamt"": 53477,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD6623"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""XJOBCPF009"",
          ""desc"": ""DIVERTOR CONCEALED PART-HG VERNIS BASIS SET FOR SINGLE LEVER"",
          ""unit"": ""Number"",
          ""qty"": 17,
          ""rate"": 2925,
          ""amt"": 49725,
          ""bqty"": 17,
          ""bamt"": 49725,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD7361"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""XJOBVLV0162"",
          ""desc"": ""Kitchen Sink Mixer [Focus M41 Single lever kitchen mixer 240"",
          ""unit"": ""Number"",
          ""qty"": 40,
          ""rate"": 12946,
          ""amt"": 517840,
          ""bqty"": 40,
          ""bamt"": 517840,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD7662"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""RAIN WATER PROJECT INFRA"",
          ""item"": ""XJOBDNG0262"",
          ""desc"": ""Rain water Harvesting.                    RWRP. Supply & ins"",
          ""unit"": ""Each"",
          ""qty"": 1,
          ""rate"": 375000,
          ""amt"": 375000,
          ""bqty"": 1,
          ""bamt"": 375000,
          ""code"": ""70424""
        },
        {
          ""no"": ""PURCHORD5459"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MCM FLEXI CLADDING INDIA PRIVA"",
          ""item"": ""CBDMFLR002"",
          ""desc"": ""SUNNY BEIGE COLOUR  - SLATE SUNNYE 052056-1200*600 MM"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 26189,
          ""rate"": 111.75,
          ""amt"": 2926620.75,
          ""bqty"": 47817.71,
          ""bamt"": 5343629.65,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5459"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MCM FLEXI CLADDING INDIA PRIVA"",
          ""item"": ""CBDMFLR002"",
          ""desc"": ""WHITE COLOUR  - SLATE H001 -1200*600"",
          ""unit"": ""Sq. Ft"",
          ""qty"": 21630,
          ""rate"": 111.75,
          ""amt"": 2417152.5,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5823"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""HG Vivenis 80 pillar tap w/o_x000D_ waste chrome Better BASI"",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 7875,
          ""amt"": 220500,
          ""bqty"": 28,
          ""bamt"": 220500,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5823"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""HG angle valve E DN15xDN15 - ANGLE VALVE- 13927000 - As per "",
          ""unit"": ""Number"",
          ""qty"": 56,
          ""rate"": 315,
          ""amt"": 17640,
          ""bqty"": 56,
          ""bamt"": 17640,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5823"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""HEALTH FAUCET & ACCESSORIES - Bidette S_x000D_ pre-hose1250 "",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 1730,
          ""amt"": 48440,
          ""bqty"": 23,
          ""bamt"": 39790,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5823"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""KITCHEN MIXERS - HG Logis M31 KM 2601 chr._x000D_ 71835000 -"",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 8530,
          ""amt"": 238840,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5823"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""ANGLE VALVE ( Sink ,WM-2, Aqua-1) - 13927000 - as per approv"",
          ""unit"": ""Number"",
          ""qty"": 112,
          ""rate"": 315,
          ""amt"": 35280,
          ""bqty"": 112,
          ""bamt"": 35280,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5831"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KARRMANYA ENTERPRISE"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""FLUSH PLATE - Geberit Alpha 35 actuator plate"",
          ""unit"": ""Number"",
          ""qty"": 140,
          ""rate"": 920,
          ""amt"": 128800,
          ""bqty"": 140,
          ""bamt"": 128800,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5831"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KARRMANYA ENTERPRISE"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""FLUSH TANK - Geberit Alpha Kambifix Cistern 8.5cm"",
          ""unit"": ""Number"",
          ""qty"": 140,
          ""rate"": 3415,
          ""amt"": 478100,
          ""bqty"": 140,
          ""bamt"": 478100,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5833"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MODERN CERAMICS"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""SEATCOVER - TC394CVK#W/TC281SJ  - As per approved only - TOT"",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 3990,
          ""amt"": 111720,
          ""bqty"": 28,
          ""bamt"": 111720,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5833"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MODERN CERAMICS"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""WC - AP Wall Hung Toilet - CW822M#NW1 - As per approved only"",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 5580,
          ""amt"": 156240,
          ""bqty"": 28,
          ""bamt"": 156240,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD5833"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MODERN CERAMICS"",
          ""item"": ""CBDMFIN004"",
          ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size_x000D_ with 250mm & 190m"",
          ""unit"": ""Number"",
          ""qty"": 28,
          ""rate"": 1203,
          ""amt"": 33684,
          ""bqty"": 28,
          ""bamt"": 33684,
          ""code"": ""12718""
        },
        {
          ""no"": ""PURCHORD3320"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HEAVEN CONSTRUCTION"",
          ""item"": ""XJOBMIS997"",
          ""desc"": ""cement concrete foundation including excavation for the pole"",
          ""unit"": ""Number"",
          ""qty"": 132,
          ""rate"": 1100,
          ""amt"": 145200,
          ""bqty"": 57,
          ""bamt"": 62700,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3320"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HEAVEN CONSTRUCTION"",
          ""item"": ""XJOBSWD017"",
          ""desc"": ""Structural steel works"",
          ""unit"": ""Kilograms"",
          ""qty"": 6680,
          ""rate"": 110,
          ""amt"": 734800,
          ""bqty"": 5523,
          ""bamt"": 607530,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3320"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""HEAVEN CONSTRUCTION"",
          ""item"": ""XJOBSWD018"",
          ""desc"": ""P/F of 28 guage CGI sheet"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 788,
          ""rate"": 610,
          ""amt"": 480680,
          ""bqty"": 674.79,
          ""bamt"": 411620.68,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS997"",
          ""desc"": ""Against DBR 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 8219,
          ""amt"": 8219,
          ""bqty"": 1,
          ""bamt"": 8210.78,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS997"",
          ""desc"": ""on Submission & approval of shematics & preliminary cost est"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 16437,
          ""amt"": 16437,
          ""bqty"": 1,
          ""bamt"": 16437,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS997"",
          ""desc"": ""on Submission & approval of Tender documents BOQ technical s"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 16437,
          ""amt"": 16437,
          ""bqty"": 1,
          ""bamt"": 16437,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS997"",
          ""desc"": ""on GFC drawing submission packages & its approval 15%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 24656,
          ""amt"": 24656,
          ""bqty"": 1.4,
          ""bamt"": 34518.4,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS032"",
          ""desc"": ""Removing and Shifting of Existing GI Barication Near Sales O"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 236,
          ""rate"": 485,
          ""amt"": 114460,
          ""bqty"": 234.68,
          ""bamt"": 113819.8,
          ""code"": ""70438""
        },
        {
          ""no"": ""PURCHORD10024"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""ANISHA MASAND"",
          ""item"": ""MSBUPRM01"",
          ""desc"": ""[Being Work Order booked towards 32 Pcs @ Rs. 3,500/- each ("",
          ""unit"": ""Number"",
          ""qty"": 32,
          ""rate"": 3500,
          ""amt"": 112000,
          ""bqty"": 32,
          ""bamt"": 112000,
          ""code"": ""75114""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [2MP DOME  30 Mtr]"",
          ""unit"": ""Number"",
          ""qty"": 45,
          ""rate"": 3000,
          ""amt"": 135000,
          ""bqty"": 27,
          ""bamt"": 81000,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [2MP Bullet 30 Mtr]"",
          ""unit"": ""Number"",
          ""qty"": 5,
          ""rate"": 3000,
          ""amt"": 15000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [Seagate 6 TB HDD]"",
          ""unit"": ""Number"",
          ""qty"": 2,
          ""rate"": 13000,
          ""amt"": 26000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [32CH NVR]"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 12500,
          ""amt"": 12500,
          ""bqty"": 1,
          ""bamt"": 12500,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV005"",
          ""desc"": ""Network Rack [6U RACK]"",
          ""unit"": ""Number"",
          ""qty"": 2,
          ""rate"": 3750,
          ""amt"": 7500,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV004"",
          ""desc"": ""POE Switch [24PORT POE GIGABIT without Fiber option]"",
          ""unit"": ""Number"",
          ""qty"": 2,
          ""rate"": 16000,
          ""amt"": 32000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9124"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [Installation Labour]"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 60000,
          ""amt"": 60000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9480"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""S4 IT SOLUTION"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [Rj45 Connector with Crimping & testing Both End"",
          ""unit"": ""Number"",
          ""qty"": 27,
          ""rate"": 150,
          ""amt"": 4050,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9480"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""S4 IT SOLUTION"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [Camera Installation & Configration]"",
          ""unit"": ""Number"",
          ""qty"": 27,
          ""rate"": 450,
          ""amt"": 12150,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9480"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""S4 IT SOLUTION"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [One Year Service Support Warranty]"",
          ""unit"": ""Number"",
          ""qty"": 27,
          ""rate"": 650,
          ""amt"": 17550,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9480"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""S4 IT SOLUTION"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [PVC Encluser Box]"",
          ""unit"": ""Number"",
          ""qty"": 2,
          ""rate"": 1350,
          ""amt"": 2700,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD9480"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""S4 IT SOLUTION"",
          ""item"": ""XJOBCCTV001"",
          ""desc"": ""CCTV Camera [6U Wall Mount Rack]"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 3500,
          ""amt"": 3500,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70027""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""Against DBR 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 57530,
          ""amt"": 57530,
          ""bqty"": 1,
          ""bamt"": 57530,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""on Submission & approval of shematics & preliminary cost est"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 115060,
          ""amt"": 115060,
          ""bqty"": 1,
          ""bamt"": 115060,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""on Submission & approval of Tender documents BOQ technical s"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 115060,
          ""amt"": 115060,
          ""bqty"": 1,
          ""bamt"": 115060,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""on Shorlisting and finalisation of vendor 10%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 115060,
          ""amt"": 115060,
          ""bqty"": 1,
          ""bamt"": 115060,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""on GFC drawing submission packages & its approval 15%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 172590,
          ""amt"": 172590,
          ""bqty"": 1,
          ""bamt"": 172590,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER001"",
          ""desc"": ""pro rata during execution 40%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 460240,
          ""amt"": 460240,
          ""bqty"": 1,
          ""bamt"": 460226.19,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER004"",
          ""desc"": ""on handing over of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 57530,
          ""amt"": 57530,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER004"",
          ""desc"": ""on completion of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 57530,
          ""amt"": 57530,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER004"",
          ""desc"": ""For Structural Strengthening Work"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 150000,
          ""amt"": 150000,
          ""bqty"": 1,
          ""bamt"": 150000,
          ""code"": ""72005""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS042"",
          ""desc"": ""Box type waterproofing for horizontal surfaces (Swimming poo"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 762,
          ""rate"": 1377,
          ""amt"": 1049274,
          ""bqty"": 761.08,
          ""bamt"": 1048007.16,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBMIS042"",
          ""desc"": ""Box type waterproofing for vertical surfaces (Swimming pool)"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 736,
          ""rate"": 1432,
          ""amt"": 1053952,
          ""bqty"": 735.85,
          ""bamt"": 1053737.2,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3028"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""item"": ""XJOBBRI203"",
          ""desc"": ""Brick masonry - 230 mm thk. Upto plinth level"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 15,
          ""rate"": 8387,
          ""amt"": 125805,
          ""bqty"": 1.51,
          ""bamt"": 12706.3,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI113"",
          ""desc"": ""Brick masonry - 230 mm thk. Upto plinth level"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 2,
          ""rate"": 9093,
          ""amt"": 18186,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Miscellaneous works"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 300,
          ""rate"": 1840,
          ""amt"": 552000,
          ""bqty"": 167.66,
          ""bamt"": 308494.4,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Lightweight Block masonry - 150 mm thk. Upto 8th flr. level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 5836,
          ""rate"": 1335,
          ""amt"": 7791060,
          ""bqty"": 5279.17,
          ""bamt"": 7047691.95,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Lightweight Block masonry - 100 mm thk. Upto 8th flr. level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 5450,
          ""rate"": 1179,
          ""amt"": 6425550,
          ""bqty"": 5266.57,
          ""bamt"": 6209286.03,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Gypsum plaster  Upto 8th flr. level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 14600,
          ""rate"": 415,
          ""amt"": 6059000,
          ""bqty"": 12596.74,
          ""bamt"": 5227647.1,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""External cement plaster   Upto 8th flr. level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 13750,
          ""rate"": 860,
          ""amt"": 11825000,
          ""bqty"": 13330.71,
          ""bamt"": 11464410.6,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Putty  work"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 7944,
          ""rate"": 142,
          ""amt"": 1128048,
          ""bqty"": 6019.72,
          ""bamt"": 854800.24,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Internal cement plaster    Upto 8th flr. level"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 10760,
          ""rate"": 442,
          ""amt"": 4755920,
          ""bqty"": 9243.07,
          ""bamt"": 4085436.94,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBBRI102"",
          ""desc"": ""Applying waterproofing plaster"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 250,
          ""rate"": 894,
          ""amt"": 223500,
          ""bqty"": 139.7,
          ""bamt"": 124891.8,
          ""code"": ""70417""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMAR107"",
          ""desc"": ""Providing & fixing machine cut polished White Marble stone w"",
          ""unit"": ""R Meter"",
          ""qty"": 250,
          ""rate"": 500,
          ""amt"": 125000,
          ""bqty"": 230.1,
          ""bamt"": 115050,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMAR107"",
          ""desc"": ""Providing & fixing machine cut polished White Marble stone w"",
          ""unit"": ""R Meter"",
          ""qty"": 160,
          ""rate"": 400,
          ""amt"": 64000,
          ""bqty"": 151.8,
          ""bamt"": 60720,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLR101"",
          ""desc"": ""Providing and laying approved Italian Marble Flooring (Colou"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 2900,
          ""rate"": 6790,
          ""amt"": 19691000,
          ""bqty"": 2613.4,
          ""bamt"": 17744986,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBSKR101"",
          ""desc"": ""Providing and fixing of approved Italian Marble Skirting (Co"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 115,
          ""rate"": 8148,
          ""amt"": 937020,
          ""bqty"": 73,
          ""bamt"": 594804,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMAR115"",
          ""desc"": ""Providing and fixing of approved Italian Marble dado of (req"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 80,
          ""rate"": 6500,
          ""amt"": 520000,
          ""bqty"": 22.87,
          ""bamt"": 148655,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLR104"",
          ""desc"": ""Providing and laying approved Vitrified Flooring of size 120"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 500,
          ""rate"": 1610,
          ""amt"": 805000,
          ""bqty"": 354.13,
          ""bamt"": 570149.3,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBSKR103"",
          ""desc"": ""Providing and fixing of approved Vitrified Skirting ( 1200 x"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 225,
          ""rate"": 2107,
          ""amt"": 474075,
          ""bqty"": 132.49,
          ""bamt"": 279156.43,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLR104"",
          ""desc"": ""Providing and laying approved Vitrified Flooring of size 120"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 700,
          ""rate"": 1610,
          ""amt"": 1127000,
          ""bqty"": 379.86,
          ""bamt"": 611574.6,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBSKR103"",
          ""desc"": ""Providing and fixing of approved Vitrified skirting 1200  x "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 107,
          ""rate"": 2107,
          ""amt"": 225449,
          ""bqty"": 33.85,
          ""bamt"": 71321.95,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMAR116"",
          ""desc"": ""Providing & fixing of approved Vitrified tile dado 1200 x 60"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 1000,
          ""rate"": 1580,
          ""amt"": 1580000,
          ""bqty"": 637,
          ""bamt"": 1006460,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLR110"",
          ""desc"": ""Providing and Laying of Restile make Tread of Size 1520mm x "",
          ""unit"": ""Sq. Meter"",
          ""qty"": 285,
          ""rate"": 1780,
          ""amt"": 507300,
          ""bqty"": 285,
          ""bamt"": 507300,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBFLR112"",
          ""desc"": ""Providing & fixing following Restile make Riser of Size 1520"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 160,
          ""rate"": 2046,
          ""amt"": 327360,
          ""bqty"": 138.85,
          ""bamt"": 284087.1,
          ""code"": ""70418""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBLFT014"",
          ""desc"": ""Against DBR 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 32874,
          ""amt"": 32874,
          ""bqty"": 1,
          ""bamt"": 32874,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBLFT014"",
          ""desc"": ""on Submission & approval of shematics & preliminary cost est"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 65749,
          ""amt"": 65749,
          ""bqty"": 1,
          ""bamt"": 65749,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBLFT014"",
          ""desc"": ""on Submission & approval of Tender documents BOQ technical s"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 65749,
          ""amt"": 65749,
          ""bqty"": 1,
          ""bamt"": 65749,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER002"",
          ""desc"": ""on Shorlisting and finalisation of vendor 10%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 65749,
          ""amt"": 65749,
          ""bqty"": 1,
          ""bamt"": 65749,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER002"",
          ""desc"": ""on GFC drawing submission packages & its approval 15%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 98623,
          ""amt"": 98623,
          ""bqty"": 0.73,
          ""bamt"": 72320.25,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER002"",
          ""desc"": ""pro rata during execution 40%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 262994,
          ""amt"": 262994,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER002"",
          ""desc"": ""on handing over of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 32874,
          ""amt"": 32874,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD3545"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XCONSER002"",
          ""desc"": ""on completion of building 5%"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 32874,
          ""amt"": 32874,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72004""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS166"",
          ""desc"": ""Transportation"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1,
          ""rate"": 65000,
          ""amt"": 65000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70012""
        },
        {
          ""no"": ""PURCHORD7113"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VESTERIA ."",
          ""item"": ""XJOBMIS166"",
          ""desc"": ""Transportation"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1,
          ""rate"": 15000,
          ""amt"": 15000,
          ""bqty"": 1,
          ""bamt"": 15000,
          ""code"": ""70012""
        },
        {
          ""no"": ""PURCHORD9875"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""LIDCO BUILDING TECHNOLOGIES LL"",
          ""item"": ""nan"",
          ""desc"": ""nan"",
          ""unit"": ""nan"",
          ""qty"": 0,
          ""rate"": 0,
          ""amt"": 720,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70012""
        },
        {
          ""no"": ""PURCHORD3101"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""EPICONS CONSULTANTS PRIVATE LI"",
          ""item"": ""XCONSER016"",
          ""desc"": ""On review of the Proposed Foundation DBRStructural Scheme/fr"",
          ""unit"": ""Percentage"",
          ""qty"": 40,
          ""rate"": 1630,
          ""amt"": 65200,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72013""
        },
        {
          ""no"": ""PURCHORD3101"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""EPICONS CONSULTANTS PRIVATE LI"",
          ""item"": ""XCONSER016"",
          ""desc"": ""On Review of the Structural analysis &design based on ETABS "",
          ""unit"": ""Percentage"",
          ""qty"": 40,
          ""rate"": 1630,
          ""amt"": 65200,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72013""
        },
        {
          ""no"": ""PURCHORD3101"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""EPICONS CONSULTANTS PRIVATE LI"",
          ""item"": ""XCONSER016"",
          ""desc"": ""On submission of Peer Review Report of Structural Design."",
          ""unit"": ""Percentage"",
          ""qty"": 20,
          ""rate"": 1630,
          ""amt"": 32600,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""72013""
        },
        {
          ""no"": ""PURCHORD5262"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KONE ELEVATOR INDIA PRIVATE LI"",
          ""item"": ""XJOBLFT004"",
          ""desc"": ""Supply, Installation, Testing & Commissioning of Lift with 1"",
          ""unit"": ""Number"",
          ""qty"": 3,
          ""rate"": 2550000,
          ""amt"": 7650000,
          ""bqty"": 3.01,
          ""bamt"": 7683991.5,
          ""code"": ""70430""
        },
        {
          ""no"": ""PURCHORD5262"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""KONE ELEVATOR INDIA PRIVATE LI"",
          ""item"": ""XJOBLFT004"",
          ""desc"": ""Supply, Installation, Testing & Commissioning of Lift with 1"",
          ""unit"": ""Number"",
          ""qty"": 288983,
          ""rate"": 1,
          ""amt"": 288983,
          ""bqty"": 255000,
          ""bamt"": 255000,
          ""code"": ""70430""
        },
        {
          ""no"": ""PURCHORD6252"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""item"": ""XJOBMIS098"",
          ""desc"": ""Providing, fabricating and fixing structural steel for canop"",
          ""unit"": ""Kilograms"",
          ""qty"": 1940.5,
          ""rate"": 138,
          ""amt"": 267789,
          ""bqty"": 1940.5,
          ""bamt"": 267789,
          ""code"": ""70412""
        },
        {
          ""no"": ""PURCHORD4826"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of departmental labour as required by Engineer In-Cha"",
          ""unit"": ""Number"",
          ""qty"": 8,
          ""rate"": 1650,
          ""amt"": 13200,
          ""bqty"": 8,
          ""bamt"": 13200,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD4826"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of departmental labour as required by Engineer In-Cha"",
          ""unit"": ""Number"",
          ""qty"": 59,
          ""rate"": 800,
          ""amt"": 47200,
          ""bqty"": 59,
          ""bamt"": 47200,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of labour for all types of works"",
          ""unit"": ""Number"",
          ""qty"": 300,
          ""rate"": 650,
          ""amt"": 195000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of labour for all types of works"",
          ""unit"": ""Number"",
          ""qty"": 55,
          ""rate"": 1600,
          ""amt"": 88000,
          ""bqty"": 40,
          ""bamt"": 64000,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5510"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SSB BUILDWELL"",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of unskilled Labours  for 6 months_x000D_ (4 Nos x 18"",
          ""unit"": ""Number"",
          ""qty"": 728,
          ""rate"": 600,
          ""amt"": 436800,
          ""bqty"": 373.5,
          ""bamt"": 224100,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5510"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SSB BUILDWELL"",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of Civil Mason for 6 months_x000D_ (1 Nos x 182 days "",
          ""unit"": ""Number"",
          ""qty"": 50,
          ""rate"": 1200,
          ""amt"": 60000,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5510"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SSB BUILDWELL"",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of Breaker Machine Electric for_x000D_ _x000D_ 6 mont"",
          ""unit"": ""Number"",
          ""qty"": 70,
          ""rate"": 1300,
          ""amt"": 91000,
          ""bqty"": 20,
          ""bamt"": 26000,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD8642"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of labour for all types of works [Department Labours "",
          ""unit"": ""Number"",
          ""qty"": 450,
          ""rate"": 650,
          ""amt"": 292500,
          ""bqty"": 418,
          ""bamt"": 271700,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD8642"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of labour for all types of works [Supply of Breaker M"",
          ""unit"": ""Number"",
          ""qty"": 180,
          ""rate"": 1300,
          ""amt"": 234000,
          ""bqty"": 23,
          ""bamt"": 29900,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD8642"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""item"": ""XJOBLBR005"",
          ""desc"": ""Supply of labour for all types of works [Supply of Superviso"",
          ""unit"": ""Number"",
          ""qty"": 90,
          ""rate"": 750,
          ""amt"": 67500,
          ""bqty"": 11.5,
          ""bamt"": 8625,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD9041"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""VARAD FACILITIES MANAGEMENT SE"",
          ""item"": ""XJOBLBR022"",
          ""desc"": ""Lumpsum for Construction Contracts (Sq.Ft.)"",
          ""unit"": ""Daily"",
          ""qty"": 312,
          ""rate"": 653.85,
          ""amt"": 204001.2,
          ""bqty"": 47,
          ""bamt"": 30730.95,
          ""code"": ""70443""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""RUN CONNECT 300 METEOR BLACK - RUN CONNECT 300 METEOR BLACK "",
          ""unit"": ""Number"",
          ""qty"": 4,
          ""rate"": 493104.15,
          ""amt"": 1972416.6,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""SYNCHRO CONNECT P 300 METEOR BLACK - SYNCHRO CONNECT P 300 M"",
          ""unit"": ""Number"",
          ""qty"": 2,
          ""rate"": 427269.15,
          ""amt"": 854538.3,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""RECLINE CONNECT P 300 METEOR BLACK - RECLINE CONNECT P 300 M"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 261200.48,
          ""amt"": 261200.48,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""GROUP CYCLE RIDE - GROUP CYCLE RIDE ** Size: Standard; Frame"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 160801.87,
          ""amt"": 160801.87,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""SKILLROW 7\"" - SKILLROW 7\"" ** Console: 7\""; User Connectivity:"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 261364.95,
          ""amt"": 261364.95,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""CHEST PRESS 700 METEOR BLACK - CHEST PRESS 700 METEOR BLACK "",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 314362.36,
          ""amt"": 314362.36,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""SHOULDER PRESS 700 METEOR BLACK - SHOULDER PRESS 700 METEOR "",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 314362.36,
          ""amt"": 314362.36,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""LAT MACHINE 700 METEOR BLACK - LAT MACHINE 700 METEOR BLACK "",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 210672,
          ""amt"": 210672,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""DUAL LEG CURL/ EXTENSION 700 METEOR BLACK - DUAL LEG CURL/ E"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 388426.5,
          ""amt"": 388426.5,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""LEG PRESS 700 METEOR BLACK - LEG PRESS 700 METEOR BLACK ** W"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 472366.36,
          ""amt"": 472366.36,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""DUAL ADJUSTABLE PULLEY FITNESS ANTH/ BLAC - DUAL ADJUSTABLE "",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 616051.13,
          ""amt"": 616051.13,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD5803"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""XJOBMIS256"",
          ""desc"": ""MULTIPOWER - MULTIPOWER_x000D_ MB82NN0-AN00GG00"",
          ""unit"": ""Number"",
          ""qty"": 1,
          ""rate"": 342342,
          ""amt"": 342342,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70011""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Supply & Fixing jam line 150mm to 200 mm width , Door Side P"",
          ""unit"": ""R Meter"",
          ""qty"": 40,
          ""rate"": 1550,
          ""amt"": 62000,
          ""bqty"": 35.74,
          ""bamt"": 55397,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Carrying out Diamond Polishing treatment on Skirting, Fascia"",
          ""unit"": ""R Meter"",
          ""qty"": 190,
          ""rate"": 192,
          ""amt"": 36480,
          ""bqty"": 122.38,
          ""bamt"": 23496.96,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Staircase Tread    Supply & Fixing of marble Tread with Whit"",
          ""unit"": ""R Meter"",
          ""qty"": 180,
          ""rate"": 2250,
          ""amt"": 405000,
          ""bqty"": 166.1,
          ""bamt"": 373725,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Staircase Riser       Supply & Fixing of marble ,base rate o"",
          ""unit"": ""R Meter"",
          ""qty"": 110,
          ""rate"": 1350,
          ""amt"": 148500,
          ""bqty"": 96,
          ""bamt"": 129600,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Staircase Door -Providing and fixing one hour fire rated 45m"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 3,
          ""rate"": 17000,
          ""amt"": 51000,
          ""bqty"": 3,
          ""bamt"": 51000,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""12mm T Patti -Providing and fixing Metal T Patti SS304 of  o"",
          ""unit"": ""R Meter"",
          ""qty"": 50,
          ""rate"": 921,
          ""amt"": 46050,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS239"",
          ""desc"": ""GYPSUM FALSE CEILING & PAINT WORK_x000D_ Gypsum false ceilin"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 160,
          ""rate"": 1350,
          ""amt"": 216000,
          ""bqty"": 152.91,
          ""bamt"": 206428.5,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Gypsum False Ceiling Running Patta (2\""-6'')"",
          ""unit"": ""R Meter"",
          ""qty"": 50,
          ""rate"": 296,
          ""amt"": 14800,
          ""bqty"": 0,
          ""bamt"": 0,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Grooves at different finish junction"",
          ""unit"": ""R Meter"",
          ""qty"": 98,
          ""rate"": 98,
          ""amt"": 9604,
          ""bqty"": 57.8,
          ""bamt"": 5664.4,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS239"",
          ""desc"": ""Wall & Ceiling Paint -Providing & Applying of two coats of R"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 250,
          ""rate"": 410,
          ""amt"": 102500,
          ""bqty"": 228.86,
          ""bamt"": 93832.6,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Paint Running Patta"",
          ""unit"": ""R Meter"",
          ""qty"": 400,
          ""rate"": 150,
          ""amt"": 60000,
          ""bqty"": 400,
          ""bamt"": 60000,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD6830"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS209"",
          ""desc"": ""Metallic PU on Rafter"",
          ""unit"": ""R Meter"",
          ""qty"": 1800,
          ""rate"": 775,
          ""amt"": 1395000,
          ""bqty"": 1749.74,
          ""bamt"": 1356048.5,
          ""code"": ""70422""
        },
        {
          ""no"": ""PURCHORD3904"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS249"",
          ""desc"": ""Testing Charges QA,QC,SHE,etc"",
          ""unit"": ""Each"",
          ""qty"": 12,
          ""rate"": 3500,
          ""amt"": 42000,
          ""bqty"": 12,
          ""bamt"": 42000,
          ""code"": ""71418""
        },
        {
          ""no"": ""PURCHORD3904"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""item"": ""XJOBMIS249"",
          ""desc"": ""Testing Charges QA,QC,SHE,etc"",
          ""unit"": ""Each"",
          ""qty"": 1,
          ""rate"": 1500,
          ""amt"": 1500,
          ""bqty"": 1,
          ""bamt"": 1500,
          ""code"": ""71418""
        },
        {
          ""no"": ""PURCHORD3074"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Supply of labour"",
          ""unit"": ""Number"",
          ""qty"": 10,
          ""rate"": 1300,
          ""amt"": 13000,
          ""bqty"": 6,
          ""bamt"": 7800,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD3074"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Supply of labour"",
          ""unit"": ""Number"",
          ""qty"": 50,
          ""rate"": 600,
          ""amt"": 30000,
          ""bqty"": 41,
          ""bamt"": 24600,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD3241"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHIRUNNISAH ZABIULLAH CHAUDHA"",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Supply of labour"",
          ""unit"": ""Number"",
          ""qty"": 20,
          ""rate"": 1300,
          ""amt"": 26000,
          ""bqty"": 14,
          ""bamt"": 18200,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD3241"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""MAHIRUNNISAH ZABIULLAH CHAUDHA"",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Supply of labour"",
          ""unit"": ""Number"",
          ""qty"": 70,
          ""rate"": 600,
          ""amt"": 42000,
          ""bqty"": 54.5,
          ""bamt"": 32700,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBLBR007"",
          ""desc"": ""Providing & laying in position plain cement concrete (PCC) u"",
          ""unit"": ""Cu. Meter"",
          ""qty"": 3,
          ""rate"": 6500,
          ""amt"": 19500,
          ""bqty"": 2.79,
          ""bamt"": 18135,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBLBR014"",
          ""desc"": ""Microconcrete M40 for Columns_x000D_ Providing and laying Mi"",
          ""unit"": ""Kilograms"",
          ""qty"": 36188,
          ""rate"": 53,
          ""amt"": 1917964,
          ""bqty"": 36188,
          ""bamt"": 1917964,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBSTP091"",
          ""desc"": ""Carting away excavated material, debris etc."",
          ""unit"": ""Trip"",
          ""qty"": 61,
          ""rate"": 7700,
          ""amt"": 469700,
          ""bqty"": 61,
          ""bamt"": 469700,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD4826"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBLBR019"",
          ""desc"": ""Supply of Reinforcement of required size & quantity as direc"",
          ""unit"": ""Metric Ton"",
          ""qty"": 2,
          ""rate"": 63800,
          ""amt"": 127600,
          ""bqty"": 1.57,
          ""bamt"": 100166,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD4826"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBSTP091"",
          ""desc"": ""Carting away Debris"",
          ""unit"": ""Trip"",
          ""qty"": 15,
          ""rate"": 6050,
          ""amt"": 90750,
          ""bqty"": 15,
          ""bamt"": 90750,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Civil miscellaneous works(Number)"",
          ""unit"": ""Number"",
          ""qty"": 600,
          ""rate"": 95,
          ""amt"": 57000,
          ""bqty"": 600,
          ""bamt"": 57000,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS094"",
          ""desc"": ""Civil miscellaneous works(Bags)"",
          ""unit"": ""Bags"",
          ""qty"": 10,
          ""rate"": 550,
          ""amt"": 5500,
          ""bqty"": 10,
          ""bamt"": 5500,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD5388"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""PAINTTECH INFRA LLP"",
          ""item"": ""XJOBLBR009"",
          ""desc"": ""Civil miscellaneous works(Number) [Grouting work: Miscellane"",
          ""unit"": ""Number"",
          ""qty"": 65,
          ""rate"": 280,
          ""amt"": 18200,
          ""bqty"": 65,
          ""bamt"": 18200,
          ""code"": ""70442""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEXT036"",
          ""desc"": ""Providing & Laying PVC Sleeves in beams as per direction of "",
          ""unit"": ""Number"",
          ""qty"": 175,
          ""rate"": 198,
          ""amt"": 34650,
          ""bqty"": 168,
          ""bamt"": 33264,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEXT036"",
          ""desc"": ""Providing & Laying PVC Sleeves in beams as per direction of "",
          ""unit"": ""Number"",
          ""qty"": 550,
          ""rate"": 203,
          ""amt"": 111650,
          ""bqty"": 548,
          ""bamt"": 111244,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD3344"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBEXT036"",
          ""desc"": ""Providing & Laying PVC Sleeves in beams as per direction of "",
          ""unit"": ""Number"",
          ""qty"": 340,
          ""rate"": 320,
          ""amt"": 108800,
          ""bqty"": 333,
          ""bamt"": 106560,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMIS946"",
          ""desc"": ""Providing, cutting, fabricating, welding,  bending, binding "",
          ""unit"": ""Metric Ton"",
          ""qty"": 9,
          ""rate"": 114400,
          ""amt"": 1029600,
          ""bqty"": 7.63,
          ""bamt"": 872471.6,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD4556"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""item"": ""XJOBMSC001"",
          ""desc"": ""Dismantling and Shifting and Reerecting along with Making an"",
          ""unit"": ""Lumpsum"",
          ""qty"": 1,
          ""rate"": 25000,
          ""amt"": 25000,
          ""bqty"": 1,
          ""bamt"": 25000,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS081"",
          ""desc"": ""\""Bund Wall In Granite _x000D_ Providing and fixing of  Grani"",
          ""unit"": ""R Meter"",
          ""qty"": 1200,
          ""rate"": 810,
          ""amt"": 972000,
          ""bqty"": 792.51,
          ""bamt"": 641933.1,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS081"",
          ""desc"": ""Provide and Fixing of designer SS railing for Staircase with"",
          ""unit"": ""R Meter"",
          ""qty"": 338,
          ""rate"": 3900,
          ""amt"": 1318200,
          ""bqty"": 207.4,
          ""bamt"": 808860,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD5129"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""item"": ""XJOBMIS948"",
          ""desc"": ""Trap Door Providing & fixing Lift type Trap door of size - 4"",
          ""unit"": ""Number"",
          ""qty"": 18,
          ""rate"": 1850,
          ""amt"": 33300,
          ""bqty"": 18,
          ""bamt"": 33300,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD6252"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""item"": ""XJOBMIS947"",
          ""desc"": ""Providing, fabricating & Fixing ISMB 150x75, ISMC 75x40 mm, "",
          ""unit"": ""Kilograms"",
          ""qty"": 1501,
          ""rate"": 110,
          ""amt"": 165110,
          ""bqty"": 1501,
          ""bamt"": 165110,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD6252"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""item"": ""XJOBMIS948"",
          ""desc"": ""Drilling, Providing & Fixing Hilti make safety stud anchor H"",
          ""unit"": ""Number"",
          ""qty"": 104,
          ""rate"": 305,
          ""amt"": 31720,
          ""bqty"": 104,
          ""bamt"": 31720,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD6252"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""item"": ""XJOBMIS948"",
          ""desc"": ""Providing & Fixing of Load hooks in U shape including tools "",
          ""unit"": ""Number"",
          ""qty"": 12,
          ""rate"": 200,
          ""amt"": 2400,
          ""bqty"": 12,
          ""bamt"": 2400,
          ""code"": ""70416""
        },
        {
          ""no"": ""PURCHORD6252"",
          ""date"": """",
          ""status"": ""Approved"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""item"": ""XJOBFEN004"",
          ""desc"": ""Providing & Fixing MS Shade with fixing corrugated sheet wit"",
          ""unit"": ""Sq. Meter"",
          ""qty"": 24.87,
          ""rate"": 610,
          ""amt"": 15170.7,
          ""bqty"": 24.87,
          ""bamt"": 15170.7,
          ""code"": ""70416""
        }
      ],
      ""billedDetails"": {
        ""12718"": [
          {
            ""no"": ""AP/INV/8/002643/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-09-20"",
            ""acct"": ""12718 Current Assets, Loans And Advances"",
            ""amt"": 43875
          }
        ],
        ""70405"": [
          {
            ""no"": ""AP/INV/8/000100/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-03-02"",
            ""acct"": ""70405 Cost of Construction : Civil Const"",
            ""amt"": 25000
          }
        ],
        ""70416"": [
          {
            ""no"": ""AP/INV/8/001397/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-08-05"",
            ""acct"": ""70416 Cost of Construction : Civil Const"",
            ""amt"": 22600
          }
        ],
        ""70427"": [
          {
            ""no"": ""AP/INV/8/003464/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-01"",
            ""acct"": ""70427 Cost of Construction : Civil Const"",
            ""amt"": 76734
          },
          {
            ""no"": ""AP/INV/8/000341/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-27"",
            ""acct"": ""70427 Cost of Construction : Civil Const"",
            ""amt"": 64401
          }
        ],
        ""70430"": [
          {
            ""no"": ""AP/INV/8/001387/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-08-18"",
            ""acct"": ""70430 Cost of Construction : Civil Const"",
            ""amt"": 10265
          },
          {
            ""no"": ""AP/INV/8/002319/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-07-22"",
            ""acct"": ""70430 Cost of Construction : Civil Const"",
            ""amt"": 10265
          },
          {
            ""no"": ""AP/INV/8/002320/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-07-22"",
            ""acct"": ""70430 Cost of Construction : Civil Const"",
            ""amt"": 10265
          }
        ],
        ""70441"": [
          {
            ""no"": ""AP/INV/8/002513/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-11-15"",
            ""acct"": ""70441 Cost of Construction : Civil Const"",
            ""amt"": 1000
          }
        ],
        ""70442"": [
          {
            ""no"": ""AP/INV/8/003680/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-16"",
            ""acct"": ""70442 Cost of Construction : Civil Const"",
            ""amt"": 14097
          }
        ],
        ""71414"": [
          {
            ""no"": ""AP/INV/8/001930/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-10-04"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 118706
          },
          {
            ""no"": ""AP/INV/8/002435/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-12-05"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 284784
          },
          {
            ""no"": ""AP/INV/8/002898/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-01-08"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 165830
          },
          {
            ""no"": ""AP/INV/8/002900/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-12-24"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 15678
          },
          {
            ""no"": ""AP/INV/8/000455/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-20"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 302204
          },
          {
            ""no"": ""AP/INV/8/001054/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-06-17"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 97128
          },
          {
            ""no"": ""AP/INV/8/001389/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-07-04"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 87224
          },
          {
            ""no"": ""AP/INV/8/001407/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-08-13"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 78366
          },
          {
            ""no"": ""AP/INV/8/001644/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-09-05"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 71529
          },
          {
            ""no"": ""AP/INV/8/002136/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-10-04"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 72760
          },
          {
            ""no"": ""AP/INV/8/002397/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-11-03"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 48788
          },
          {
            ""no"": ""AP/INV/8/003093/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-01-03"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 43410
          },
          {
            ""no"": ""AP/INV/8/003678/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-03-04"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 40248
          },
          {
            ""no"": ""AP/INV/8/000053/26-27"",
            ""status"": ""Approved"",
            ""date"": ""2026-04-06"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 32857
          },
          {
            ""no"": ""AP/INV/8/000134/26-27"",
            ""status"": ""Approved"",
            ""date"": ""2026-04-11"",
            ""acct"": ""71414 Cost of Construction : Civil Direc"",
            ""amt"": 11243
          }
        ],
        ""71415"": [
          {
            ""no"": ""AP/INV/8/000753/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-06-13"",
            ""acct"": ""71415 Cost of Construction : Civil Direc"",
            ""amt"": 11000
          }
        ],
        ""71418"": [
          {
            ""no"": ""AP/INV/8/005680/23-24"",
            ""status"": ""Approved"",
            ""date"": ""2024-01-03"",
            ""acct"": ""71418 Cost of Construction : Civil Direc"",
            ""amt"": 17500
          },
          {
            ""no"": ""AP/INV/8/000101/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-04-25"",
            ""acct"": ""71418 Cost of Construction : Civil Direc"",
            ""amt"": 7000
          }
        ],
        ""71422"": [
          {
            ""no"": ""AP/INV/8/001045/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-07-08"",
            ""acct"": ""71422 Cost of Construction : Civil Direc"",
            ""amt"": 391009
          },
          {
            ""no"": ""AP/INV/8/000300/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-06"",
            ""acct"": ""71422 Cost of Construction : Civil Direc"",
            ""amt"": 433281
          }
        ],
        ""71610"": [
          {
            ""no"": ""AP/INV/8/002054/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-05"",
            ""acct"": ""71610 Cost of Construction : Land Cost :"",
            ""amt"": 2549198
          },
          {
            ""no"": ""AP/INV/8/002329/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-11-07"",
            ""acct"": ""71610 Cost of Construction : Land Cost :"",
            ""amt"": 1389380
          }
        ],
        ""71805"": [
          {
            ""no"": ""AP/INV/8/003683/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-03-09"",
            ""acct"": ""71805 Cost of Construction : Approval Co"",
            ""amt"": 7000
          },
          {
            ""no"": ""AP/INV/8/003692/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-16"",
            ""acct"": ""71805 Cost of Construction : Approval Co"",
            ""amt"": 8000
          },
          {
            ""no"": ""AP/INV/8/003696/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-03-16"",
            ""acct"": ""71805 Cost of Construction : Approval Co"",
            ""amt"": 7000
          }
        ],
        ""71808"": [
          {
            ""no"": ""AP/INV/8/002294/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-26"",
            ""acct"": ""71808 Cost of Construction : Approval Co"",
            ""amt"": 1123500
          },
          {
            ""no"": ""AP/INV/8/002296/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-26"",
            ""acct"": ""71808 Cost of Construction : Approval Co"",
            ""amt"": 1123500
          }
        ],
        ""71815"": [
          {
            ""no"": ""AP/INV/8/000016/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-04-03"",
            ""acct"": ""71815 Cost of Construction : Approval Co"",
            ""amt"": 492000
          }
        ],
        ""71825"": [
          {
            ""no"": ""AP/INV/8/006328/23-24"",
            ""status"": ""Approved"",
            ""date"": ""2024-03-26"",
            ""acct"": ""71825 Cost of Construction : Approval Co"",
            ""amt"": 1099832
          }
        ],
        ""71829"": [
          {
            ""no"": ""AP/INV/8/005226/23-24"",
            ""status"": ""Approved"",
            ""date"": ""2023-12-12"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 17700
          },
          {
            ""no"": ""AP/INV/8/002135/25-26"",
            ""status"": ""Rejected"",
            ""date"": ""2025-10-24"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 118000
          },
          {
            ""no"": ""AP/INV/8/003430/25-26"",
            ""status"": ""Rejected"",
            ""date"": ""2026-02-19"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 722676
          },
          {
            ""no"": ""AP/INV/8/003436/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-02-19"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 722676
          },
          {
            ""no"": ""AP/INV/8/000120/26-27"",
            ""status"": ""Approved"",
            ""date"": ""2026-04-13"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 56850
          },
          {
            ""no"": ""AP/INV/8/000576/26-27"",
            ""status"": ""Approved"",
            ""date"": ""2026-06-01"",
            ""acct"": ""71829 Cost of Construction : Approval Co"",
            ""amt"": 15210
          }
        ],
        ""71832"": [
          {
            ""no"": ""AP/INV/8/002299/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-26"",
            ""acct"": ""71832 Cost of Construction : Approval Co"",
            ""amt"": 1182000
          },
          {
            ""no"": ""AP/INV/8/002306/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-26"",
            ""acct"": ""71832 Cost of Construction : Approval Co"",
            ""amt"": 12000
          }
        ],
        ""71833"": [
          {
            ""no"": ""AP/INV/8/003665/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-24"",
            ""acct"": ""71833 Cost of Construction : Approval Co"",
            ""amt"": 43430000
          }
        ],
        ""72012"": [
          {
            ""no"": ""AP/INV/8/002861/24-25"",
            ""status"": ""Rejected"",
            ""date"": ""2025-01-10"",
            ""acct"": ""72012 Cost of Construction : Consultants"",
            ""amt"": 20000
          },
          {
            ""no"": ""AP/INV/8/003049/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-01-10"",
            ""acct"": ""72012 Cost of Construction : Consultants"",
            ""amt"": 20000
          }
        ],
        ""75111"": [
          {
            ""no"": ""AP/INV/8/000845/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-05-31"",
            ""acct"": ""75111 Administrative, Selling And Genera"",
            ""amt"": 36495
          },
          {
            ""no"": ""AP/INV/8/000845/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-05-31"",
            ""acct"": ""75111 Administrative, Selling And Genera"",
            ""amt"": 3577
          },
          {
            ""no"": ""AP/INV/8/003754/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-03-20"",
            ""acct"": ""75111 Administrative, Selling And Genera"",
            ""amt"": 40000
          }
        ],
        ""75113"": [
          {
            ""no"": ""AP/INV/8/003055/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-11-03"",
            ""acct"": ""75113 Administrative, Selling And Genera"",
            ""amt"": 27500
          },
          {
            ""no"": ""AP/INV/8/003056/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2025-01-28"",
            ""acct"": ""75113 Administrative, Selling And Genera"",
            ""amt"": 27500
          }
        ],
        ""75114"": [
          {
            ""no"": ""AP/INV/8/000062/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-04-07"",
            ""acct"": ""75114 Administrative, Selling And Genera"",
            ""amt"": 350000
          },
          {
            ""no"": ""AP/INV/8/000507/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-04-18"",
            ""acct"": ""75114 Administrative, Selling And Genera"",
            ""amt"": 350000
          }
        ],
        ""75116"": [
          {
            ""no"": ""AP/INV/8/000035/24-25"",
            ""status"": ""Approved"",
            ""date"": ""2024-04-05"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 656488
          },
          {
            ""no"": ""AP/INV/8/000235/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-04-15"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 720000
          },
          {
            ""no"": ""AP/INV/8/000386/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-06"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 618821
          },
          {
            ""no"": ""AP/INV/8/000387/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-14"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 591600
          },
          {
            ""no"": ""AP/INV/8/000541/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-16"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 683509
          },
          {
            ""no"": ""AP/INV/8/000543/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-16"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 683509
          },
          {
            ""no"": ""AP/INV/8/000544/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-16"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 683509
          },
          {
            ""no"": ""AP/INV/8/000545/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-05-16"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 846115
          },
          {
            ""no"": ""AP/INV/8/000879/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-06-12"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 618822
          },
          {
            ""no"": ""AP/INV/8/001271/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-08-02"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 1244373
          },
          {
            ""no"": ""AP/INV/8/001916/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-10-06"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 890013
          },
          {
            ""no"": ""AP/INV/8/001947/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-09-16"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 670005
          },
          {
            ""no"": ""AP/INV/8/002202/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-10-14"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 1194196.92
          },
          {
            ""no"": ""AP/INV/8/002336/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2025-11-07"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 2576431
          },
          {
            ""no"": ""AP/INV/8/003187/25-26"",
            ""status"": ""Approved"",
            ""date"": ""2026-01-27"",
            ""acct"": ""75116 Administrative, Selling And Genera"",
            ""amt"": 1251478
          }
        ]
      },
      ""vendors"": [
        {
          ""name"": ""SUNIL CONSTRUCTION CO."",
          ""cat"": ""Contractor"",
          ""orderVal"": 20.82,
          ""woVal"": 20.82,
          ""billed"": 20.14,
          ""outstanding"": 0.68,
          ""retention"": 1.04,
          ""wo"": 3,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 50,
          ""billPct"": 97
        },
        {
          ""name"": ""GREENHEART INFRAPROJECTS PRIVATE L"",
          ""cat"": ""Contractor"",
          ""orderVal"": 17.83,
          ""woVal"": 17.83,
          ""billed"": 14.51,
          ""outstanding"": 3.31,
          ""retention"": 0.89,
          ""wo"": 4,
          ""po"": 0,
          ""asn"": 5,
          ""inv"": 52,
          ""billPct"": 81
        },
        {
          ""name"": ""VINAY WINDOWS SYSTEM PRIVATE LIMIT"",
          ""cat"": ""Contractor"",
          ""orderVal"": 4.33,
          ""woVal"": 4.33,
          ""billed"": 4.11,
          ""outstanding"": 0.22,
          ""retention"": 0.22,
          ""wo"": 2,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 12,
          ""billPct"": 95
        },
        {
          ""name"": ""JRD ENTERPRISES"",
          ""cat"": ""Contractor"",
          ""orderVal"": 3.96,
          ""woVal"": 3.96,
          ""billed"": 2.78,
          ""outstanding"": 1.18,
          ""retention"": 0.2,
          ""wo"": 1,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 18,
          ""billPct"": 70
        },
        {
          ""name"": ""R-LITE ELECTRICALS"",
          ""cat"": ""Service"",
          ""orderVal"": 2.83,
          ""woVal"": 0,
          ""billed"": 1.79,
          ""outstanding"": 1.04,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 2,
          ""asn"": 1,
          ""inv"": 17,
          ""billPct"": 63
        },
        {
          ""name"": ""CRESCENDO"",
          ""cat"": ""Service"",
          ""orderVal"": 1.49,
          ""woVal"": 0,
          ""billed"": 1.25,
          ""outstanding"": 0.24,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 1,
          ""asn"": 0,
          ""inv"": 14,
          ""billPct"": 84
        },
        {
          ""name"": ""SANATAN & BROTHERS"",
          ""cat"": ""Service"",
          ""orderVal"": 1.48,
          ""woVal"": 0,
          ""billed"": 1.16,
          ""outstanding"": 0.32,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 1,
          ""asn"": 1,
          ""inv"": 21,
          ""billPct"": 78
        },
        {
          ""name"": ""KONE ELEVATOR INDIA PRIVATE LIMITE"",
          ""cat"": ""Service"",
          ""orderVal"": 0.94,
          ""woVal"": 0,
          ""billed"": 0.92,
          ""outstanding"": 0.02,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 1,
          ""asn"": 0,
          ""inv"": 16,
          ""billPct"": 98
        },
        {
          ""name"": ""TRINITY HEALTH TECHNOLOGIES PRIVAT"",
          ""cat"": ""Service"",
          ""orderVal"": 0.74,
          ""woVal"": 0,
          ""billed"": 0,
          ""outstanding"": 0.74,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 2,
          ""asn"": 0,
          ""inv"": 0,
          ""billPct"": 0
        },
        {
          ""name"": ""MCM FLEXI CLADDING INDIA PRIVATE L"",
          ""cat"": ""Material"",
          ""orderVal"": 0.63,
          ""woVal"": 0,
          ""billed"": 0.63,
          ""outstanding"": 0,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 1,
          ""asn"": 0,
          ""inv"": 2,
          ""billPct"": 100
        },
        {
          ""name"": ""PAINTTECH INFRA LLP"",
          ""cat"": ""Contractor"",
          ""orderVal"": 0.59,
          ""woVal"": 0.59,
          ""billed"": 0.59,
          ""outstanding"": 0,
          ""retention"": 0.06,
          ""wo"": 2,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 10,
          ""billPct"": 100
        },
        {
          ""name"": ""KUBER ENTERPRIES"",
          ""cat"": ""Contractor"",
          ""orderVal"": 0.54,
          ""woVal"": 0.54,
          ""billed"": 0.49,
          ""outstanding"": 0.05,
          ""retention"": 0.03,
          ""wo"": 1,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 9,
          ""billPct"": 91
        },
        {
          ""name"": ""RUBY STEEL"",
          ""cat"": ""Contractor"",
          ""orderVal"": 0.53,
          ""woVal"": 0.53,
          ""billed"": 0.55,
          ""outstanding"": -0.02,
          ""retention"": 0.03,
          ""wo"": 1,
          ""po"": 0,
          ""asn"": 0,
          ""inv"": 8,
          ""billPct"": 103
        },
        {
          ""name"": ""AAKAR ARCHITECTS & CONSULTANTS"",
          ""cat"": ""Service"",
          ""orderVal"": 0.49,
          ""woVal"": 0,
          ""billed"": 0.41,
          ""outstanding"": 0.09,
          ""retention"": 0,
          ""wo"": 0,
          ""po"": 1,
          ""asn"": 0,
          ""inv"": 1,
          ""billPct"": 82
        },
        {
          ""name"": ""STANDARD WALL SYSTEMS"",
          ""cat"": ""Contractor"",
          ""orderVal"": 0.48,
          ""woVal"": 0.48,
          ""billed"": 0.39,
          ""outstanding"": 0.09,
          ""retention"": 0.02,
          ""wo"": 2,
          ""po"": 0,
          ""asn"": 1,
          ""inv"": 8,
          ""billPct"": 81
        }
      ],
      ""wos"": [
        {
          ""no"": ""PO/8/000085/26-27"",
          ""glDate"": ""15-05-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""amount"": 0.04,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000449/25-26"",
          ""glDate"": ""22-01-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""CQRA PRIVATELIMITED"",
          ""amount"": 0.03,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000450/25-26"",
          ""glDate"": ""22-01-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""POWERSOFT IT PRIVATE LIMITED"",
          ""amount"": 0.03,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Sandeep Vadalia"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000439/25-26"",
          ""glDate"": ""07-01-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""VARAD FACILITIES MANAGEMENT SE"",
          ""amount"": 0.02,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Meenakshi J Sawant"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN2588"",
          ""asnDate"": ""17-03-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000429/25-26"",
          ""glDate"": ""02-01-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""amount"": 0.77,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""02-01-27"",
          ""createdBy"": ""Meenakshi J Sawant"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3534"",
          ""asnDate"": ""07-06-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000419/25-26"",
          ""glDate"": ""30-12-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""SILA SOLUTIONS PRIVATE LIMITED"",
          ""amount"": 0.01,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Vasant Tarkar"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000363/25-26"",
          ""glDate"": ""06-12-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""amount"": 0.07,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""03-12-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3686"",
          ""asnDate"": ""22-06-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000340/25-26"",
          ""glDate"": ""24-11-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""STANDARD WALL SYSTEMS"",
          ""amount"": 0.23,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""19-11-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3156"",
          ""asnDate"": ""12-05-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000333/25-26"",
          ""glDate"": ""15-11-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""BRICKLANE"",
          ""amount"": 0.02,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000203/25-26"",
          ""glDate"": ""20-08-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""CQRA PRIVATELIMITED"",
          ""amount"": 0.13,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""18-07-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000182/25-26"",
          ""glDate"": ""06-08-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""amount"": 1.56,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""06-08-30"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000113/25-26"",
          ""glDate"": ""18-06-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""amount"": 2.19,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""02-06-30"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3641"",
          ""asnDate"": ""16-06-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000080/25-26"",
          ""glDate"": ""28-05-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""RAJENDRA SAROJ ENTERPRISES"",
          ""amount"": 0.08,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3512"",
          ""asnDate"": ""05-06-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000070/25-26"",
          ""glDate"": ""19-05-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""RUBY STEEL"",
          ""amount"": 0.53,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""12-05-30"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Fully Billed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000069/25-26"",
          ""glDate"": ""17-05-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""KUBER ENTERPRIES"",
          ""amount"": 0.54,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""13-05-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000026/25-26"",
          ""glDate"": ""24-04-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""KREEM CREATIONS"",
          ""amount"": 0.38,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""25-03-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000006/25-26"",
          ""glDate"": ""05-04-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""NATIONAL ENTERPRISES"",
          ""amount"": 0.07,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""21-03-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000007/25-26"",
          ""glDate"": ""05-04-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""STANDARD WALL SYSTEMS"",
          ""amount"": 0.25,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""31-03-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000438/24-25"",
          ""glDate"": ""31-03-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""amount"": 0.64,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""04-03-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000430/24-25"",
          ""glDate"": ""20-03-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""MARLINGA SHANKARPPA PUJARI"",
          ""amount"": 0.16,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""04-02-26"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Rejected"",
          ""billStatus"": ""Rejected by Supervisor"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000395/24-25"",
          ""glDate"": ""24-02-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""COOLDECK INDUSTRIES PRIVATE LI"",
          ""amount"": 0.26,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""10-02-24"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000335/24-25"",
          ""glDate"": ""09-01-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""amount"": 3.69,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""01-01-30"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000330/24-25"",
          ""glDate"": ""07-01-25"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""CQRA PRIVATELIMITED"",
          ""amount"": 0.18,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000308/24-25"",
          ""glDate"": ""24-12-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""HRUTURAJ GREEN"",
          ""amount"": 0.02,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Fully Billed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000299/24-25"",
          ""glDate"": ""18-12-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""SSB BUILDWELL"",
          ""amount"": 0.07,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""30-11-25"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000269/24-25"",
          ""glDate"": ""28-11-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""PAINTTECH INFRA LLP"",
          ""amount"": 0.34,
          ""ret"": 10,
          ""wctRet"": null,
          ""retDate"": ""09-10-34"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000238/24-25"",
          ""glDate"": ""30-10-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""PAINTTECH INFRA LLP"",
          ""amount"": 0.25,
          ""ret"": 10,
          ""wctRet"": null,
          ""retDate"": ""10-07-34"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000215/24-25"",
          ""glDate"": ""09-10-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""amount"": 13.31,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""03-10-34"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3643"",
          ""asnDate"": ""16-06-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000144/24-25"",
          ""glDate"": ""19-08-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""amount"": 0.06,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""01-08-25"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000124/24-25"",
          ""glDate"": ""31-07-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""amount"": 1.4,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""24-04-25"",
          ""createdBy"": ""Shravani Gorivale"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000035/24-25"",
          ""glDate"": ""27-05-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""amount"": 0.01,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Minal Jadhav"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Fully Billed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000528/23-24"",
          ""glDate"": ""12-03-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""STRUCTWEL DESIGNERS AND CONSUL"",
          ""amount"": 0.25,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000468/23-24"",
          ""glDate"": ""24-01-24"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""amount"": 19.36,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""26-12-32"",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000444/23-24"",
          ""glDate"": ""11-12-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""HEAVEN CONSTRUCTION"",
          ""amount"": 0.16,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Closed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000176/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""NEOINFINIT MULTICONS PRIVATE L"",
          ""amount"": 0.08,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Closed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000194/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""JRD ENTERPRISES"",
          ""amount"": 3.96,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": ""08-03-33"",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000205/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""GREENSCAPE CREATION"",
          ""amount"": 0.07,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000240/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""amount"": 0.01,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000267/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""EPICONS CONSULTANTS PRIVATE LI"",
          ""amount"": 0.02,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Closed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/8/000405/23-24"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""vendor"": ""MAHIRUNNISAH ZABIULLAH CHAUDHA"",
          ""amount"": 0.01,
          ""ret"": 0,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        }
      ],
      ""pos"": [
        {
          ""no"": ""PO/8/000134/26-27"",
          ""cat"": ""Department"",
          ""glDate"": ""17-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""RAIN WATER PROJECT INFRA"",
          ""item"": ""—"",
          ""val"": 1.71,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000049/26-27"",
          ""cat"": ""Department"",
          ""glDate"": ""02-05-26"",
          ""dept"": ""INFORMATION TECHNOLOGY"",
          ""supplier"": ""S4 IT SOLUTION"",
          ""item"": ""—"",
          ""val"": 0.98,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Shravani Gorivale"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000031/26-27"",
          ""cat"": ""Department"",
          ""glDate"": ""20-04-26"",
          ""dept"": ""SPECIALITY SUPPORT - M"",
          ""supplier"": ""ANISHA MASAND"",
          ""item"": ""—"",
          ""val"": 1.12,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Sachin Palkar"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000013/26-27"",
          ""cat"": ""Material"",
          ""glDate"": ""07-04-26"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 6.1,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Pratiksha Rambade"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3473"",
          ""grnDate"": ""29-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000007/26-27"",
          ""cat"": ""Material"",
          ""glDate"": ""03-04-26"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""LIDCO BUILDING TECHNOLOGIES LL"",
          ""item"": ""—"",
          ""val"": 4.11,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Pratiksha Rambade"",
          ""status"": ""Pending Billing/Partiall"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3256"",
          ""grnDate"": ""22-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000530/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""12-03-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""AZONIC INTERNATIONAL"",
          ""item"": ""—"",
          ""val"": 4.46,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000508/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""27-02-26"",
          ""dept"": ""INFORMATION TECHNOLOGY"",
          ""supplier"": ""S4 IT SOLUTION"",
          ""item"": ""—"",
          ""val"": 0.59,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Sandeep Vadalia"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000510/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""23-02-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""AADISHAKTI WATER SUPPLIERS"",
          ""item"": ""—"",
          ""val"": 0.3,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Meenakshi J Sawant"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000467/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""30-01-26"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""LIDCO BUILDING TECHNOLOGIES LL"",
          ""item"": ""—"",
          ""val"": 3.93,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3254"",
          ""grnDate"": ""17-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000454/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""23-01-26"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""—"",
          ""val"": 7.84,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000402/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""22-12-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""OYSTER SAFETY SOLUTIONS"",
          ""item"": ""—"",
          ""val"": 3.91,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000377/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""11-12-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 3.85,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN2801"",
          ""grnDate"": ""18-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000352/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""01-12-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""—"",
          ""val"": 0.89,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000323/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""04-11-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""ESFRO SOLUTIONS PRIVATE LIMITE"",
          ""item"": ""—"",
          ""val"": 28.89,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000314/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""29-10-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""OUTDOORFURNITURES.IN"",
          ""item"": ""—"",
          ""val"": 1.78,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000285/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""13-10-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 4.51,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000299/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""13-10-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""TREEN TECHNOLOGIES"",
          ""item"": ""—"",
          ""val"": 1.5,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000262/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""03-10-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 7.5,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000261/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""01-10-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 4.89,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Pending Receipt"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000272/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""23-09-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""BRIDGEWAY ELECTRIK SOLUTIONS P"",
          ""item"": ""—"",
          ""val"": 4.15,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000232/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""09-09-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""RAIN WATER PROJECT INFRA"",
          ""item"": ""—"",
          ""val"": 4.42,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000217/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""04-09-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""—"",
          ""val"": 0.52,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Pending Supervisor Appro"",
          ""appr"": ""Pending Approv"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000209/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""26-08-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""KARRMANYA ENTERPRISE"",
          ""item"": ""—"",
          ""val"": 2.42,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000218/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""25-08-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""SUNGRID ENERGY SYSTEMS PRIVATE"",
          ""item"": ""—"",
          ""val"": 11.74,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000243/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""25-08-25"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""PARBAT PATEL & ASSOCIATES"",
          ""item"": ""—"",
          ""val"": 7.3,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Feny Dedhia"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000196/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""13-08-25"",
          ""dept"": ""SECURITY"",
          ""supplier"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""item"": ""—"",
          ""val"": 3.74,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Tarkar"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000183/25-26"",
          ""cat"": ""Material"",
          ""glDate"": ""06-08-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""—"",
          ""val"": 6.11,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000154/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""14-07-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 15.84,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000155/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""14-07-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""VESTERIA ."",
          ""item"": ""—"",
          ""val"": 0.67,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Vasant Marathe"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000077/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""27-05-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""—"",
          ""val"": 0.59,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000062/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""14-05-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""R-LITE ELECTRICALS"",
          ""item"": ""—"",
          ""val"": 0.73,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000059/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""12-05-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""TREEN TECHNOLOGIES"",
          ""item"": ""—"",
          ""val"": 0.99,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000037/25-26"",
          ""cat"": ""Department"",
          ""glDate"": ""30-04-25"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""AAKAR ARCHITECTS & CONSULTANTS"",
          ""item"": ""—"",
          ""val"": 49.45,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Feny Dedhia"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000399/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""27-02-25"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""—"",
          ""val"": 2.95,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Feny Dedhia"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000384/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""20-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 4.36,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3331"",
          ""grnDate"": ""29-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000385/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""20-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 4.36,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN2648"",
          ""grnDate"": ""10-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000386/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""20-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 7.05,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN2650"",
          ""grnDate"": ""10-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000372/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""13-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 5.58,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3481"",
          ""grnDate"": ""05-05-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000363/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""07-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""KARRMANYA ENTERPRISE"",
          ""item"": ""—"",
          ""val"": 7.16,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000365/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""07-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MODERN CERAMICS"",
          ""item"": ""—"",
          ""val"": 6.23,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN2586"",
          ""grnDate"": ""03-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000360/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""06-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""C. BHOGILAL WEST END PRIVATE L"",
          ""item"": ""—"",
          ""val"": 6.62,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000353/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""05-02-25"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""TRINITY HEALTH TECHNOLOGIES PR"",
          ""item"": ""—"",
          ""val"": 73.56,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Closed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000347/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""24-01-25"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""ESSENCE OF ART"",
          ""item"": ""—"",
          ""val"": 7.79,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Feny Dedhia"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000442/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""03-01-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""LOTUS AIRCON"",
          ""item"": ""—"",
          ""val"": 21.97,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000443/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""03-01-25"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""LOTUS AIRCON"",
          ""item"": ""—"",
          ""val"": 17.24,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3068"",
          ""grnDate"": ""17-03-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000309/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""18-12-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""BHAVANI ENTERPRISES (PROP.SANT"",
          ""item"": ""—"",
          ""val"": 2.16,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000310/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""18-12-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""BHAVANI ENTERPRISES (PROP.SANT"",
          ""item"": ""—"",
          ""val"": 0.63,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000290/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""14-12-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""CRESCENDO"",
          ""item"": ""—"",
          ""val"": 149,
          ""ret"": 5,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000288/24-25"",
          ""cat"": ""Material"",
          ""glDate"": ""10-12-24"",
          ""dept"": ""PURCHASE"",
          ""supplier"": ""MCM FLEXI CLADDING INDIA PRIVA"",
          ""item"": ""—"",
          ""val"": 63.06,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Mahesh Aithal"",
          ""status"": ""Pending Billing/Partiall"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000259/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""08-11-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""KREATIVE ENGINEERING AND ELECT"",
          ""item"": ""—"",
          ""val"": 32.11,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000266/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""07-11-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""R-LITE ELECTRICALS"",
          ""item"": ""—"",
          ""val"": 282.09,
          ""ret"": 5,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN2703"",
          ""grnDate"": ""01-04-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000249/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""23-10-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""SANATAN & BROTHERS"",
          ""item"": ""—"",
          ""val"": 148.23,
          ""ret"": 5,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": ""ASN3567"",
          ""grnDate"": ""10-06-2026"",
          ""grnStatus"": ""Approved""
        },
        {
          ""no"": ""PO/8/000247/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""22-10-24"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""supplier"": ""KONE ELEVATOR INDIA PRIVATE LI"",
          ""item"": ""—"",
          ""val"": 93.68,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Manoj Gosavi"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000534/23-24"",
          ""cat"": ""Department"",
          ""glDate"": ""31-03-24"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""THE DESIGN CORE"",
          ""item"": ""—"",
          ""val"": 24.78,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Dattatray V Kondhalk"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000323/23-24"",
          ""cat"": ""Department"",
          ""glDate"": ""18-11-23"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""DAWN DIGITAL PRIVATE LIMITED"",
          ""item"": ""—"",
          ""val"": 20.65,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Yogesh Patel"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000141/23-24"",
          ""cat"": ""Department"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""ARCHITECT HAFEEZ CONTRACTOR"",
          ""item"": ""—"",
          ""val"": 38.9,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Yogesh Patel"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000162/23-24"",
          ""cat"": ""Department"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""ESSENCE OF ART"",
          ""item"": ""—"",
          ""val"": 22.15,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Yogesh Patel"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        },
        {
          ""no"": ""PO/8/000202/23-24"",
          ""cat"": ""Department"",
          ""glDate"": ""30-09-23"",
          ""dept"": ""APPROVAL/LIASON"",
          ""supplier"": ""NAVNIRMITI CONSTRUCTION"",
          ""item"": ""—"",
          ""val"": 0.71,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Yogesh Patel"",
          ""status"": ""Closed"",
          ""appr"": ""Approved"",
          ""memo"": """",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null
        }
      ],
      ""poUnit"": ""₹ Lakh"",
      ""grn"": [
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        },
        {
          ""grn"": ""ASN2597"",
          ""po"": ""Purchase Order #PO/8/000215/24-25"",
          ""vendor"": ""GREENHEART INFRAPROJECTS P"",
          ""item"": ""Material / progress"",
          ""qty"": ""—"",
          ""val"": 154.91,
          ""date"": ""2026-03-31"",
          ""by"": ""CONSTRUCTION/EXE""
        }
      ],
      ""grnUnit"": ""₹ Lakh"",
      ""invoices"": [
        {
          ""no"": ""AP/INV/8/000862/26-27"",
          ""vendor"": ""VARAD FACILITIES MANAGEMENT SE"",
          ""glDate"": ""29-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 19890,
          ""paid"": 0,
          ""balance"": 19890,
          ""ret"": 0,
          ""wopo"": ""PO/8/000439/25-26"",
          ""memo"": ""RA - 1 Booked Supply of manpower for the operating  of the l""
        },
        {
          ""no"": ""AP/INV/8/000825/26-27"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""glDate"": ""25-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 66251.75,
          ""paid"": 0,
          ""balance"": 66251.75,
          ""ret"": 0,
          ""wopo"": ""PO/8/000363/25-26"",
          ""memo"": ""RA - 4 Booked  Departmental labour supply in wing  F for Hou""
        },
        {
          ""no"": ""AP/INV/8/000830/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 131830,
          ""paid"": 0,
          ""balance"": 131830,
          ""ret"": 0,
          ""wopo"": ""PO/8/000365/24-25"",
          ""memo"": ""Quantity Validation""
        },
        {
          ""no"": ""AP/INV/8/000831/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 9534.4,
          ""paid"": 0,
          ""balance"": 9534.4,
          ""ret"": 0,
          ""wopo"": ""PO/8/000013/26-27"",
          ""memo"": ""invoice dated - 28-Mar-26""
        },
        {
          ""no"": ""AP/INV/8/000834/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 6053.4,
          ""paid"": 0,
          ""balance"": 6053.4,
          ""ret"": 0,
          ""wopo"": ""PO/8/000384/24-25"",
          ""memo"": ""invoice dated - 29-Apr-26""
        },
        {
          ""no"": ""AP/INV/8/000837/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 47011.2,
          ""paid"": 0,
          ""balance"": 47011.2,
          ""ret"": 0,
          ""wopo"": ""PO/8/000384/24-25"",
          ""memo"": ""invoice dated - 30-May-26""
        },
        {
          ""no"": ""AP/INV/8/000838/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 82269.6,
          ""paid"": 0,
          ""balance"": 82269.6,
          ""ret"": 0,
          ""wopo"": ""PO/8/000372/24-25"",
          ""memo"": ""invoice dated - 30-May-26""
        },
        {
          ""no"": ""AP/INV/8/000839/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 2938.2,
          ""paid"": 0,
          ""balance"": 2938.2,
          ""ret"": 0,
          ""wopo"": ""PO/8/000386/24-25"",
          ""memo"": ""invoice dated - 30-May-26""
        },
        {
          ""no"": ""AP/INV/8/000842/26-27"",
          ""vendor"": ""MODERN CERAMICS"",
          ""glDate"": ""25-06-26"",
          ""dept"": ""PURCHASE"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 88358.4,
          ""paid"": 0,
          ""balance"": 88358.4,
          ""ret"": 0,
          ""wopo"": ""PO/8/000384/24-25"",
          ""memo"": ""invoice dated - 4-Jun-26""
        },
        {
          ""no"": ""AP/INV/8/000869/26-27"",
          ""vendor"": ""R R ENTERPRISES (PROP.KANCHAN "",
          ""glDate"": ""25-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Retention Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 2831,
          ""paid"": 0,
          ""balance"": 2831,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""FWNG/RRE/26-27/04_RETENTION1719907""
        },
        {
          ""no"": ""AP/INV/8/000807/26-27"",
          ""vendor"": ""ESSENCE OF ART"",
          ""glDate"": ""23-06-26"",
          ""dept"": ""APPROVAL/LIASON"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 71252.95,
          ""paid"": 71252.95,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": ""PO/8/000347/24-25"",
          ""memo"": ""Being the amount payable towards the as per work progress on""
        },
        {
          ""no"": ""AP/INV/8/000808/26-27"",
          ""vendor"": ""ESSENCE OF ART"",
          ""glDate"": ""23-06-26"",
          ""dept"": ""APPROVAL/LIASON"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 299841.97,
          ""paid"": 0,
          ""balance"": 299841.97,
          ""ret"": 0,
          ""wopo"": ""PO/8/000162/23-24"",
          ""memo"": ""Being the amount payable towards the Interior Design service""
        },
        {
          ""no"": ""AP/INV/8/000801/26-27"",
          ""vendor"": ""PARBAT PATEL & ASSOCIATES"",
          ""glDate"": ""22-06-26"",
          ""dept"": ""APPROVAL/LIASON"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 290520,
          ""paid"": 0,
          ""balance"": 290520,
          ""ret"": 0,
          ""wopo"": ""PO/8/000243/25-26"",
          ""memo"": ""Being the amount payable towards Remarks from M.S. for exist""
        },
        {
          ""no"": ""AP/INV/8/000784/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""20-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 2886466.34,
          ""paid"": 0,
          ""balance"": 2886466.34,
          ""ret"": 0,
          ""wopo"": ""PO/8/000113/25-26"",
          ""memo"": ""BEING RA 04 BOOKED GREENHEART INFRA PROJECTS PVT LTD HUBTOWN""
        },
        {
          ""no"": ""AP/INV/8/000786/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""20-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 3527792.98,
          ""paid"": 3527792.98,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": ""PO/8/000215/24-25"",
          ""memo"": ""BEING RA 15 BOOKED GREENHEART INFR PROJECTS PVT LTD HUBTOWN ""
        },
        {
          ""no"": ""AP/INV/8/000817/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""20-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Retention Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 152060,
          ""paid"": 0,
          ""balance"": 152060,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""12/2026-27_RETENTION1714638""
        },
        {
          ""no"": ""AP/INV/8/000819/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""20-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Retention Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 124417,
          ""paid"": 0,
          ""balance"": 124417,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""11/2026-27_RETENTION1714642""
        },
        {
          ""no"": ""AP/INV/8/000751/26-27"",
          ""vendor"": ""SANATAN & BROTHERS"",
          ""glDate"": ""17-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 398655.62,
          ""paid"": 0,
          ""balance"": 398655.62,
          ""ret"": 0,
          ""wopo"": ""PO/8/000249/24-25"",
          ""memo"": ""Ra bill 10 SITC of Plumbing works at Seasons F wing.""
        },
        {
          ""no"": ""AP/INV/8/000766/26-27"",
          ""vendor"": ""SANATAN & BROTHERS"",
          ""glDate"": ""17-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""type"": ""Retention Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 17183,
          ""paid"": 0,
          ""balance"": 17183,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""SB/2026-2027/16_RETENTION1707677""
        },
        {
          ""no"": ""AP/INV/8/000722/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""12-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 2045313.01,
          ""paid"": 0,
          ""balance"": 2045313.01,
          ""ret"": 0,
          ""wopo"": ""PO/8/000429/25-26"",
          ""memo"": ""Being RA 01 Booked Greenheart infra projects Pvt Ltd Hubtown""
        },
        {
          ""no"": ""AP/INV/8/000738/26-27"",
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""glDate"": ""12-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Retention Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 88160,
          ""paid"": 0,
          ""balance"": 88160,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""09/2026-27_RETENTION1704643""
        },
        {
          ""no"": ""AP/INV/8/000694/26-27"",
          ""vendor"": ""MAHARASHTRA INTELLIGENCE SECUR"",
          ""glDate"": ""11-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 33660,
          ""paid"": 0,
          ""balance"": 33660,
          ""ret"": 0,
          ""wopo"": ""PO/8/000085/26-27"",
          ""memo"": ""Being the amount payable towards providing Security Services""
        },
        {
          ""no"": ""AP/INV/8/000699/26-27"",
          ""vendor"": ""TATA AIG GENERAL INSURANCE COM"",
          ""glDate"": ""11-06-26"",
          ""dept"": ""ADMINISTRATION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 55628,
          ""paid"": 0,
          ""balance"": 55628,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the Amount Payable towards Extension of Car Policy of ""
        },
        {
          ""no"": ""AP/INV/8/000671/26-27"",
          ""vendor"": ""ADANI ELECTRICITY MUMBAI LIMIT"",
          ""glDate"": ""10-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 133641,
          ""paid"": 133641,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the amount payable towards electricity charges for the""
        },
        {
          ""no"": ""AP/INV/8/000673/26-27"",
          ""vendor"": ""RAJENDRA SAROJ ENTERPRISES"",
          ""glDate"": ""10-06-26"",
          ""dept"": ""CONSTRUCTION/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 217800,
          ""paid"": 0,
          ""balance"": 217800,
          ""ret"": 0,
          ""wopo"": ""PO/8/000080/25-26"",
          ""memo"": ""RA -5 Booked removing of construction debris from seasons wi""
        },
        {
          ""no"": ""AP/INV/8/000646/26-27"",
          ""vendor"": ""ADANI ELECTRICITY MUMBAI LIMIT"",
          ""glDate"": ""08-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 17240,
          ""paid"": 17240,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the amount payable towards Electricity Bill for Month ""
        },
        {
          ""no"": ""AP/INV/8/000647/26-27"",
          ""vendor"": ""NIVASA DEVELOPERS"",
          ""glDate"": ""08-06-26"",
          ""dept"": ""RESOURCE MOBILISATION "",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 15120000,
          ""paid"": 15120000,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being amount payable towards Development management fees""
        },
        {
          ""no"": ""AP/INV/8/000630/26-27"",
          ""vendor"": ""ADANI ELECTRICITY MUMBAI LIMIT"",
          ""glDate"": ""05-06-26"",
          ""dept"": ""PLANNING  AND ESTIMATI"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 5247,
          ""paid"": 5247,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the amount payable towards Electricity Bill for Month ""
        },
        {
          ""no"": ""AP/INV/8/000615/26-27"",
          ""vendor"": ""NEHA ARYA"",
          ""glDate"": ""04-06-26"",
          ""dept"": ""RESIDENTIAL SALES"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 2100000,
          ""paid"": 2100000,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the amount payable as refund towards Cancellation of b""
        },
        {
          ""no"": ""AP/INV/8/000645/26-27"",
          ""vendor"": ""SHRIVYA REALTY LLP"",
          ""glDate"": ""04-06-26"",
          ""dept"": ""RESOURCE MOBILISATION "",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 23400000,
          ""paid"": 23400000,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Being the amount payable towards @7.5%, Revenue Share from H""
        }
      ],
      ""invUnit"": ""₹"",
      ""retention"": [
        {
          ""vendor"": ""SUNIL CONSTRUCTION CO."",
          ""bills"": 13,
          ""held"": 0.76,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""GREENHEART INFRAPROJECTS PRIVA"",
          ""bills"": 5,
          ""held"": 0.2,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""VINAY WINDOWS SYSTEM PRIVATE L"",
          ""bills"": 5,
          ""held"": 0.19,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""PAINTTECH INFRA LLP"",
          ""bills"": 5,
          ""held"": 0.05,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""RUBY STEEL"",
          ""bills"": 3,
          ""held"": 0.02,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""R-LITE ELECTRICALS"",
          ""bills"": 2,
          ""held"": 0.02,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""SANATAN & BROTHERS"",
          ""bills"": 3,
          ""held"": 0.02,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""KUBER ENTERPRIES"",
          ""bills"": 3,
          ""held"": 0.02,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        }
      ],
      ""tds"": [
        {
          ""section"": ""194C"",
          ""desc"": ""Payment to contractors"",
          ""base"": 45.93,
          ""rate"": ""1% / 2%"",
          ""tds"": 0.34,
          ""deductees"": 8
        },
        {
          ""section"": ""194Q"",
          ""desc"": ""Purchase of goods > ₹50L"",
          ""base"": 1.17,
          ""rate"": ""0.10%"",
          ""tds"": 0.06,
          ""deductees"": 3
        },
        {
          ""section"": ""194J"",
          ""desc"": ""Professional / consultancy"",
          ""base"": 2.1,
          ""rate"": ""10%"",
          ""tds"": 0.02,
          ""deductees"": 2
        }
      ],
      ""boq"": [
        {
          ""code"": ""XJOBMIS164"",
          ""desc"": ""70414 RCC Superstructure"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 706,
          ""dr"": 0,
          ""or_"": 85494,
          ""bv"": 0,
          ""wv"": 6.04
        },
        {
          ""code"": ""XJOBBRI102"",
          ""desc"": ""70417 Finishing Masonary Work"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 59748,
          ""dr"": 0,
          ""or_"": 681,
          ""bv"": 0,
          ""wv"": 4.07
        },
        {
          ""code"": ""XJOBCON019"",
          ""desc"": ""70414 RCC Superstructure"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 3619,
          ""dr"": 0,
          ""or_"": 9186,
          ""bv"": 0,
          ""wv"": 3.32
        },
        {
          ""code"": ""XJOBMIS837"",
          ""desc"": ""70426 Electrical Internal Work"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 29701474,
          ""dr"": 0,
          ""or_"": 1,
          ""bv"": 0,
          ""wv"": 2.97
        },
        {
          ""code"": ""XJOBALU110"",
          ""desc"": ""70420 Windows"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 68808,
          ""dr"": 0,
          ""or_"": 386,
          ""bv"": 0,
          ""wv"": 2.65
        },
        {
          ""code"": ""XJOBMIS078"",
          ""desc"": ""70414 RCC Superstructure"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 33246,
          ""dr"": 0,
          ""or_"": 618,
          ""bv"": 0,
          ""wv"": 2.06
        },
        {
          ""code"": ""XJOBFLR101"",
          ""desc"": ""70418 Finishing Work Tiling an"",
          ""u"": """",
          ""dq"": 3206,
          ""oq"": 2900,
          ""dr"": 6790,
          ""or_"": 6790,
          ""bv"": 2.18,
          ""wv"": 1.97
        },
        {
          ""code"": ""XJOBMAR116"",
          ""desc"": ""70418 Finishing Work Tiling an"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 5332,
          ""dr"": 0,
          ""or_"": 2977,
          ""bv"": 0,
          ""wv"": 1.59
        },
        {
          ""code"": ""XJOBMIS915"",
          ""desc"": ""70424 Plumbing Internal Work"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 12798207,
          ""dr"": 0,
          ""or_"": 1,
          ""bv"": 0,
          ""wv"": 1.28
        },
        {
          ""code"": ""XJOBMIS828"",
          ""desc"": ""70432 Fire Fighting"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 12627285,
          ""dr"": 0,
          ""or_"": 1,
          ""bv"": 0,
          ""wv"": 1.26
        },
        {
          ""code"": ""XJOBFLR104"",
          ""desc"": ""70418 Finishing Work Tiling an"",
          ""u"": """",
          ""dq"": 12374,
          ""oq"": 6311,
          ""dr"": 2100,
          ""or_"": 1858,
          ""bv"": 2.6,
          ""wv"": 1.17
        },
        {
          ""code"": ""XJOBMIS918"",
          ""desc"": ""70441 Sales Office, Sample Fla"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 11358275,
          ""dr"": 0,
          ""or_"": 1,
          ""bv"": 0,
          ""wv"": 1.14
        },
        {
          ""code"": ""XJOBMIS061"",
          ""desc"": ""70407 Foundation RCC Work"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 135,
          ""dr"": 0,
          ""or_"": 83000,
          ""bv"": 0,
          ""wv"": 1.12
        },
        {
          ""code"": ""XJOBCON003"",
          ""desc"": ""70407 Foundation RCC Work"",
          ""u"": """",
          ""dq"": 0,
          ""oq"": 1231,
          ""dr"": 0,
          ""or_"": 8967,
          ""bv"": 0,
          ""wv"": 1.1
        }
      ],
      ""boqTot"": {
        ""design"": 30.13,
        ""order"": 53.99
      },
      ""cr"": [],
      ""woLines"": {
        ""PO/8/000085/26-27"": [
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [APRIL2026 ( 1NOS FOR DAY DUTY & 1"",
            ""unit"": ""Daily"",
            ""qty"": 60,
            ""rate"": 566.67,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [MAY2026 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 62,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [JUNE2026 ( 1NOS FOR DAY DUTY & 1 "",
            ""unit"": ""Daily"",
            ""qty"": 60,
            ""rate"": 566.67,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [JULY2026 ( 1NOS FOR DAY DUTY & 1 "",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [AUGUST2026 ( 1NOS FOR DAY DUTY & "",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [SEPT2026 ( 1NOS FOR DAY DUTY & 1 "",
            ""unit"": ""Daily"",
            ""qty"": 60,
            ""rate"": 566.67,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [OCT2026 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [NOV2026 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 60,
            ""rate"": 566.67,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [DEC2026 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [JAN2027 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [FEB2027 ( 1NOS FOR DAY DUTY & 1 N"",
            ""unit"": ""Daily"",
            ""qty"": 56,
            ""rate"": 607.14,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [MARCH2027 ( 1NOS FOR DAY DUTY & 1"",
            ""unit"": ""Daily"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000,
            ""tax"": -6120,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          }
        ],
        ""PO/8/000449/25-26"": [
          {
            ""item"": ""XCONSER045"",
            ""desc"": ""Quality Control Management Inspection Body (QA/QC) MEP S"",
            ""unit"": ""Monthly"",
            ""qty"": 6,
            ""rate"": 41667,
            ""amt"": 250002,
            ""tax"": -45000.36,
            ""bqty"": 5,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 22500.18,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 22500.18,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000450/25-26"": [
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [2MP DOME  30 Mtr]"",
            ""unit"": ""Number"",
            ""qty"": 45,
            ""rate"": 3000,
            ""amt"": 135000,
            ""tax"": -24300,
            ""bqty"": 27,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [2MP Bullet 30 Mtr]"",
            ""unit"": ""Number"",
            ""qty"": 5,
            ""rate"": 3000,
            ""amt"": 15000,
            ""tax"": -2700,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Seagate 6 TB HDD]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 13000,
            ""amt"": 26000,
            ""tax"": -4680,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [32CH NVR]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 12500,
            ""amt"": 12500,
            ""tax"": -2250,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV005"",
            ""desc"": ""Network Rack [6U RACK]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 3750,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV004"",
            ""desc"": ""POE Switch [24PORT POE GIGABIT without Fiber option]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 16000,
            ""amt"": 32000,
            ""tax"": -5760,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Installation Labour]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 60000,
            ""amt"": 60000,
            ""tax"": -10800,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 25920,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 25920,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000439/25-26"": [
          {
            ""item"": ""XJOBLBR022"",
            ""desc"": ""Lumpsum for Construction Contracts (Sq.Ft.)"",
            ""unit"": ""Daily"",
            ""qty"": 312,
            ""rate"": 653.85,
            ""amt"": 204001.2,
            ""tax"": -36720.22,
            ""bqty"": 47,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 18360.11,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 18360.11,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000429/25-26"": [
          {
            ""item"": ""XJOBFLR113"",
            ""desc"": ""Providing and laying I.P.S. flooring of 1:2:4 grade, 40 "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 195,
            ""rate"": 500,
            ""amt"": 97500,
            ""tax"": -17550,
            ""bqty"": 195,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBBRI125"",
            ""desc"": ""Providing & constructing AAC (Siporex block) masonry wal"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 5,
            ""rate"": 1450,
            ""amt"": 7250,
            ""tax"": -1305,
            ""bqty"": 3.22,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBPLR101"",
            ""desc"": ""Providing and applying 12mm-15mm thick single Coat Inter"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 10,
            ""rate"": 470,
            ""amt"": 4700,
            ""tax"": -846,
            ""bqty"": 3.82,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBSTP026"",
            ""desc"": ""Proviidng & Laying integral cement based treatment for w"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 9,
            ""rate"": 4250,
            ""amt"": 38250,
            ""tax"": -6885,
            ""bqty"": 6.98,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBLBR008"",
            ""desc"": ""Civil miscellaneous works(Sq. Meter) [Brickbat Coba - Pr"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 9,
            ""rate"": 1250,
            ""amt"": 11250,
            ""tax"": -2025,
            ""bqty"": 9,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBSEC030"",
            ""desc"": ""Providing & Fixing  approved Vitrified tiles (Colour, pa"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 20,
            ""rate"": 1738,
            ""amt"": 34760,
            ""tax"": -6256.8,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMAR116"",
            ""desc"": ""Providing & fixing of approved Vitrified tile dado ( req"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 18.6,
            ""rate"": 1738,
            ""amt"": 32326.8,
            ""tax"": -5818.82,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR116"",
            ""desc"": ""Providing & fixing of approved Vitrified tile dado ( req"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 1.5,
            ""rate"": 1889,
            ""amt"": 2833.5,
            ""tax"": -510.04,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Miscellaneous items for Work (Sq. Meter) [Same as item a"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 5.25,
            ""rate"": 2513,
            ""amt"": 13193.25,
            ""tax"": -2374.78,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS237"",
            ""desc"": ""Miscellaneous items for Work (R. Meter) [Wash Basin Coun"",
            ""unit"": ""R Meter"",
            ""qty"": 2.2,
            ""rate"": 6500,
            ""amt"": 14300,
            ""tax"": -2574,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS237"",
            ""desc"": ""Miscellaneous items for Work (R. Meter) [Kitchen Platfor"",
            ""unit"": ""R Meter"",
            ""qty"": 1.78,
            ""rate"": 8625,
            ""amt"": 15352.5,
            ""tax"": -2763.46,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Miscellaneous items for Work (Sq. Meter) [Supply & Insta"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 175,
            ""rate"": 2600,
            ""amt"": 455000,
            ""tax"": -81900,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          }
        ],
        ""PO/8/000419/25-26"": [
          {
            ""item"": ""XJOBMIS999"",
            ""desc"": ""Housekeeping and Supervisor Charges [Providing one House"",
            ""unit"": ""Days"",
            ""qty"": 151,
            ""rate"": 688.41,
            ""amt"": 103950.06,
            ""tax"": -18711.02,
            ""bqty"": 80,
            ""wpRet"": null,
            ""code"": ""74602"",
            ""cdesc"": ""Housekeeping Charges""
          },
          {
            ""item"": ""XJOBMIS999"",
            ""desc"": ""Housekeeping and Supervisor Charges [Consumable - as per"",
            ""unit"": ""Monthly"",
            ""qty"": 5,
            ""rate"": 2000,
            ""amt"": 10000,
            ""tax"": -1800,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""74602"",
            ""cdesc"": ""Housekeeping Charges""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10255.51,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10255.51,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000363/25-26"": [
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Department Labo"",
            ""unit"": ""Number"",
            ""qty"": 450,
            ""rate"": 650,
            ""amt"": 292500,
            ""tax"": -52650,
            ""bqty"": 418,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Supply of Break"",
            ""unit"": ""Number"",
            ""qty"": 180,
            ""rate"": 1300,
            ""amt"": 234000,
            ""tax"": -42120,
            ""bqty"": 23,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Supply of Super"",
            ""unit"": ""Number"",
            ""qty"": 90,
            ""rate"": 750,
            ""amt"": 67500,
            ""tax"": -12150,
            ""bqty"": 11.5,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 53460,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 53460,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000340/25-26"": [
          {
            ""item"": ""XJOBMIS239"",
            ""desc"": ""Miscellaneous items for Entrance and typical lobbies (Sq"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 75,
            ""rate"": 4812,
            ""amt"": 360900,
            ""tax"": -64962,
            ""bqty"": 75,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBSTL103"",
            ""desc"": ""Providing and fixing M.S handrail of 50 mm dia pipe incl"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 381,
            ""rate"": 2600,
            ""amt"": 990600,
            ""tax"": -178308,
            ""bqty"": 342.08,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBMIS197"",
            ""desc"": ""Supply ,Erecting & Dismantling MS Scaffolding work  For "",
            ""unit"": ""Sq. Ft"",
            ""qty"": 2153,
            ""rate"": 260,
            ""amt"": 559780,
            ""tax"": -100760.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 172015.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 172015.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000333/25-26"": [
          {
            ""item"": ""XCONSER068"",
            ""desc"": ""Appointing a Consultant for Quantity Survey work (Sq. Ft"",
            ""unit"": ""Lumpsum"",
            ""qty"": 1,
            ""rate"": 10000,
            ""amt"": 10000,
            ""tax"": -1800,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER068"",
            ""desc"": ""Appointing a Consultant for Quantity Survey work (Sq. Ft"",
            ""unit"": ""Percentage"",
            ""qty"": 30,
            ""rate"": 1486.48,
            ""amt"": 44594.4,
            ""tax"": -8027,
            ""bqty"": 30,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER068"",
            ""desc"": ""Appointing a Consultant for Quantity Survey work (Sq. Ft"",
            ""unit"": ""Percentage"",
            ""qty"": 30,
            ""rate"": 1486.48,
            ""amt"": 44594.4,
            ""tax"": -8027,
            ""bqty"": 30,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER068"",
            ""desc"": ""Appointing a Consultant for Quantity Survey work (Sq. Ft"",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 1486.48,
            ""amt"": 29729.6,
            ""tax"": -5351.32,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER068"",
            ""desc"": ""Appointing a Consultant for Quantity Survey work (Sq. Ft"",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 1486.48,
            ""amt"": 29729.6,
            ""tax"": -5351.32,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 14278.32,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 14278.32,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000203/25-26"": [
          {
            ""item"": ""XCONSER045"",
            ""desc"": ""Quality Consultant [Quality Control Management Inspectio"",
            ""unit"": ""Monthly"",
            ""qty"": 12,
            ""rate"": 91600,
            ""amt"": 1099200,
            ""tax"": -197856,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 98928,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 98928,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000182/25-26"": [
          {
            ""item"": ""XJOBMIS918"",
            ""desc"": ""Miscellaneous items for Sample flat [Civil Work-Tiling A"",
            ""unit"": ""Lumpsum"",
            ""qty"": 1267628,
            ""rate"": 1,
            ""amt"": 1267628,
            ""tax"": -228173.04,
            ""bqty"": 1267627,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS918"",
            ""desc"": ""Miscellaneous items for Sample flat [Pop / False Ceiling"",
            ""unit"": ""Lumpsum"",
            ""qty"": 305750,
            ""rate"": 1,
            ""amt"": 305750,
            ""tax"": -55035,
            ""bqty"": 210178,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS918"",
            ""desc"": ""Miscellaneous items for Sample flat [Carpentry Work]"",
            ""unit"": ""Lumpsum"",
            ""qty"": 6495904,
            ""rate"": 1,
            ""amt"": 6495904,
            ""tax"": -1169262.72,
            ""bqty"": 6446454,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS918"",
            ""desc"": ""Miscellaneous items for Sample flat [Painting & Polishin"",
            ""unit"": ""Lumpsum"",
            ""qty"": 791850,
            ""rate"": 1,
            ""amt"": 791850,
            ""tax"": -142533,
            ""bqty"": 464230,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS917"",
            ""desc"": ""Miscellaneous items for Specialised work [Plumbing Work]"",
            ""unit"": ""Lumpsum"",
            ""qty"": 899020,
            ""rate"": 1,
            ""amt"": 899020,
            ""tax"": -161823.6,
            ""bqty"": 899020,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS917"",
            ""desc"": ""Miscellaneous items for Specialised work [Electrical Wor"",
            ""unit"": ""Lumpsum"",
            ""qty"": 982719,
            ""rate"": 1,
            ""amt"": 982719,
            ""tax"": -176889.42,
            ""bqty"": 962310,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBMIS918"",
            ""desc"": ""Miscellaneous items for Sample flat [White Goods materia"",
            ""unit"": ""Lumpsum"",
            ""qty"": 2497143,
            ""rate"": 1,
            ""amt"": 2497143,
            ""tax"": -449485.74,
            ""bqty"": 2480000,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1191601.26,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1191601.26,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000113/25-26"": [
          {
            ""item"": ""XJOBSEC029"",
            ""desc"": ""PCC Providing and laying 75mm  to 100mm thick PCC screed"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 400,
            ""rate"": 800,
            ""amt"": 320000,
            ""tax"": -57600,
            ""bqty"": 378,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBFLR101"",
            ""desc"": ""Italian marble flooring - Laying Italian Marble Flooring"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 0,
            ""rate"": 2152,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR102"",
            ""desc"": ""Supply & Fixing of marble ,base rate of Marble -280 Rs /"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 125,
            ""rate"": 6853,
            ""amt"": 856625,
            ""tax"": -154192.5,
            ""bqty"": 125,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR102"",
            ""desc"": ""Supply & Fixing of marble ,base rate of Marble -305Rs /S"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 300,
            ""rate"": 7273,
            ""amt"": 2181900,
            ""tax"": -392742,
            ""bqty"": 231.2,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBSKR102"",
            ""desc"": ""Supply & Fixing of Marble skirting"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 8,
            ""rate"": 8600,
            ""amt"": 68800,
            ""tax"": -12384,
            ""bqty"": 7.33,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Making of the V -Groove on the Marble Flooring"",
            ""unit"": ""R Meter"",
            ""qty"": 0,
            ""rate"": 200,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Carrying out Diamond Mirror Polishing Treatment On Laid "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 365,
            ""rate"": 775,
            ""amt"": 282875,
            ""tax"": -50917.5,
            ""bqty"": 185,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Double height Dado Italian Marble with Ardex PU 5- Fixin"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 0,
            ""rate"": 4037,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Supply & Fixing of marble ,base rate of Marble -1150 Rs "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 60,
            ""rate"": 21500,
            ""amt"": 1290000,
            ""tax"": -232200,
            ""bqty"": 50,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Supply  & Fixing of marble ,base rate of Marble -300 Rs "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 60,
            ""rate"": 8700,
            ""amt"": 522000,
            ""tax"": -93960,
            ""bqty"": 59.71,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Supply  & Fixing of marble ,base rate of Marble -315 Rs "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 210,
            ""rate"": 9000,
            ""amt"": 1890000,
            ""tax"": -340200,
            ""bqty"": 201.75,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Supply & Fixing jam line 150mm to 200 mm width , Door Si"",
            ""unit"": ""R Meter"",
            ""qty"": 40,
            ""rate"": 1550,
            ""amt"": 62000,
            ""tax"": -11160,
            ""bqty"": 35.74,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          }
        ],
        ""PO/8/000080/25-26"": [
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Removing of construction debris with manually loading in"",
            ""unit"": ""Number"",
            ""qty"": 150,
            ""rate"": 5500,
            ""amt"": 825000,
            ""tax"": 0,
            ""bqty"": 136,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000070/25-26"": [
          {
            ""item"": ""XJOBMIS081"",
            ""desc"": ""SS Glass Railing (Deck Area and Kitchen Utility) _x000D_"",
            ""unit"": ""R Meter"",
            ""qty"": 429,
            ""rate"": 10500,
            ""amt"": 4504500,
            ""tax"": -810810,
            ""bqty"": 429,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 405405,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 405405,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000069/25-26"": [
          {
            ""item"": ""XJOBLBR015"",
            ""desc"": ""Installation of MCM Slates Sunny of Size 1200 x 600 mman"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 47786,
            ""rate"": 96,
            ""amt"": 4587456,
            ""tax"": -825742.08,
            ""bqty"": 41866,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 412871.04,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 412871.04,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000026/25-26"": [
          {
            ""item"": ""XJOBMIS900"",
            ""desc"": ""Providing and Fixing of FRP Jali (1050 mm x 2150 mm) 18m"",
            ""unit"": ""Number"",
            ""qty"": 60,
            ""rate"": 34575,
            ""amt"": 2074500,
            ""tax"": -373410,
            ""bqty"": 59,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBMIS900"",
            ""desc"": ""Providing and Fixing of FRP Jali (700 mm x 2620 mm) 18mm"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 27366,
            ""amt"": 766248,
            ""tax"": -137924.64,
            ""bqty"": 4,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBMIS084"",
            ""desc"": ""Providing and fixing H frame scaffolding. size 6.5ft x 3"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 8000,
            ""rate"": 22,
            ""amt"": 176000,
            ""tax"": -31680,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBMIS236"",
            ""desc"": ""Miscellaneous items for Work (Number) [FRP FLORAL jali b"",
            ""unit"": ""Number"",
            ""qty"": 14,
            ""rate"": 12000,
            ""amt"": 168000,
            ""tax"": -30240,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS236"",
            ""desc"": ""Miscellaneous items for Work (Number) [Holding Stand - S"",
            ""unit"": ""Number"",
            ""qty"": 14,
            ""rate"": 2100,
            ""amt"": 29400,
            ""tax"": -5292,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 289273.32,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 289273.32,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000006/25-26"": [
          {
            ""item"": ""XJOBMIS947"",
            ""desc"": ""Providing, fabricating & Fixing ISMB 150x75, ISMC 75x40 "",
            ""unit"": ""Kilograms"",
            ""qty"": 1501,
            ""rate"": 110,
            ""amt"": 165110,
            ""tax"": -29719.8,
            ""bqty"": 1501,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMIS948"",
            ""desc"": ""Drilling, Providing & Fixing Hilti make safety stud anch"",
            ""unit"": ""Number"",
            ""qty"": 104,
            ""rate"": 305,
            ""amt"": 31720,
            ""tax"": -5709.6,
            ""bqty"": 104,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMIS948"",
            ""desc"": ""Providing & Fixing of Load hooks in U shape including to"",
            ""unit"": ""Number"",
            ""qty"": 12,
            ""rate"": 200,
            ""amt"": 2400,
            ""tax"": -432,
            ""bqty"": 12,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBFEN004"",
            ""desc"": ""Providing & Fixing MS Shade with fixing corrugated sheet"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 24.87,
            ""rate"": 610,
            ""amt"": 15170.7,
            ""tax"": -2730.72,
            ""bqty"": 24.87,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMSC001"",
            ""desc"": ""Fencing Removing (Qty – 15 sqm)"",
            ""unit"": ""Lumpsum"",
            ""qty"": 1,
            ""rate"": 1500,
            ""amt"": 1500,
            ""tax"": -270,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMIS098"",
            ""desc"": ""Providing, fabricating and fixing structural steel for c"",
            ""unit"": ""Kilograms"",
            ""qty"": 1940.5,
            ""rate"": 138,
            ""amt"": 267789,
            ""tax"": -48202.02,
            ""bqty"": 1940.5,
            ""wpRet"": null,
            ""code"": ""70412"",
            ""cdesc"": ""Podium Finishing""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Providing, fixing M20 bolts in MS structures with pin pr"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 3100,
            ""amt"": 6200,
            ""tax"": -1116,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBLBR014"",
            ""desc"": ""Civil miscellaneous works(Kilograms) [Labour for  Fabric"",
            ""unit"": ""Kilograms"",
            ""qty"": 396,
            ""rate"": 38,
            ""amt"": 15048,
            ""tax"": -2708.64,
            ""bqty"": 115.34,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR004"",
            ""desc"": ""Labour (Patel) rate for Civil works - Superstructure [La"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 11,
            ""rate"": 160,
            ""amt"": 1760,
            ""tax"": -316.8,
            ""bqty"": 10.95,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR014"",
            ""desc"": ""Civil miscellaneous works(Kilograms) [Labour for removin"",
            ""unit"": ""Kilograms"",
            ""qty"": 1723,
            ""rate"": 18,
            ""amt"": 31014,
            ""tax"": -5582.52,
            ""bqty"": 1597.24,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR004"",
            ""desc"": ""Labour (Patel) rate for Civil works - Superstructure [La"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 277,
            ""rate"": 65,
            ""amt"": 18005,
            ""tax"": -3240.9,
            ""bqty"": 277,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Civil miscellaneous works(Number) [Providing & Fixing MS"",
            ""unit"": ""Number"",
            ""qty"": 10,
            ""rate"": 1500,
            ""amt"": 15000,
            ""tax"": -2700,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          }
        ],
        ""PO/8/000007/25-26"": [
          {
            ""item"": ""XJOBMSC001"",
            ""desc"": ""Supply, installation, fixing and removing of the H frame"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 10358,
            ""rate"": 205,
            ""amt"": 2123390,
            ""tax"": -382210.2,
            ""bqty"": 9340.46,
            ""wpRet"": null,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 191105.1,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 191105.1,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000438/24-25"": [
          {
            ""item"": ""XJOBFAC012"",
            ""desc"": ""Spider Glazing System: SPIDER GLAZING WITH ENTRANCE DOOR"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 159,
            ""rate"": 14290,
            ""amt"": 2272110,
            ""tax"": -408979.8,
            ""bqty"": 152.14,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBFAC013"",
            ""desc"": ""Vision Glass : 6mm+1.52 Sentry glass +6mm, Extra over on"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 159,
            ""rate"": 6810,
            ""amt"": 1082790,
            ""tax"": -194902.2,
            ""bqty"": 152.14,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBSPL028"",
            ""desc"": ""Curtain wall system- Design, Fabrication, Installation, "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 114,
            ""rate"": 8150,
            ""amt"": 929100,
            ""tax"": -167238,
            ""bqty"": 112.95,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBFAC013"",
            ""desc"": ""Vision Glass : 6mm+1.52 Sentry glass +6mm, Extra over on"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 114,
            ""rate"": 7680,
            ""amt"": 875520,
            ""tax"": -157593.6,
            ""bqty"": 112.95,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBFAC011"",
            ""desc"": ""Patch Fitting Doors : Extra over on the item 1- The entr"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 21.6,
            ""rate"": 11050,
            ""amt"": 238680,
            ""tax"": -42962.4,
            ""bqty"": 18.5,
            ""wpRet"": null,
            ""code"": ""70423"",
            ""cdesc"": ""Facade Work""
          },
          {
            ""item"": ""XJOBMIS098"",
            ""desc"": ""MS CONSUMPTION :Extra over on the item'D.1'- Supply and "",
            ""unit"": ""Kilograms"",
            ""qty"": 0,
            ""rate"": 175,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70412"",
            ""cdesc"": ""Podium Finishing""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 485838,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 485838,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000430/24-25"": [
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Plumbing works:- Core Cutting in RCC members(beam, slab,"",
            ""unit"": ""Inch"",
            ""qty"": 2902,
            ""rate"": 45,
            ""amt"": 130590,
            ""tax"": 0,
            ""bqty"": 2651,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Plumbing works:- Core Cutting in RCC members(beam, slab,"",
            ""unit"": ""Inch"",
            ""qty"": 3844,
            ""rate"": 58,
            ""amt"": 222952,
            ""tax"": 0,
            ""bqty"": 3775,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Plumbing works:- Core Cutting in RCC members(beam, slab,"",
            ""unit"": ""Inch"",
            ""qty"": 2650,
            ""rate"": 70,
            ""amt"": 185500,
            ""tax"": 0,
            ""bqty"": 2650,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Plumbing works:- Core Cutting in RCC members(beam, slab,"",
            ""unit"": ""Inch"",
            ""qty"": 6602,
            ""rate"": 90,
            ""amt"": 594180,
            ""tax"": 0,
            ""bqty"": 5879,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 0,
            ""rate"": 30,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 462,
            ""rate"": 30,
            ""amt"": 13860,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 70,
            ""rate"": 38,
            ""amt"": 2660,
            ""tax"": 0,
            ""bqty"": 63,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 2618,
            ""rate"": 38,
            ""amt"": 99484,
            ""tax"": 0,
            ""bqty"": 2618,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 1691,
            ""rate"": 42,
            ""amt"": 71022,
            ""tax"": 0,
            ""bqty"": 1390,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 808,
            ""rate"": 45,
            ""amt"": 36360,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 835,
            ""rate"": 58,
            ""amt"": 48430,
            ""tax"": 0,
            ""bqty"": 475,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR017"",
            ""desc"": ""Fire Fightening works :- Core Cutting in RCC members(bea"",
            ""unit"": ""Inch"",
            ""qty"": 279,
            ""rate"": 70,
            ""amt"": 19530,
            ""tax"": 0,
            ""bqty"": 96,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          }
        ],
        ""PO/8/000395/24-25"": [
          {
            ""item"": ""XJOBLBR010"",
            ""desc"": ""Providing and fixing of UPVC Fins with Powder Coating of"",
            ""unit"": ""R Meter"",
            ""qty"": 5561,
            ""rate"": 260,
            ""amt"": 1445860,
            ""tax"": -260254.8,
            ""bqty"": 3339.7,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR015"",
            ""desc"": ""Supply, Installation, fixing & removing of the Scaffoldi"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 30182,
            ""rate"": 25,
            ""amt"": 754550,
            ""tax"": -135819,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 198036.9,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 198036.9,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000335/24-25"": [
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 10200,
            ""rate"": 675,
            ""amt"": 6885000,
            ""tax"": -1239300,
            ""bqty"": 10179.99,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 10200,
            ""rate"": 110,
            ""amt"": 1122000,
            ""tax"": -201960,
            ""bqty"": 10179.97,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 10200,
            ""rate"": 390,
            ""amt"": 3978000,
            ""tax"": -716040,
            ""bqty"": 10179.08,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 7589,
            ""rate"": 671,
            ""amt"": 5092219,
            ""tax"": -916599.42,
            ""bqty"": 7581.03,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 7589,
            ""rate"": 110,
            ""amt"": 834790,
            ""tax"": -150262.2,
            ""bqty"": 7581.03,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 7589,
            ""rate"": 390,
            ""amt"": 2959710,
            ""tax"": -532747.8,
            ""bqty"": 7581.03,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 5147,
            ""rate"": 650,
            ""amt"": 3345550,
            ""tax"": -602199,
            ""bqty"": 5105.99,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 5147,
            ""rate"": 80,
            ""amt"": 411760,
            ""tax"": -74116.8,
            ""bqty"": 5105.99,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBALU110"",
            ""desc"": ""Windows : Design WIND PRESSURE to considere-1.5KPA & win"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 5147,
            ""rate"": 370,
            ""amt"": 1904390,
            ""tax"": -342790.2,
            ""bqty"": 5105.99,
            ""wpRet"": null,
            ""code"": ""70420"",
            ""cdesc"": ""Windows""
          },
          {
            ""item"": ""XJOBMIS255"",
            ""desc"": ""2 Railing for Window Integrated_x000D_ Window Integrated"",
            ""unit"": ""R Meter"",
            ""qty"": 468,
            ""rate"": 3050,
            ""amt"": 1427400,
            ""tax"": -256932,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBMIS255"",
            ""desc"": ""2 Railing for Window Integrated_x000D_ Window Integrated"",
            ""unit"": ""R Meter"",
            ""qty"": 468,
            ""rate"": 1312,
            ""amt"": 614016,
            ""tax"": -110522.88,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBMIS255"",
            ""desc"": ""2 Railing for Window Integrated_x000D_ Window Integrated"",
            ""unit"": ""R Meter"",
            ""qty"": 468,
            ""rate"": 918,
            ""amt"": 429624,
            ""tax"": -77332.32,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          }
        ],
        ""PO/8/000330/24-25"": [
          {
            ""item"": ""XCONSER045"",
            ""desc"": ""Quality Control Management Inspection Body (QA/QC) MEP S"",
            ""unit"": ""Monthly"",
            ""qty"": 12,
            ""rate"": 125000,
            ""amt"": 1500000,
            ""tax"": -270000,
            ""bqty"": 11.5,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 135000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 135000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000308/24-25"": [
          {
            ""item"": ""XJOBMIS400"",
            ""desc"": ""i)Cleaning all garden area_x000D_ ii)filling red soil_x0"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 650,
            ""rate"": 127,
            ""amt"": 82550,
            ""tax"": -14859,
            ""bqty"": 650,
            ""wpRet"": null,
            ""code"": ""70437"",
            ""cdesc"": ""Landscaping and Tree""
          },
          {
            ""item"": ""XJOBLSP005"",
            ""desc"": ""i)Cleaning all garden area_x000D_ ii)filling red soil_x0"",
            ""unit"": ""Number"",
            ""qty"": 250,
            ""rate"": 30,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 250,
            ""wpRet"": null,
            ""code"": ""70437"",
            ""cdesc"": ""Landscaping and Tree""
          },
          {
            ""item"": ""XJOBMIS191"",
            ""desc"": ""Providing Paspalum carpet Lawn"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 1050,
            ""rate"": 54,
            ""amt"": 56700,
            ""tax"": -10206,
            ""bqty"": 1050,
            ""wpRet"": null,
            ""code"": ""70437"",
            ""cdesc"": ""Landscaping and Tree""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 13207.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 13207.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000299/24-25"": [
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of unskilled Labours  for 6 months_x000D_ (4 Nos "",
            ""unit"": ""Number"",
            ""qty"": 728,
            ""rate"": 600,
            ""amt"": 436800,
            ""tax"": -78624,
            ""bqty"": 373.5,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of Civil Mason for 6 months_x000D_ (1 Nos x 182 d"",
            ""unit"": ""Number"",
            ""qty"": 50,
            ""rate"": 1200,
            ""amt"": 60000,
            ""tax"": -10800,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of Breaker Machine Electric for_x000D_ _x000D_ 6 "",
            ""unit"": ""Number"",
            ""qty"": 70,
            ""rate"": 1300,
            ""amt"": 91000,
            ""tax"": -16380,
            ""bqty"": 20,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 52902,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 52902,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000269/24-25"": [
          {
            ""item"": ""XJOBMIS244"",
            ""desc"": ""Providing and applying Elastoroof PU Single Component, L"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 995,
            ""rate"": 855,
            ""amt"": 850725,
            ""tax"": -153130.5,
            ""bqty"": 992.68,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          },
          {
            ""item"": ""XJOBWPR118"",
            ""desc"": ""Protective Screed/plaster: Providing & applying  12mm th"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 995,
            ""rate"": 510,
            ""amt"": 507450,
            ""tax"": -91341,
            ""bqty"": 992.68,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWPR113"",
            ""desc"": ""Brick Bat Coba- Providing & laying an average 4.\"" (110mm"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 148,
            ""rate"": 1450,
            ""amt"": 214600,
            ""tax"": -38628,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWPR107"",
            ""desc"": ""Brick Bat Coba- Providing & laying an 8\"" to 10\"" well soa"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 188.09,
            ""rate"": 6800,
            ""amt"": 1279012,
            ""tax"": -230222.16,
            ""bqty"": 175.93,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Civil miscellaneous works(Number) [Grouting work: Miscel"",
            ""unit"": ""Number"",
            ""qty"": 65,
            ""rate"": 280,
            ""amt"": 18200,
            ""tax"": -3276,
            ""bqty"": 65,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 258298.83,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 258298.83,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000238/24-25"": [
          {
            ""item"": ""XJOBWPR118"",
            ""desc"": ""A UGWT Waterproofing_x000D_ _x000D_ Providing and applyi"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 750,
            ""rate"": 550,
            ""amt"": 412500,
            ""tax"": -74250,
            ""bqty"": 749.54,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWPR118"",
            ""desc"": ""Protective Screed/plaster : Providing & applying  12mm t"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 750,
            ""rate"": 510,
            ""amt"": 382500,
            ""tax"": -68850,
            ""bqty"": 749.54,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWPR118"",
            ""desc"": ""B Pump Room Waterproofing_x000D_ _x000D_ Providing and a"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 517,
            ""rate"": 550,
            ""amt"": 284350,
            ""tax"": -51183,
            ""bqty"": 516.22,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWPR118"",
            ""desc"": ""B Pump Room Waterproofing_x000D_ Protective Screed/plast"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 517,
            ""rate"": 510,
            ""amt"": 263670,
            ""tax"": -47460.6,
            ""bqty"": 516.22,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS236"",
            ""desc"": ""Drill the holes  50 -60 mm depth along the construction "",
            ""unit"": ""Number"",
            ""qty"": 2835,
            ""rate"": 280,
            ""amt"": 793800,
            ""tax"": -142884,
            ""bqty"": 2830,
            ""wpRet"": null,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 192313.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 192313.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000215/24-25"": [
          {
            ""item"": ""XJOBFLR101"",
            ""desc"": ""Providing and laying approved Italian Marble Flooring (C"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 2900,
            ""rate"": 6790,
            ""amt"": 19691000,
            ""tax"": -3544380,
            ""bqty"": 2613.4,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBSKR101"",
            ""desc"": ""Providing and fixing of approved Italian Marble Skirting"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 115,
            ""rate"": 8148,
            ""amt"": 937020,
            ""tax"": -168663.6,
            ""bqty"": 73,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR115"",
            ""desc"": ""Providing and fixing of approved Italian Marble dado of "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 80,
            ""rate"": 6500,
            ""amt"": 520000,
            ""tax"": -93600,
            ""bqty"": 22.87,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR104"",
            ""desc"": ""Providing and laying approved Vitrified Flooring of size"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 500,
            ""rate"": 1610,
            ""amt"": 805000,
            ""tax"": -144900,
            ""bqty"": 354.13,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBSKR103"",
            ""desc"": ""Providing and fixing of approved Vitrified Skirting ( 12"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 225,
            ""rate"": 2107,
            ""amt"": 474075,
            ""tax"": -85333.5,
            ""bqty"": 132.49,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR104"",
            ""desc"": ""Providing and laying approved Vitrified Flooring of size"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 700,
            ""rate"": 1610,
            ""amt"": 1127000,
            ""tax"": -202860,
            ""bqty"": 379.86,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBSKR103"",
            ""desc"": ""Providing and fixing of approved Vitrified skirting 1200"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 107,
            ""rate"": 2107,
            ""amt"": 225449,
            ""tax"": -40580.82,
            ""bqty"": 33.85,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR116"",
            ""desc"": ""Providing & fixing of approved Vitrified tile dado 1200 "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 1000,
            ""rate"": 1580,
            ""amt"": 1580000,
            ""tax"": -284400,
            ""bqty"": 637,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR110"",
            ""desc"": ""Providing and Laying of Restile make Tread of Size 1520m"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 285,
            ""rate"": 1780,
            ""amt"": 507300,
            ""tax"": -91314,
            ""bqty"": 285,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR112"",
            ""desc"": ""Providing & fixing following Restile make Riser of Size "",
            ""unit"": ""Sq. Meter"",
            ""qty"": 160,
            ""rate"": 2046,
            ""amt"": 327360,
            ""tax"": -58924.8,
            ""bqty"": 138.85,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLR104"",
            ""desc"": ""Providing & fixing and cutting size Restile make 600 x 6"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 321,
            ""rate"": 1561,
            ""amt"": 501081,
            ""tax"": -90194.58,
            ""bqty"": 290.27,
            ""wpRet"": null,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBFLB002"",
            ""desc"": ""Providing and fixing of approved Restile Tile of Size 60"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 250,
            ""rate"": 2201,
            ""amt"": 550250,
            ""tax"": -99045,
            ""bqty"": 93.38,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          }
        ],
        ""PO/8/000144/24-25"": [
          {
            ""item"": ""XJOBLBR019"",
            ""desc"": ""Supply of Reinforcement of required size & quantity as d"",
            ""unit"": ""Metric Ton"",
            ""qty"": 2,
            ""rate"": 63800,
            ""amt"": 127600,
            ""tax"": -22968,
            ""bqty"": 1.57,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of departmental labour as required by Engineer In"",
            ""unit"": ""Number"",
            ""qty"": 8,
            ""rate"": 1650,
            ""amt"": 13200,
            ""tax"": -2376,
            ""bqty"": 8,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of departmental labour as required by Engineer In"",
            ""unit"": ""Number"",
            ""qty"": 59,
            ""rate"": 800,
            ""amt"": 47200,
            ""tax"": -8496,
            ""bqty"": 59,
            ""wpRet"": null,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBSTP091"",
            ""desc"": ""Carting away Debris"",
            ""unit"": ""Trip"",
            ""qty"": 15,
            ""rate"": 6050,
            ""amt"": 90750,
            ""tax"": -16335,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBMIS943"",
            ""desc"": ""Providing labour, concrete, steel, breaker etc as requir"",
            ""unit"": ""Lumpsum"",
            ""qty"": 1,
            ""rate"": 198850,
            ""amt"": 198850,
            ""tax"": -35793,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70411"",
            ""cdesc"": ""Podium RCC""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 42984,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 42984,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000124/24-25"": [
          {
            ""item"": ""XJOBSTP020"",
            ""desc"": ""SHUTTERING & FORMWORK_x000D_ Providing, erecting, fixing"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 227,
            ""rate"": 1320,
            ""amt"": 299640,
            ""tax"": -53935.2,
            ""bqty"": 226.06,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 0,
            ""rate"": 215,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 0,
            ""rate"": 292,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 2241,
            ""rate"": 303,
            ""amt"": 679023,
            ""tax"": -122224.14,
            ""bqty"": 2241,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 62,
            ""rate"": 512,
            ""amt"": 31744,
            ""tax"": -5713.92,
            ""bqty"": 62,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 124,
            ""rate"": 605,
            ""amt"": 75020,
            ""tax"": -13503.6,
            ""bqty"": 124,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 53,
            ""rate"": 671,
            ""amt"": 35563,
            ""tax"": -6401.34,
            ""bqty"": 53,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 65,
            ""rate"": 902,
            ""amt"": 58630,
            ""tax"": -10553.4,
            ""bqty"": 65,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 75,
            ""rate"": 660,
            ""amt"": 49500,
            ""tax"": -8910,
            ""bqty"": 75,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 60,
            ""rate"": 919,
            ""amt"": 55140,
            ""tax"": -9925.2,
            ""bqty"": 60,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS919"",
            ""desc"": ""Anchor Rebar - Providing & fixing Hilti cartridge RE - 5"",
            ""unit"": ""Number"",
            ""qty"": 0,
            ""rate"": 1155,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70440"",
            ""cdesc"": ""Swimming Pool""
          },
          {
            ""item"": ""XJOBMIS917"",
            ""desc"": ""APPLICATION OF BONDING AGENT AT JUNCTION OF NEW & OLD CO"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 331,
            ""rate"": 688,
            ""amt"": 227728,
            ""tax"": -40991.04,
            ""bqty"": 330.8,
            ""wpRet"": null,
            ""code"": ""70441"",
            ""cdesc"": ""Sales Office, Sample Flat and Drea""
          }
        ],
        ""PO/8/000035/24-25"": [
          {
            ""item"": ""XJOBMIS249"",
            ""desc"": ""Testing Charges QA,QC,SHE,etc"",
            ""unit"": ""Each"",
            ""qty"": 12,
            ""rate"": 3500,
            ""amt"": 42000,
            ""tax"": -7560,
            ""bqty"": 12,
            ""wpRet"": null,
            ""code"": ""71418"",
            ""cdesc"": ""Testing charges, QA,QC,SHE,etc""
          },
          {
            ""item"": ""XJOBMIS249"",
            ""desc"": ""Testing Charges QA,QC,SHE,etc"",
            ""unit"": ""Each"",
            ""qty"": 1,
            ""rate"": 1500,
            ""amt"": 1500,
            ""tax"": -270,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""71418"",
            ""cdesc"": ""Testing charges, QA,QC,SHE,etc""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3915,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3915,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000528/23-24"": [
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""Against DBR 5%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 57530,
            ""amt"": 57530,
            ""tax"": -10355.4,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""on Submission & approval of shematics & preliminary cost"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 115060,
            ""amt"": 115060,
            ""tax"": -20710.8,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""on Submission & approval of Tender documents BOQ technic"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 115060,
            ""amt"": 115060,
            ""tax"": -20710.8,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""on Shorlisting and finalisation of vendor 10%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 115060,
            ""amt"": 115060,
            ""tax"": -20710.8,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""on GFC drawing submission packages & its approval 15%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 172590,
            ""amt"": 172590,
            ""tax"": -31066.2,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER001"",
            ""desc"": ""pro rata during execution 40%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 460240,
            ""amt"": 460240,
            ""tax"": -82843.2,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER004"",
            ""desc"": ""on handing over of building 5%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 57530,
            ""amt"": 57530,
            ""tax"": -10355.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XCONSER004"",
            ""desc"": ""on completion of building 5%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 57530,
            ""amt"": 57530,
            ""tax"": -10355.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72005"",
            ""cdesc"": ""RCC Consultants""
          },
          {
            ""item"": ""XJOBLFT014"",
            ""desc"": ""Against DBR 5%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 32874,
            ""amt"": 32874,
            ""tax"": -5917.32,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72004"",
            ""cdesc"": ""Service Consultancy (MEP)""
          },
          {
            ""item"": ""XJOBLFT014"",
            ""desc"": ""on Submission & approval of shematics & preliminary cost"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 65749,
            ""amt"": 65749,
            ""tax"": -11834.82,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72004"",
            ""cdesc"": ""Service Consultancy (MEP)""
          },
          {
            ""item"": ""XJOBLFT014"",
            ""desc"": ""on Submission & approval of Tender documents BOQ technic"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 65749,
            ""amt"": 65749,
            ""tax"": -11834.82,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72004"",
            ""cdesc"": ""Service Consultancy (MEP)""
          },
          {
            ""item"": ""XCONSER002"",
            ""desc"": ""on Shorlisting and finalisation of vendor 10%"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 65749,
            ""amt"": 65749,
            ""tax"": -11834.82,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72004"",
            ""cdesc"": ""Service Consultancy (MEP)""
          }
        ],
        ""PO/8/000468/23-24"": [
          {
            ""item"": ""XJOBMIS966"",
            ""desc"": ""1.5 mtr. wide plinth protection"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 0,
            ""rate"": 1244,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70406"",
            ""cdesc"": ""Foundation Building Protection""
          },
          {
            ""item"": ""XJOBBRI113"",
            ""desc"": ""Brick masonry - 230 mm thk. Upto plinth level"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 2,
            ""rate"": 9093,
            ""amt"": 18186,
            ""tax"": -3273.48,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBCON019"",
            ""desc"": ""RMC M20/M25/M30 grade"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 3600,
            ""rate"": 9187,
            ""amt"": 33073200,
            ""tax"": -5953176,
            ""bqty"": 3559.11,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBMIS164"",
            ""desc"": ""Miscellaneous items for Marble / Granite / Tile work"",
            ""unit"": ""Metric Ton"",
            ""qty"": 690,
            ""rate"": 85505,
            ""amt"": 58998450,
            ""tax"": -10619721,
            ""bqty"": 669,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBMIS078"",
            ""desc"": ""Plywood formwork Above Plinth level upto 8th flr. Level"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 26960,
            ""rate"": 748,
            ""amt"": 20166080,
            ""tax"": -3629894.4,
            ""bqty"": 26537.38,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBCON218"",
            ""desc"": ""Miscellaneous works"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 702,
            ""rate"": 167,
            ""amt"": 117234,
            ""tax"": -21102.12,
            ""bqty"": 695.11,
            ""wpRet"": null,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XDUMMY"",
            ""desc"": ""XDUMMY"",
            ""unit"": ""Number"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBBRI102"",
            ""desc"": ""Miscellaneous works"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 300,
            ""rate"": 1840,
            ""amt"": 552000,
            ""tax"": -99360,
            ""bqty"": 167.66,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBBRI102"",
            ""desc"": ""Lightweight Block masonry - 150 mm thk. Upto 8th flr. le"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 5836,
            ""rate"": 1335,
            ""amt"": 7791060,
            ""tax"": -1402390.8,
            ""bqty"": 5279.17,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBBRI102"",
            ""desc"": ""Lightweight Block masonry - 100 mm thk. Upto 8th flr. le"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 5450,
            ""rate"": 1179,
            ""amt"": 6425550,
            ""tax"": -1156599,
            ""bqty"": 5266.57,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBWOD101"",
            ""desc"": ""Red miranti door frames"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 25,
            ""rate"": 141123,
            ""amt"": 3528075,
            ""tax"": -635053.5,
            ""bqty"": 21.36,
            ""wpRet"": null,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBBRI102"",
            ""desc"": ""Gypsum plaster  Upto 8th flr. level"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 14600,
            ""rate"": 415,
            ""amt"": 6059000,
            ""tax"": -1090620,
            ""bqty"": 12596.74,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          }
        ],
        ""PO/8/000444/23-24"": [
          {
            ""item"": ""XJOBMIS997"",
            ""desc"": ""cement concrete foundation including excavation for the "",
            ""unit"": ""Number"",
            ""qty"": 132,
            ""rate"": 1100,
            ""amt"": 145200,
            ""tax"": -26136,
            ""bqty"": 57,
            ""wpRet"": null,
            ""code"": ""70438"",
            ""cdesc"": ""Other Infrastructure Works""
          },
          {
            ""item"": ""XJOBSWD017"",
            ""desc"": ""Structural steel works"",
            ""unit"": ""Kilograms"",
            ""qty"": 6680,
            ""rate"": 110,
            ""amt"": 734800,
            ""tax"": -132264,
            ""bqty"": 5523,
            ""wpRet"": null,
            ""code"": ""70438"",
            ""cdesc"": ""Other Infrastructure Works""
          },
          {
            ""item"": ""XJOBSWD018"",
            ""desc"": ""P/F of 28 guage CGI sheet"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 788,
            ""rate"": 610,
            ""amt"": 480680,
            ""tax"": -86522.4,
            ""bqty"": 674.79,
            ""wpRet"": null,
            ""code"": ""70438"",
            ""cdesc"": ""Other Infrastructure Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 122461.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 122461.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000176/23-24"": [
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""10% As Advance."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 6367.09,
            ""amt"": 63670.9,
            ""tax"": -11460.76,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""20% On LOD 200 LOD 300 LOD 400."",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 6367.09,
            ""amt"": 127341.8,
            ""tax"": -22921.52,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""15% On Clash Report & BOQ."",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 6367.09,
            ""amt"": 95506.35,
            ""tax"": -17191.14,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""15% On Clash Free Report & Final BOQ."",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 6367.09,
            ""amt"": 95506.35,
            ""tax"": -17191.14,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""20% On Final GFC."",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 6367.09,
            ""amt"": 127341.8,
            ""tax"": -22921.52,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""10% On Final BOQ."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 6367.09,
            ""amt"": 63670.9,
            ""tax"": -11460.76,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""XCONSER006"",
            ""desc"": ""10% On Built Drawing."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 6367.09,
            ""amt"": 63670.9,
            ""tax"": -11460.76,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 57303.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 57303.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000194/23-24"": [
          {
            ""item"": ""XJOBEAR001"",
            ""desc"": ""Excavation in all types of soil except hard rock for dep"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 3787,
            ""rate"": 696,
            ""amt"": 2635752,
            ""tax"": -474435.36,
            ""bqty"": 3786.68,
            ""wpRet"": null,
            ""code"": ""70405"",
            ""cdesc"": ""Excavation and Backfilling""
          },
          {
            ""item"": ""XJOBEAR026"",
            ""desc"": ""Backfilling with available earth"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 2134,
            ""rate"": 187,
            ""amt"": 399058,
            ""tax"": -71830.44,
            ""bqty"": 2133.18,
            ""wpRet"": null,
            ""code"": ""70405"",
            ""cdesc"": ""Excavation and Backfilling""
          },
          {
            ""item"": ""XJOBMIS966"",
            ""desc"": ""Anti-termite"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 673,
            ""rate"": 77,
            ""amt"": 51821,
            ""tax"": -9327.78,
            ""bqty"": 672.1,
            ""wpRet"": null,
            ""code"": ""70406"",
            ""cdesc"": ""Foundation Building Protection""
          },
          {
            ""item"": ""XJOBMIS966"",
            ""desc"": ""1.5 mtr. wide plinth protection"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 0,
            ""rate"": 1060,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70406"",
            ""cdesc"": ""Foundation Building Protection""
          },
          {
            ""item"": ""XJOBMIS951"",
            ""desc"": ""P/L Rubble soling"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 57,
            ""rate"": 1777,
            ""amt"": 101289,
            ""tax"": -18232.02,
            ""bqty"": 56.42,
            ""wpRet"": null,
            ""code"": ""70407"",
            ""cdesc"": ""Foundation RCC Work""
          },
          {
            ""item"": ""XJOBMIS951"",
            ""desc"": ""RMC M15 grade"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 108,
            ""rate"": 7580,
            ""amt"": 818640,
            ""tax"": -147355.2,
            ""bqty"": 107.84,
            ""wpRet"": null,
            ""code"": ""70407"",
            ""cdesc"": ""Foundation RCC Work""
          },
          {
            ""item"": ""XJOBCON003"",
            ""desc"": ""RMC M30 grade"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 1231,
            ""rate"": 8967,
            ""amt"": 11038377,
            ""tax"": -1986907.86,
            ""bqty"": 1230.07,
            ""wpRet"": null,
            ""code"": ""70407"",
            ""cdesc"": ""Foundation RCC Work""
          },
          {
            ""item"": ""XJOBMIS061"",
            ""desc"": ""Miscellaneous items for Reinforcement steel  work"",
            ""unit"": ""Metric Ton"",
            ""qty"": 135,
            ""rate"": 83000,
            ""amt"": 11205000,
            ""tax"": -2016900,
            ""bqty"": 134.84,
            ""wpRet"": null,
            ""code"": ""70407"",
            ""cdesc"": ""Foundation RCC Work""
          },
          {
            ""item"": ""XJOBMIS950"",
            ""desc"": ""Plywood Shuttering works"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 2969,
            ""rate"": 642,
            ""amt"": 1906098,
            ""tax"": -343097.64,
            ""bqty"": 2968.23,
            ""wpRet"": null,
            ""code"": ""70407"",
            ""cdesc"": ""Foundation RCC Work""
          },
          {
            ""item"": ""XJOBMIS042"",
            ""desc"": ""Box type waterproofing for horizontal surfaces (Swimming"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 762,
            ""rate"": 1377,
            ""amt"": 1049274,
            ""tax"": -188869.32,
            ""bqty"": 761.08,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBMIS042"",
            ""desc"": ""Box type waterproofing for vertical surfaces (Swimming p"",
            ""unit"": ""Sq. Meter"",
            ""qty"": 736,
            ""rate"": 1432,
            ""amt"": 1053952,
            ""tax"": -189711.36,
            ""bqty"": 735.85,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBBRI203"",
            ""desc"": ""Brick masonry - 230 mm thk. Upto plinth level"",
            ""unit"": ""Cu. Meter"",
            ""qty"": 15,
            ""rate"": 8387,
            ""amt"": 125805,
            ""tax"": -22644.9,
            ""bqty"": 1.51,
            ""wpRet"": null,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          }
        ],
        ""PO/8/000205/23-24"": [
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Mango (Girth 1' - Ht 30')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 25000,
            ""amt"": 25000,
            ""tax"": -4500,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Ashoka (Girth 1' - Ht 35')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 15000,
            ""amt"": 15000,
            ""tax"": -2700,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Nilgiri (Girth 4' - Ht 30')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 25000,
            ""amt"": 25000,
            ""tax"": -4500,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Mango (Girth 1' - Ht 22')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 20000,
            ""amt"": 20000,
            ""tax"": -3600,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Chinch (Girth 9\"" - Ht 22')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 12000,
            ""amt"": 12000,
            ""tax"": -2160,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Gulmohar (Girth 1' - Ht 30')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 22000,
            ""amt"": 22000,
            ""tax"": -3960,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Pipal (Girth 5' - Ht 45')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 45000,
            ""amt"": 45000,
            ""tax"": -8100,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Mango (Girth 1' - Ht 25')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 20000,
            ""amt"": 20000,
            ""tax"": -3600,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 7500,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 7500,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 7500,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""XCONSER066"",
            ""desc"": ""Ashoka (Girth 4\"" - Ht 10')"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 7500,
            ""amt"": 7500,
            ""tax"": -1350,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          }
        ],
        ""PO/8/000240/23-24"": [
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Supply of labour"",
            ""unit"": ""Number"",
            ""qty"": 10,
            ""rate"": 1300,
            ""amt"": 13000,
            ""tax"": -2340,
            ""bqty"": 6,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Supply of labour"",
            ""unit"": ""Number"",
            ""qty"": 50,
            ""rate"": 600,
            ""amt"": 30000,
            ""tax"": -5400,
            ""bqty"": 41,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3870,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3870,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000267/23-24"": [
          {
            ""item"": ""XCONSER016"",
            ""desc"": ""On review of the Proposed Foundation DBRStructural Schem"",
            ""unit"": ""Percentage"",
            ""qty"": 40,
            ""rate"": 1630,
            ""amt"": 65200,
            ""tax"": -11736,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72013"",
            ""cdesc"": ""Review Consultancy""
          },
          {
            ""item"": ""XCONSER016"",
            ""desc"": ""On Review of the Structural analysis &design based on ET"",
            ""unit"": ""Percentage"",
            ""qty"": 40,
            ""rate"": 1630,
            ""amt"": 65200,
            ""tax"": -11736,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72013"",
            ""cdesc"": ""Review Consultancy""
          },
          {
            ""item"": ""XCONSER016"",
            ""desc"": ""On submission of Peer Review Report of Structural Design"",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 1630,
            ""amt"": 32600,
            ""tax"": -5868,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72013"",
            ""cdesc"": ""Review Consultancy""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 14670,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 14670,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000405/23-24"": [
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Supply of labour"",
            ""unit"": ""Number"",
            ""qty"": 20,
            ""rate"": 1300,
            ""amt"": 26000,
            ""tax"": 0,
            ""bqty"": 14,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Supply of labour"",
            ""unit"": ""Number"",
            ""qty"": 70,
            ""rate"": 600,
            ""amt"": 42000,
            ""tax"": 0,
            ""bqty"": 54.5,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          }
        ]
      },
      ""poLines"": {
        ""PO/8/000134/26-27"": [
          {
            ""item"": ""XJOBWELL01"",
            ""desc"": """",
            ""unit"": ""Lumpsum"",
            ""qty"": 144980,
            ""rate"": 1,
            ""amt"": 144980,
            ""tax"": -26096.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70453"",
            ""cdesc"": ""Borewell""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 13048.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 13048.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000049/26-27"": [
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [4U wall mount rock]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 3500,
            ""amt"": 3500,
            ""tax"": -630,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Seagate skyhawk 4 TB surveillance]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 14300,
            ""amt"": 28600,
            ""tax"": -5148,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Rj45 Connector with crimping & testing CCTV"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1050,
            ""amt"": 29400,
            ""tax"": -5292,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [22 Inch Led Monior /TV]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 4800,
            ""amt"": 4800,
            ""tax"": -864,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [CP Plus 16 Port PoE Switch]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 8300,
            ""amt"": 16600,
            ""tax"": -2988,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 7461,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 7461,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000031/26-27"": [
          {
            ""item"": ""MSBUPRM01"",
            ""desc"": ""[Being Work Order booked towards 32 Pcs @ Rs. 3,500/- ea"",
            ""unit"": ""Number"",
            ""qty"": 32,
            ""rate"": 3500,
            ""amt"": 112000,
            ""tax"": 0,
            ""bqty"": 32,
            ""wpRet"": null,
            ""code"": ""75114"",
            ""cdesc"": ""Sales Promotion Expenses""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000013/26-27"": [
          {
            ""item"": ""MEP : PLSWBS01"",
            ""desc"": ""Wash Basin[Kolher Make Forefront Vessel w/faucet deck 26"",
            ""unit"": ""Number"",
            ""qty"": 64,
            ""rate"": 8080,
            ""amt"": 517120,
            ""tax"": -93081.6,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 46540.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 46540.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000007/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""unit"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 720,
            ""tax"": -129.6,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70012"",
            ""cdesc"": ""Transport Charges""
          },
          {
            ""item"": ""SSRMHC023"",
            ""desc"": ""SS Manhole Cover 900x900mm"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 29610,
            ""amt"": 29610,
            ""tax"": -5329.8,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""SSRMHC024"",
            ""desc"": ""SS Manhole Cover 800x800mm"",
            ""unit"": ""Number"",
            ""qty"": 9,
            ""rate"": 25200,
            ""amt"": 226800,
            ""tax"": -40824,
            ""bqty"": 9,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""SSRMHC025"",
            ""desc"": ""SS Manhole Cover 1000x1300mm"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 38700,
            ""amt"": 38700,
            ""tax"": -6966,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""SSRMHC026"",
            ""desc"": ""SS Manhole Cover 950x650mm"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 26100,
            ""amt"": 52200,
            ""tax"": -9396,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 31322.7,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 31322.7,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000530/25-26"": [
          {
            ""item"": ""XJOBMIS925"",
            ""desc"": ""Miscellaneous items for Nallah diversion works"",
            ""unit"": ""Lumpsum"",
            ""qty"": 1,
            ""rate"": 425000,
            ""amt"": 425000,
            ""tax"": -21250,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70433"",
            ""cdesc"": ""Infra Plumbing""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10625,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10625,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000508/25-26"": [
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Rj45 Connector with Crimping & testing Both"",
            ""unit"": ""Number"",
            ""qty"": 27,
            ""rate"": 150,
            ""amt"": 4050,
            ""tax"": -729,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [Camera Installation & Configration]"",
            ""unit"": ""Number"",
            ""qty"": 27,
            ""rate"": 450,
            ""amt"": 12150,
            ""tax"": -2187,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [One Year Service Support Warranty]"",
            ""unit"": ""Number"",
            ""qty"": 27,
            ""rate"": 650,
            ""amt"": 17550,
            ""tax"": -3159,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [PVC Encluser Box]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 1350,
            ""amt"": 2700,
            ""tax"": -486,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [6U Wall Mount Rack]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 3500,
            ""amt"": 3500,
            ""tax"": -630,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""XJOBCCTV001"",
            ""desc"": ""CCTV Camera [32 inch Led Monitor / TV]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 9800,
            ""amt"": 9800,
            ""tax"": -1764,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70027"",
            ""cdesc"": ""CCTV Camera and Video Door system""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4477.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4477.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000510/25-26"": [
          {
            ""item"": ""XJOBMIS924"",
            ""desc"": ""Miscellaneous items for Water supply networks [Supply of"",
            ""unit"": ""Number"",
            ""qty"": 20,
            ""rate"": 1500,
            ""amt"": 30000,
            ""tax"": 0,
            ""bqty"": 20,
            ""wpRet"": null,
            ""code"": ""70452"",
            ""cdesc"": ""Water supply networks""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000467/25-26"": [
          {
            ""item"": ""MEP : PCPNTS01"",
            ""desc"": ""Nahani Trap SS [SS 304 tile insert floor drain of size 1"",
            ""unit"": ""Number"",
            ""qty"": 207,
            ""rate"": 659,
            ""amt"": 136413,
            ""tax"": -24554.34,
            ""bqty"": 207,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPSSCJ0"",
            ""desc"": ""SS-304 COCKROACH FLOOR DRAIN JALI 6\"" X 6\""  with hole [SS"",
            ""unit"": ""Number"",
            ""qty"": 280,
            ""rate"": 702,
            ""amt"": 196560,
            ""tax"": -35380.8,
            ""bqty"": 280,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 29967.57,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 29967.57,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000454/25-26"": [
          {
            ""item"": ""MEP : PLSDVP00"",
            ""desc"": ""Diverter upper plate [DIVERTOR - HG Vernis shape FS bath"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 2240,
            ""amt"": 8960,
            ""tax"": -1612.8,
            ""bqty"": 4,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPOHS01"",
            ""desc"": ""Overhead Shower [SHOWER - HG Vernis shape 230mm vario ov"",
            ""unit"": ""Number"",
            ""qty"": 6,
            ""rate"": 3120,
            ""amt"": 18720,
            ""tax"": -3369.6,
            ""bqty"": 6,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPSHA01"",
            ""desc"": ""Shower arm [SHOWER ARM - HG shower arm E 390mm chrome - "",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 8435,
            ""amt"": 33740,
            ""tax"": -6073.2,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPBST01"",
            ""desc"": ""Bath Spout [SPOUT - HG Vernis Blend Bath spout CN - 7142"",
            ""unit"": ""Number"",
            ""qty"": 68,
            ""rate"": 1825,
            ""amt"": 124100,
            ""tax"": -22338,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPHNS01"",
            ""desc"": ""Hand Shower [HAND SHOWER - HG Rain dance Select E 120 Ha"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 5266,
            ""amt"": 21064,
            ""tax"": -3791.52,
            ""bqty"": 4,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPSNF01"",
            ""desc"": ""Sink Faucet  Combo [BASIN FAUCET - HG Vernis Blend BM 10"",
            ""unit"": ""Number"",
            ""qty"": 8,
            ""rate"": 3640,
            ""amt"": 29120,
            ""tax"": -5241.6,
            ""bqty"": 8,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : PCPCND01"",
            ""desc"": ""Concealed Divertor  for shower Mixer [DIVERTOR - HG Vern"",
            ""unit"": ""Number"",
            ""qty"": 8,
            ""rate"": 1315,
            ""amt"": 10520,
            ""tax"": -1893.6,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMPLB0316"",
            ""desc"": ""Health Faucet [HEALTH FAUCET & ACCESSORIES - HG Bidette "",
            ""unit"": ""Number"",
            ""qty"": 23,
            ""rate"": 1730,
            ""amt"": 39790,
            ""tax"": -7162.2,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          }
        ],
        ""PO/8/000402/25-26"": [
          {
            ""item"": ""XJOBMIS243"",
            ""desc"": ""Miscellaneous Work for Fire Fighting(Sq. Ft)"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 390,
            ""rate"": 850,
            ""amt"": 331500,
            ""tax"": -59670,
            ""bqty"": 240.41,
            ""wpRet"": null,
            ""code"": ""70432"",
            ""cdesc"": ""Fire Fighting""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 29835,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 29835,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000377/25-26"": [
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Art  For wall"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 63500,
            ""amt"": 63500,
            ""tax"": -7620,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""MEP : ELEARL01"",
            ""desc"": ""Acrylic LED Lamp"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 3500,
            ""amt"": 7000,
            ""tax"": -840,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Base Material"",
            ""unit"": ""Bags"",
            ""qty"": 2,
            ""rate"": 1000,
            ""amt"": 2000,
            ""tax"": -360,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Crush Pebble"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 500,
            ""amt"": 500,
            ""tax"": -90,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Décor Items"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 10000,
            ""amt"": 10000,
            ""tax"": -1800,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Flower Vase with Flower"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 1250,
            ""amt"": 5000,
            ""tax"": -250,
            ""bqty"": 4,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""FRP Planter"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 20500,
            ""amt"": 41000,
            ""tax"": -7380,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""FRP Planter"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 14500,
            ""amt"": 29000,
            ""tax"": -5220,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          }
        ],
        ""PO/8/000352/25-26"": [
          {
            ""item"": ""XJOBMIS900"",
            ""desc"": ""Miscellaneous works(Lumpsum) [[WAREHOUSE RENT PER MONTH "",
            ""unit"": ""Lumpsum"",
            ""qty"": 1,
            ""rate"": 75000,
            ""amt"": 75000,
            ""tax"": -13500,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6750,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6750,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000323/25-26"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 2448565,
            ""rate"": 1,
            ""amt"": 2448565,
            ""tax"": -440741.7,
            ""bqty"": 2338125.44,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 220370.85,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 220370.85,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000314/25-26"": [
          {
            ""item"": ""Furniture : FU"",
            ""desc"": ""Side Table"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 16000,
            ""amt"": 16000,
            ""tax"": -2880,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Furniture : FU"",
            ""desc"": ""Single Seater Chair"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 32000,
            ""amt"": 64000,
            ""tax"": -11520,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Furniture : FU"",
            ""desc"": ""Swing Two Seater"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 64000,
            ""amt"": 64000,
            ""tax"": -11520,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Cushion"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 2200,
            ""amt"": 2200,
            ""tax"": -396,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Cushion"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 2200,
            ""amt"": 4400,
            ""tax"": -792,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""IGST"",
            ""desc"": ""IGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 27108,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18913"",
            ""cdesc"": ""Input Tax Credit - IGST""
          }
        ],
        ""PO/8/000285/25-26"": [
          {
            ""item"": ""MEP : ELECHN01"",
            ""desc"": ""Chandelier [Length 200mmsolid glass.900x255mm C shape su"",
            ""unit"": ""Number"",
            ""qty"": 3,
            ""rate"": 127500,
            ""amt"": 382500,
            ""tax"": -68850,
            ""bqty"": 3,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 34425,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 34425,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000299/25-26"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 127035,
            ""rate"": 1,
            ""amt"": 127035,
            ""tax"": -22866.3,
            ""bqty"": 127035,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 11433.15,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 11433.15,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000262/25-26"": [
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Size 13x13x12 cm.]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 1500,
            ""amt"": 1500,
            ""tax"": -270,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Small Decor Size 16.5x6.5x5.8cm & 15x5.5x7.5 "",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 2650,
            ""amt"": 2650,
            ""tax"": -477,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Model 110134-S-GD, Size 130x93x230mm, Materia"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 4850,
            ""amt"": 4850,
            ""tax"": -873,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Table Decor, size 49.5x47x36cm]"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 5340,
            ""amt"": 5340,
            ""tax"": -961.2,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Model no.110110, Size 135x125x290 mm , Materi"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 9250,
            ""amt"": 9250,
            ""tax"": -1665,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Model B782BK, Size 21x13x14cm, Material:- Met"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 6500,
            ""amt"": 6500,
            ""tax"": -1170,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts [Model 110035, Size 250x220x420mm, Material :-"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 9850,
            ""amt"": 9850,
            ""tax"": -1773,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Artifacts"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 1425,
            ""amt"": 1425,
            ""tax"": -256.5,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          }
        ],
        ""PO/8/000261/25-26"": [
          {
            ""item"": ""MEP : ELEARL01"",
            ""desc"": ""Acrylic LED Lamp"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 3500,
            ""amt"": 7000,
            ""tax"": -1260,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Base Material [Size 6'x5']"",
            ""unit"": ""Bags"",
            ""qty"": 10,
            ""rate"": 1000,
            ""amt"": 10000,
            ""tax"": -1800,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Bushes & Creepers"",
            ""unit"": ""Number"",
            ""qty"": 14,
            ""rate"": 1500,
            ""amt"": 21000,
            ""tax"": -3780,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Crush Pebble [1Bag= 25kgs]"",
            ""unit"": ""Number"",
            ""qty"": 6,
            ""rate"": 800,
            ""amt"": 4800,
            ""tax"": -240,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""Flower Vase with Flower"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 1250,
            ""amt"": 5000,
            ""tax"": -900,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""FRP Planter [Elewood Cluster of 3 Planter 27\""x20\""x14\""]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 20500,
            ""amt"": 41000,
            ""tax"": -7380,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""FRP Planter [Elewood27 21\""x27\"". Colour :- Camping Gold]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 9850,
            ""amt"": 19700,
            ""tax"": -3546,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""Interior Mater"",
            ""desc"": ""FRP Planter [Pristine 28, 33\""x28\"" Colour:- Crome Gold]"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 14500,
            ""amt"": 29000,
            ""tax"": -5220,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          }
        ],
        ""PO/8/000272/25-26"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 351720,
            ""rate"": 1,
            ""amt"": 351720,
            ""tax"": -63309.6,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 31654.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 31654.8,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000232/25-26"": [
          {
            ""item"": ""XJOBDNG0262"",
            ""desc"": ""Rain water Harvesting.                    RWRP. Supply &"",
            ""unit"": ""Each"",
            ""qty"": 1,
            ""rate"": 375000,
            ""amt"": 375000,
            ""tax"": -67500,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33750,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33750,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000217/25-26"": [
          {
            ""item"": ""CBDMPLB0292"",
            ""desc"": ""Shower Mixer - P/E all plumbing works for toilet block ["",
            ""unit"": ""Number"",
            ""qty"": 15,
            ""rate"": 2925,
            ""amt"": 43875,
            ""tax"": -7897.5,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3948.75,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3948.75,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000209/25-26"": [
          {
            ""item"": ""CBDMPLB0292"",
            ""desc"": ""Shower Mixer - P/E all plumbing works for toilet block ["",
            ""unit"": ""Number"",
            ""qty"": 45,
            ""rate"": 4555,
            ""amt"": 204975,
            ""tax"": -36895.5,
            ""bqty"": 45,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 18447.75,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 18447.75,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000218/25-26"": [
          {
            ""item"": ""XJOBMIS803"",
            ""desc"": ""Miscellaneous work for Low side cable termination as per"",
            ""unit"": ""Number"",
            ""qty"": 721875,
            ""rate"": 1,
            ""amt"": 721875,
            ""tax"": -86625,
            ""bqty"": 577500,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""XJOBMIS803"",
            ""desc"": ""Miscellaneous work for Low side cable termination as per"",
            ""unit"": ""Number"",
            ""qty"": 309375,
            ""rate"": 1,
            ""amt"": 309375,
            ""tax"": -55687.5,
            ""bqty"": 247500,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 71156.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 71156.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000243/25-26"": [
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [H. E. NOC Remarks]"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 25000,
            ""amt"": 25000,
            ""tax"": -4500,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Remarks from M.S. for existing w"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 9000,
            ""amt"": 9000,
            ""tax"": -1620,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Dry fitting permission (Terrace "",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 50000,
            ""amt"": 50000,
            ""tax"": -9000,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Wet fitting P-form for permanent"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 80000,
            ""amt"": 80000,
            ""tax"": -14400,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Capacity of water tank from wate"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 15000,
            ""amt"": 15000,
            ""tax"": -2700,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Certification and Permanent wate"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 75000,
            ""amt"": 75000,
            ""tax"": -13500,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Down take approval from (P&R)]"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 50000,
            ""amt"": 50000,
            ""tax"": -9000,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Road opening permission from A.E"",
            ""unit"": ""Percentage"",
            ""qty"": 1,
            ""rate"": 25000,
            ""amt"": 25000,
            ""tax"": -4500,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          }
        ],
        ""PO/8/000196/25-26"": [
          {
            ""item"": ""SSECWRK005"",
            ""desc"": ""SECURITY GUARD MALE [Being 1+1=2 Nos. Security Guards Wo"",
            ""unit"": ""Monthly"",
            ""qty"": 22,
            ""rate"": 17000,
            ""amt"": 374000,
            ""tax"": -67320,
            ""bqty"": 21.48,
            ""wpRet"": null,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33660,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""19001"",
            ""cdesc"": ""Interim - RCM Input Tax Credit - S""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_SGST"",
            ""unit"": """",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 33660,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""25304"",
            ""cdesc"": ""RCM Payable - SGST""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33660,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""19002"",
            ""cdesc"": ""Interim - RCM Input Tax Credit - C""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_CGST"",
            ""unit"": """",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 33660,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""25305"",
            ""cdesc"": ""RCM Payable - CGST""
          }
        ],
        ""PO/8/000183/25-26"": [
          {
            ""item"": ""XJOBVLV0162"",
            ""desc"": ""Kitchen Sink Mixer [Focus M41 Single lever kitchen mixer"",
            ""unit"": ""Number"",
            ""qty"": 40,
            ""rate"": 12946,
            ""amt"": 517840,
            ""tax"": -93211.2,
            ""bqty"": 40,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 46605.6,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 46605.6,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000154/25-26"": [
          {
            ""item"": ""FURACH0102"",
            ""desc"": ""ACCENT CHAIR"",
            ""unit"": ""Number"",
            ""qty"": 3,
            ""rate"": 31500,
            ""amt"": 94500,
            ""tax"": -17010,
            ""bqty"": 3,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURACH0102"",
            ""desc"": ""ACCENT CHAIR"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 29500,
            ""amt"": 59000,
            ""tax"": -10620,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURBCH0106"",
            ""desc"": ""BUCKET CHAIR"",
            ""unit"": ""Number"",
            ""qty"": 6,
            ""rate"": 29500,
            ""amt"": 177000,
            ""tax"": -31860,
            ""bqty"": 6,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURBEN0104"",
            ""desc"": ""BENCH"",
            ""unit"": ""Number"",
            ""qty"": 5,
            ""rate"": 37500,
            ""amt"": 187500,
            ""tax"": -33750,
            ""bqty"": 5,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURCFT0108"",
            ""desc"": ""COFEE TABLE"",
            ""unit"": ""Number"",
            ""qty"": 3,
            ""rate"": 35000,
            ""amt"": 105000,
            ""tax"": -18900,
            ""bqty"": 3,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURCFT0108"",
            ""desc"": ""COFEE TABLE"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 30000,
            ""amt"": 120000,
            ""tax"": -21600,
            ""bqty"": 4,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURCNT0107"",
            ""desc"": ""CENTER TABLE TALL"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 28500,
            ""amt"": 57000,
            ""tax"": -10260,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""FURCNT0107"",
            ""desc"": ""CENTER TABLE TALL"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 29500,
            ""amt"": 29500,
            ""tax"": -5310,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          }
        ],
        ""PO/8/000155/25-26"": [
          {
            ""item"": ""FURACH0102"",
            ""desc"": ""ACCENT CHAIR"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 28500,
            ""amt"": 57000,
            ""tax"": -10260,
            ""bqty"": 2,
            ""wpRet"": null,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5130,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5130,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000077/25-26"": [
          {
            ""item"": ""XJOBCPF009"",
            ""desc"": ""DIVERTOR CONCEALED PART-HG VERNIS BASIS SET FOR SINGLE L"",
            ""unit"": ""Number"",
            ""qty"": 17,
            ""rate"": 2925,
            ""amt"": 49725,
            ""tax"": -8950.5,
            ""bqty"": 17,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4475.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4475.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000062/25-26"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 62212,
            ""rate"": 1,
            ""amt"": 62212,
            ""tax"": -11198.16,
            ""bqty"": 57152,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5599.08,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5599.08,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000059/25-26"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 84050,
            ""rate"": 1,
            ""amt"": 84050,
            ""tax"": -15129,
            ""bqty"": 84050,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 7564.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 7564.5,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000037/25-26"": [
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""On acceptance of Proposal"",
            ""unit"": ""Percentage"",
            ""qty"": 5,
            ""rate"": 41909.85,
            ""amt"": 209549.25,
            ""tax"": -37718.86,
            ""bqty"": 5,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""Preparation and Submission of Municipal drawing"",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 41909.85,
            ""amt"": 419098.5,
            ""tax"": -75437.74,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""Approval of MC for Concession"",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 41909.85,
            ""amt"": 838197,
            ""tax"": -150875.46,
            ""bqty"": 20,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""On obtaining IOD for the building"",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 41909.85,
            ""amt"": 419098.5,
            ""tax"": -75437.74,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""On obtaining C.C. (Plinth) for the building"",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 41909.85,
            ""amt"": 419098.5,
            ""tax"": -75437.74,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""On obtaining building Amendment approval"",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 41909.85,
            ""amt"": 628647.75,
            ""tax"": -113156.6,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""On obtaining full C.C. for the building"",
            ""unit"": ""Percentage"",
            ""qty"": 20,
            ""rate"": 41909.85,
            ""amt"": 838197,
            ""tax"": -150875.46,
            ""bqty"": 20,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER021"",
            ""desc"": ""Full Occupation of the Buildings"",
            ""unit"": ""Percentage"",
            ""qty"": 5,
            ""rate"": 41909.85,
            ""amt"": 209549.25,
            ""tax"": -37718.86,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          }
        ],
        ""PO/8/000399/24-25"": [
          {
            ""item"": ""XCONSER024"",
            ""desc"": ""10 high resolution assignment"",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 25000,
            ""amt"": 250000,
            ""tax"": -45000,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72011"",
            ""cdesc"": ""Other D&D Consultants""
          },
          {
            ""item"": ""IGST"",
            ""desc"": ""IGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 45000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18913"",
            ""cdesc"": ""Input Tax Credit - IGST""
          }
        ],
        ""PO/8/000384/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BASIN - Round Console Table Top_x000D_ Washbasin (490W x"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 3120,
            ""amt"": 87360,
            ""tax"": -15724.8,
            ""bqty"": 24,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WASTE COUPLING - - Pop up waste for basin_x000D_ without"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1260,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size with 250mm & 190mm L"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1203,
            ""amt"": 33684,
            ""tax"": -6063.12,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - REI Wall Hung Toilet - CW580RMUNW1 - As per approve"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 5130,
            ""amt"": 143640,
            ""tax"": -25855.2,
            ""bqty"": 27,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Regu"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 2490,
            ""amt"": 69720,
            ""tax"": -12549.6,
            ""bqty"": 16,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33271.56,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33271.56,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000385/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BASIN - Round Console Table Top_x000D_ Washbasin (490W x"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 3120,
            ""amt"": 87360,
            ""tax"": -15724.8,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WASTE COUPLING- Pop up waste for basin_x000D_ without ov"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1260,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size with 250mm & 190mm L"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1203,
            ""amt"": 33684,
            ""tax"": -6063.12,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - REI Wall Hung Toilet CW580RMUNW1 - As per approved "",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 5130,
            ""amt"": 143640,
            ""tax"": -25855.2,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEAT COVER - Duroplast Seat and Cover w/ soft close (Reg"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 2490,
            ""amt"": 69720,
            ""tax"": -12549.6,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33271.56,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 33271.56,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000386/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BASIN - Console Washbasin (500W X 460D x 70H mm) L5615CE"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 11250,
            ""amt"": 315000,
            ""tax"": -56700,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WASTE COUPLING - Pop up waste for basin_x000D_ without o"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1260,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size with 250mm & 190mm L"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1203,
            ""amt"": 33684,
            ""tax"": -6063.12,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - REI Wall Hung Toilet - CW580RMUNW1 as per approved "",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 5130,
            ""amt"": 143640,
            ""tax"": -25855.2,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Regu"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 2490,
            ""amt"": 69720,
            ""tax"": -12549.6,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 53759.16,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 53759.16,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000372/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Regu"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 2490,
            ""amt"": 69720,
            ""tax"": -12549.6,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - REI Wall Hung Toilet - As per approved - CW580RMUNW"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 5130,
            ""amt"": 143640,
            ""tax"": -25855.2,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size with 250mm & 190mm L"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1203,
            ""amt"": 33684,
            ""tax"": -6063.12,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WASTE COUPLING - Pop up waste for basin_x000D_ without o"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1260,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BASIN - Console Washbasin (500W x 450D x 70H mm) - L710C"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 6810,
            ""amt"": 190680,
            ""tax"": -34322.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 42570.36,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 42570.36,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000363/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""FLUSH PLATE - Geberit Alpha 35 actuator plate"",
            ""unit"": ""Number"",
            ""qty"": 140,
            ""rate"": 920,
            ""amt"": 128800,
            ""tax"": -23184,
            ""bqty"": 140,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""FLUSH TANK - Geberit Alpha Kambifix Cistern 8.5cm"",
            ""unit"": ""Number"",
            ""qty"": 140,
            ""rate"": 3415,
            ""amt"": 478100,
            ""tax"": -86058,
            ""bqty"": 140,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 54621,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 54621,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000365/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - TC394CVK#W/TC281SJ  - As per approved only -"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 3990,
            ""amt"": 111720,
            ""tax"": -20109.6,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - AP Wall Hung Toilet - CW822M#NW1 - As per approved "",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 5580,
            ""amt"": 156240,
            ""tax"": -28123.2,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BOTTLE TRAP - Bottle Trap 32mm Size_x000D_ with 250mm & "",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1203,
            ""amt"": 33684,
            ""tax"": -6063.12,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WASTE COUPLING - Pop up waste for basin_x000D_ without o"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1260,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""Basin - Console Washbasin (500W x 450D x 70H mm) - L710C"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 6810,
            ""amt"": 190680,
            ""tax"": -34322.4,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 47484.36,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 47484.36,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000360/24-25"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""HG Vivenis 80 pillar tap w/o_x000D_ waste chrome Better "",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 7875,
            ""amt"": 220500,
            ""tax"": -39690,
            ""bqty"": 28,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""HG angle valve E DN15xDN15 - ANGLE VALVE- 13927000 - As "",
            ""unit"": ""Number"",
            ""qty"": 56,
            ""rate"": 315,
            ""amt"": 17640,
            ""tax"": -3175.2,
            ""bqty"": 56,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""HEALTH FAUCET & ACCESSORIES - Bidette S_x000D_ pre-hose1"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 1730,
            ""amt"": 48440,
            ""tax"": -8719.2,
            ""bqty"": 23,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""KITCHEN MIXERS - HG Logis M31 KM 2601 chr._x000D_ 718350"",
            ""unit"": ""Number"",
            ""qty"": 28,
            ""rate"": 8530,
            ""amt"": 238840,
            ""tax"": -42991.2,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""ANGLE VALVE ( Sink ,WM-2, Aqua-1) - 13927000 - as per ap"",
            ""unit"": ""Number"",
            ""qty"": 112,
            ""rate"": 315,
            ""amt"": 35280,
            ""tax"": -6350.4,
            ""bqty"": 112,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 50463,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 50463,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000353/24-25"": [
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""RUN CONNECT 300 METEOR BLACK - RUN CONNECT 300 METEOR BL"",
            ""unit"": ""Number"",
            ""qty"": 4,
            ""rate"": 493104.15,
            ""amt"": 1972416.6,
            ""tax"": -355034.98,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""SYNCHRO CONNECT P 300 METEOR BLACK - SYNCHRO CONNECT P 3"",
            ""unit"": ""Number"",
            ""qty"": 2,
            ""rate"": 427269.15,
            ""amt"": 854538.3,
            ""tax"": -153816.9,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""RECLINE CONNECT P 300 METEOR BLACK - RECLINE CONNECT P 3"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 261200.48,
            ""amt"": 261200.48,
            ""tax"": -47016.08,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""GROUP CYCLE RIDE - GROUP CYCLE RIDE ** Size: Standard; F"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 160801.87,
            ""amt"": 160801.87,
            ""tax"": -28944.34,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""SKILLROW 7\"" - SKILLROW 7\"" ** Console: 7\""; User Connectiv"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 261364.95,
            ""amt"": 261364.95,
            ""tax"": -47045.7,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""CHEST PRESS 700 METEOR BLACK - CHEST PRESS 700 METEOR BL"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 314362.36,
            ""amt"": 314362.36,
            ""tax"": -56585.22,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""SHOULDER PRESS 700 METEOR BLACK - SHOULDER PRESS 700 MET"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 314362.36,
            ""amt"": 314362.36,
            ""tax"": -56585.22,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          },
          {
            ""item"": ""XJOBMIS256"",
            ""desc"": ""LAT MACHINE 700 METEOR BLACK - LAT MACHINE 700 METEOR BL"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 210672,
            ""amt"": 210672,
            ""tax"": -37920.96,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70011"",
            ""cdesc"": ""Other Materials-consumption""
          }
        ],
        ""PO/8/000347/24-25"": [
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""Advance"",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 6597.5,
            ""amt"": 98962.5,
            ""tax"": -17813.26,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""Concept finalization"",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 6597.5,
            ""amt"": 98962.5,
            ""tax"": -17813.26,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""Issue of working drawings (GFC)"",
            ""unit"": ""Percentage"",
            ""qty"": 30,
            ""rate"": 6597.5,
            ""amt"": 197925,
            ""tax"": -35626.5,
            ""bqty"": 30,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""As per work progress on site"",
            ""unit"": ""Percentage"",
            ""qty"": 40,
            ""rate"": 6597.5,
            ""amt"": 263900,
            ""tax"": -47502,
            ""bqty"": 40,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 59377.51,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 59377.51,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000442/24-25"": [
          {
            ""item"": ""XJOBMIS832"",
            ""desc"": ""Miscellaneous Work for Air  Conditioner work as per anne"",
            ""unit"": ""Number"",
            ""qty"": 1807180,
            ""rate"": 1,
            ""amt"": 1807180,
            ""tax"": -325292.4,
            ""bqty"": 1028030,
            ""wpRet"": null,
            ""code"": ""70428"",
            ""cdesc"": ""Mechanical HVAC""
          },
          {
            ""item"": ""XJOBMIS832"",
            ""desc"": ""Miscellaneous Work for Air  Conditioner work as per anne"",
            ""unit"": ""Number"",
            ""qty"": 54400,
            ""rate"": 1,
            ""amt"": 54400,
            ""tax"": -9792,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70428"",
            ""cdesc"": ""Mechanical HVAC""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 167542.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 167542.2,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000443/24-25"": [
          {
            ""item"": ""XJOBMIS829"",
            ""desc"": ""Miscellaneous Work for HVAC System for VRV system, ducta"",
            ""unit"": ""Number"",
            ""qty"": 1368485,
            ""rate"": 1,
            ""amt"": 1368485,
            ""tax"": -246327.3,
            ""bqty"": 620718,
            ""wpRet"": null,
            ""code"": ""70428"",
            ""cdesc"": ""Mechanical HVAC""
          },
          {
            ""item"": ""XJOBMIS829"",
            ""desc"": ""Miscellaneous Work for HVAC System for VRV system, ducta"",
            ""unit"": ""Number"",
            ""qty"": 92500,
            ""rate"": 1,
            ""amt"": 92500,
            ""tax"": -16650,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70428"",
            ""cdesc"": ""Mechanical HVAC""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 131488.65,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 131488.65,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000309/24-25"": [
          {
            ""item"": ""XJOBMIS915"",
            ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
            ""unit"": ""Lumpsum"",
            ""qty"": 182716,
            ""rate"": 1,
            ""amt"": 182716,
            ""tax"": -32888.88,
            ""bqty"": 162856,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 16444.44,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 16444.44,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000310/24-25"": [
          {
            ""item"": ""XJOBMIS915"",
            ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
            ""unit"": ""Lumpsum"",
            ""qty"": 53477,
            ""rate"": 1,
            ""amt"": 53477,
            ""tax"": -9625.86,
            ""bqty"": 53477,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4812.93,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4812.93,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000290/24-25"": [
          {
            ""item"": ""XJOBMIS828"",
            ""desc"": ""Miscellaneous Work for Fire Fighting Sytem & Accessories"",
            ""unit"": ""Number"",
            ""qty"": 12627285,
            ""rate"": 1,
            ""amt"": 12627285,
            ""tax"": -2272911.3,
            ""bqty"": 10502574.7,
            ""wpRet"": null,
            ""code"": ""70432"",
            ""cdesc"": ""Fire Fighting""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1136455.65,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1136455.65,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000288/24-25"": [
          {
            ""item"": ""CBDMFLR002"",
            ""desc"": ""SUNNY BEIGE COLOUR  - SLATE SUNNYE 052056-1200*600 MM"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 26189,
            ""rate"": 111.75,
            ""amt"": 2926620.75,
            ""tax"": -526791.74,
            ""bqty"": 47817.71,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CBDMFLR002"",
            ""desc"": ""WHITE COLOUR  - SLATE H001 -1200*600"",
            ""unit"": ""Sq. Ft"",
            ""qty"": 21630,
            ""rate"": 111.75,
            ""amt"": 2417152.5,
            ""tax"": -435087.46,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""12718"",
            ""cdesc"": ""Inventory Material at Site""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 480939.6,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 480939.6,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000259/24-25"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Installation Testing and Commissi"",
            ""unit"": ""Lumpsum"",
            ""qty"": 2721600,
            ""rate"": 1,
            ""amt"": 2721600,
            ""tax"": -489888,
            ""bqty"": 2633020,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 244944,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 244944,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000266/24-25"": [
          {
            ""item"": ""XJOBMIS837"",
            ""desc"": ""Miscellaneouswork for  Electrical work of Flat, Common a"",
            ""unit"": ""Lumpsum"",
            ""qty"": 23906292,
            ""rate"": 1,
            ""amt"": 23906292,
            ""tax"": -4303132.56,
            ""bqty"": 12817397.5,
            ""wpRet"": null,
            ""code"": ""70426"",
            ""cdesc"": ""Electrical Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 2151566.28,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 2151566.28,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000249/24-25"": [
          {
            ""item"": ""XJOBMIS915"",
            ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
            ""unit"": ""Lumpsum"",
            ""qty"": 12562013.89,
            ""rate"": 1,
            ""amt"": 12562013.89,
            ""tax"": -2261162.5,
            ""bqty"": 9144912,
            ""wpRet"": null,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1130581.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1130581.25,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000247/24-25"": [
          {
            ""item"": ""XJOBLFT004"",
            ""desc"": ""Supply, Installation, Testing & Commissioning of Lift wi"",
            ""unit"": ""Number"",
            ""qty"": 3,
            ""rate"": 2550000,
            ""amt"": 7650000,
            ""tax"": -1377000,
            ""bqty"": 3.01,
            ""wpRet"": null,
            ""code"": ""70430"",
            ""cdesc"": ""Mechanical Lifts & Escalators""
          },
          {
            ""item"": ""XJOBLFT004"",
            ""desc"": ""Supply, Installation, Testing & Commissioning of Lift wi"",
            ""unit"": ""Number"",
            ""qty"": 288983,
            ""rate"": 1,
            ""amt"": 288983,
            ""tax"": -52016.94,
            ""bqty"": 255000,
            ""wpRet"": null,
            ""code"": ""70430"",
            ""cdesc"": ""Mechanical Lifts & Escalators""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 714508.47,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 714508.47,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000534/23-24"": [
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""Advance"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 300000,
            ""amt"": 300000,
            ""tax"": -54000,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""On every month from March 2024 for 18 Months"",
            ""unit"": ""Number"",
            ""qty"": 18,
            ""rate"": 100000,
            ""amt"": 1800000,
            ""tax"": -324000,
            ""bqty"": 18,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 189000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 189000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000323/23-24"": [
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""Advance"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 360000,
            ""amt"": 360000,
            ""tax"": -64800,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""1st interim after approval of Camera angle or Modeling"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 360000,
            ""amt"": 360000,
            ""tax"": -64800,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""2nd Interim After Delivering all the renders or approval"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 360000,
            ""amt"": 360000,
            ""tax"": -64800,
            ""bqty"": 1.46,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""Final payment after delivering the movie."",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 120000,
            ""amt"": 120000,
            ""tax"": -21600,
            ""bqty"": 0.54,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""Advance"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 165000,
            ""amt"": 165000,
            ""tax"": -29700,
            ""bqty"": 1,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""1st interim, after approval of Camera angle or Modeling"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 165000,
            ""amt"": 165000,
            ""tax"": -29700,
            ""bqty"": 0.67,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""2nd Interim, After Delivering all the renders or approva"",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 165000,
            ""amt"": 165000,
            ""tax"": -29700,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          },
          {
            ""item"": ""XCONSER058"",
            ""desc"": ""Final payment after delivering the movie."",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 55000,
            ""amt"": 55000,
            ""tax"": -9900,
            ""bqty"": 3,
            ""wpRet"": null,
            ""code"": ""72007"",
            ""cdesc"": ""Professional Fees (Projects)""
          }
        ],
        ""PO/8/000141/23-24"": [
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""10% On Approval of Concept."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 32968.96,
            ""amt"": 329689.6,
            ""tax"": -59344.12,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""10% On Schematic Design."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 32968.96,
            ""amt"": 329689.6,
            ""tax"": -59344.12,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""10% On Drawings Submission to BMC."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 32968.96,
            ""amt"": 329689.6,
            ""tax"": -59344.12,
            ""bqty"": 10,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""5% On Marketing materials as required by the clients."",
            ""unit"": ""Percentage"",
            ""qty"": 5,
            ""rate"": 32968.96,
            ""amt"": 164844.8,
            ""tax"": -29672.06,
            ""bqty"": 5,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""10% On Design Development."",
            ""unit"": ""Percentage"",
            ""qty"": 10,
            ""rate"": 32968.96,
            ""amt"": 329689.6,
            ""tax"": -59344.12,
            ""bqty"": 32.83,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""15% On GFC including finishing."",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 32968.96,
            ""amt"": 494534.4,
            ""tax"": -89016.2,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""35% During stages of Construction at Site."",
            ""unit"": ""Percentage"",
            ""qty"": 35,
            ""rate"": 32968.96,
            ""amt"": 1153913.6,
            ""tax"": -207704.44,
            ""bqty"": 12.17,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""5% On Project Completion & Handover."",
            ""unit"": ""Percentage"",
            ""qty"": 5,
            ""rate"": 32968.96,
            ""amt"": 164844.8,
            ""tax"": -29672.06,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          }
        ],
        ""PO/8/000162/23-24"": [
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""15% As Advance."",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 18771.9,
            ""amt"": 281578.5,
            ""tax"": -50684.14,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""15% On Concept Finalization."",
            ""unit"": ""Percentage"",
            ""qty"": 15,
            ""rate"": 18771.9,
            ""amt"": 281578.5,
            ""tax"": -50684.14,
            ""bqty"": 15,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""30% On Issue of working drawings (GFC)."",
            ""unit"": ""Percentage"",
            ""qty"": 30,
            ""rate"": 18771.9,
            ""amt"": 563157,
            ""tax"": -101368.26,
            ""bqty"": 30,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""40% As per work progress on Site."",
            ""unit"": ""Percentage"",
            ""qty"": 40,
            ""rate"": 18771.9,
            ""amt"": 750876,
            ""tax"": -135157.68,
            ""bqty"": 15.74,
            ""wpRet"": null,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 168947.11,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 168947.11,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""PO/8/000202/23-24"": [
          {
            ""item"": ""XCONSER060"",
            ""desc"": ""Parking Layout Planning Remarks & Certification."",
            ""unit"": ""Number"",
            ""qty"": 1,
            ""rate"": 60000,
            ""amt"": 60000,
            ""tax"": -10800,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""72012"",
            ""cdesc"": ""Other Construction related Consult""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5400,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""unit"": """",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5400,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ]
      },
      ""asnHeaders"": {
        ""PO/8/000007/26-27"": [
          {
            ""asnNo"": ""ASN3256"",
            ""challan"": ""LIDCO/88/26-27"",
            ""date"": ""22-04-2026"",
            ""appDate"": ""20-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 9,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-3428"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000013/26-27"": [
          {
            ""asnNo"": ""ASN3473"",
            ""challan"": ""1714"",
            ""date"": ""29-04-2026"",
            ""appDate"": ""06-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-4042"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000080/25-26"": [
          {
            ""asnNo"": ""ASN3512"",
            ""challan"": ""ASN 5"",
            ""date"": ""05-06-2026"",
            ""appDate"": ""09-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 40,
            ""billQty"": 96,
            ""draftInv"": ""DRFT-3748"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000113/25-26"": [
          {
            ""asnNo"": ""ASN3641"",
            ""challan"": ""11/2026-27"",
            ""date"": ""16-06-2026"",
            ""appDate"": ""19-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 45,
            ""billQty"": 80,
            ""draftInv"": ""DRFT-3962"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 25
          }
        ],
        ""PO/8/000215/24-25"": [
          {
            ""asnNo"": ""ASN2597"",
            ""challan"": ""57/2025-26"",
            ""date"": ""31-03-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 22.87,
            ""apprQty"": 22.87,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2579"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 34
          },
          {
            ""asnNo"": ""ASN3643"",
            ""challan"": ""12/2026-27"",
            ""date"": ""16-06-2026"",
            ""appDate"": ""19-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 40,
            ""apprQty"": 40,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-3963"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 17
          },
          {
            ""asnNo"": ""ASN3175"",
            ""challan"": ""01/2026-27"",
            ""date"": ""13-05-2026"",
            ""appDate"": ""16-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 11.7,
            ""billQty"": 7985.48,
            ""draftInv"": ""DRFT-3331"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 12
          }
        ],
        ""PO/8/000249/24-25"": [
          {
            ""asnNo"": ""ASN3567"",
            ""challan"": ""SB/2026-2027/18"",
            ""date"": ""10-06-2026"",
            ""appDate"": ""13-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 343669,
            ""billQty"": 8801243,
            ""draftInv"": ""DRFT-3905"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000266/24-25"": [
          {
            ""asnNo"": ""ASN2703"",
            ""challan"": ""RE/PI/01/26-27"",
            ""date"": ""01-04-2026"",
            ""appDate"": ""14-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 3186257,
            ""apprQty"": 3186257,
            ""billQty"": 9631140.5,
            ""draftInv"": ""DRFT-2747"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000340/25-26"": [
          {
            ""asnNo"": ""ASN3156"",
            ""challan"": ""RA-2nd"",
            ""date"": ""12-05-2026"",
            ""appDate"": ""14-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 342.08,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-3309"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000363/25-26"": [
          {
            ""asnNo"": ""ASN3002"",
            ""challan"": ""ASN RA -3"",
            ""date"": ""29-04-2026"",
            ""appDate"": ""05-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 3,
            ""apprQty"": 3,
            ""billQty"": 18.5,
            ""draftInv"": ""DRFT-3187"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 2
          },
          {
            ""asnNo"": ""ASN3686"",
            ""challan"": ""FWNG/RRE/26-27/04"",
            ""date"": ""22-06-2026"",
            ""appDate"": ""24-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 1.5,
            ""apprQty"": 1.5,
            ""billQty"": 21.5,
            ""draftInv"": ""DRFT-4036"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 3
          }
        ],
        ""PO/8/000365/24-25"": [
          {
            ""asnNo"": ""ASN2586"",
            ""challan"": ""CHALLAN 20"",
            ""date"": ""03-04-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2666"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000372/24-25"": [
          {
            ""asnNo"": ""ASN3481"",
            ""challan"": ""192.2"",
            ""date"": ""05-05-2026"",
            ""appDate"": ""06-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-4049"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN2584"",
            ""challan"": ""CHALLAN 18"",
            ""date"": ""03-04-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2663"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000377/25-26"": [
          {
            ""asnNo"": ""ASN2801"",
            ""challan"": ""1893, 2011, 2010, "",
            ""date"": ""18-04-2026"",
            ""appDate"": ""18-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-3014"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 3
          },
          {
            ""asnNo"": ""ASN2678"",
            ""challan"": ""1961/1960/2010/201"",
            ""date"": ""11-04-2026"",
            ""appDate"": ""13-04-2026"",
            ""status"": ""Cancelled"",
            ""asnQty"": 2,
            ""apprQty"": 2,
            ""billQty"": 0,
            ""draftInv"": """",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 3
          }
        ],
        ""PO/8/000384/24-25"": [
          {
            ""asnNo"": ""ASN3331"",
            ""challan"": ""94"",
            ""date"": ""29-04-2026"",
            ""appDate"": ""27-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 26,
            ""draftInv"": ""DRFT-4045"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN3484"",
            ""challan"": ""308"",
            ""date"": ""27-05-2026"",
            ""appDate"": ""06-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 24,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-4053"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN2649"",
            ""challan"": ""64"",
            ""date"": ""10-04-2026"",
            ""appDate"": ""10-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 12,
            ""apprQty"": 12,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2677"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN3480"",
            ""challan"": ""192.1"",
            ""date"": ""05-05-2026"",
            ""appDate"": ""06-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 16,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-4048"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN2583"",
            ""challan"": ""CHALLAN 17"",
            ""date"": ""03-04-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2661"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000385/24-25"": [
          {
            ""asnNo"": ""ASN2648"",
            ""challan"": ""63"",
            ""date"": ""10-04-2026"",
            ""appDate"": ""10-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2675"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN2582"",
            ""challan"": ""challan16"",
            ""date"": ""03-04-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2660"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000386/24-25"": [
          {
            ""asnNo"": ""ASN2650"",
            ""challan"": ""34"",
            ""date"": ""10-04-2026"",
            ""appDate"": ""10-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2676"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 2
          },
          {
            ""asnNo"": ""ASN3482"",
            ""challan"": ""192.3"",
            ""date"": ""05-05-2026"",
            ""appDate"": ""06-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 27,
            ""draftInv"": """",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          },
          {
            ""asnNo"": ""ASN2585"",
            ""challan"": ""CHALLAN 19"",
            ""date"": ""03-04-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2665"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 2
          }
        ],
        ""PO/8/000429/25-26"": [
          {
            ""asnNo"": ""ASN3534"",
            ""challan"": ""09/2026-27"",
            ""date"": ""07-06-2026"",
            ""appDate"": ""10-06-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 12,
            ""apprQty"": 12,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-3823"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 11
          }
        ],
        ""PO/8/000439/25-26"": [
          {
            ""asnNo"": ""ASN2588"",
            ""challan"": ""VFMS/544/25-26"",
            ""date"": ""17-03-2026"",
            ""appDate"": ""07-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 26,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-4087"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 2
          },
          {
            ""asnNo"": ""ASN2564"",
            ""challan"": ""VFMS/542/25-26"",
            ""date"": ""13-03-2026"",
            ""appDate"": ""06-04-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 21,
            ""apprQty"": 21,
            ""billQty"": 0,
            ""draftInv"": ""DRFT-2895"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000443/24-25"": [
          {
            ""asnNo"": ""ASN3068"",
            ""challan"": ""AD/G/231/25-26"",
            ""date"": ""17-03-2026"",
            ""appDate"": ""08-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0.05,
            ""apprQty"": 340609.05,
            ""billQty"": 280109,
            ""draftInv"": ""DRFT-3161"",
            ""responsible"": ""Mahendra Gangan"",
            ""lineCount"": 1
          }
        ],
        ""PO/8/000467/25-26"": [
          {
            ""asnNo"": ""ASN3254"",
            ""challan"": ""LIDCO/66/26-27"",
            ""date"": ""17-04-2026"",
            ""appDate"": ""20-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 205,
            ""billQty"": 2,
            ""draftInv"": ""DRFT-3427"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 2
          },
          {
            ""asnNo"": ""ASN3258"",
            ""challan"": ""LIDCO/140/26-27"",
            ""date"": ""06-05-2026"",
            ""appDate"": ""20-05-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 0,
            ""apprQty"": 78,
            ""billQty"": 202,
            ""draftInv"": ""DRFT-3495"",
            ""responsible"": ""Virendra Mishra"",
            ""lineCount"": 1
          }
        ]
      },
      ""asnLines"": {
        ""ASN3256"": [
          {
            ""docNo"": ""LIDCO/88/26-27"",
            ""draftInv"": ""DRFT-3428"",
            ""vendBill"": ""VendBill-LIDCO/88/26-27"",
            ""asnQty"": 0,
            ""apprQty"": 9,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3473"": [
          {
            ""docNo"": ""VP/25-26/1920"",
            ""draftInv"": ""DRFT-4042"",
            ""vendBill"": ""VendBill-VP/25-26/1920"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3512"": [
          {
            ""docNo"": ""RSE/05/02-26"",
            ""draftInv"": ""DRFT-3748"",
            ""vendBill"": ""VendBill-RSE/05/02-26"",
            ""asnQty"": 0,
            ""apprQty"": 40,
            ""billQty"": 96,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3641"": [
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 0,
            ""apprQty"": 45,
            ""billQty"": 80,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 25,
            ""apprQty"": 25,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 1427.35,
            ""apprQty"": 1427.35,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 11,
            ""apprQty"": 11,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 3.37,
            ""apprQty"": 3.37,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 2,
            ""apprQty"": 2,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 2.45,
            ""apprQty"": 2.45,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 5.08,
            ""apprQty"": 5.08,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 1.65,
            ""apprQty"": 1.65,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 11.92,
            ""apprQty"": 11.92,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 2.54,
            ""apprQty"": 2.54,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 22.71,
            ""apprQty"": 22.71,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 49.21,
            ""apprQty"": 49.21,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 12.52,
            ""apprQty"": 12.52,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 16,
            ""apprQty"": 16,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 299.74,
            ""apprQty"": 299.74,
            ""billQty"": 1450,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 400,
            ""apprQty"": 400,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 75.95,
            ""apprQty"": 75.95,
            ""billQty"": 152.91,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 57.8,
            ""apprQty"": 57.8,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 3.13,
            ""apprQty"": 3.13,
            ""billQty"": 3,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 0.07,
            ""apprQty"": 0.07,
            ""billQty"": 26.7,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 31.1,
            ""apprQty"": 31.1,
            ""billQty"": 135,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 6.75,
            ""apprQty"": 6.75,
            ""billQty"": 195,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""11/2026-27"",
            ""draftInv"": ""DRFT-3962"",
            ""vendBill"": ""VendBill-11/2026-27"",
            ""asnQty"": 1.33,
            ""apprQty"": 1.33,
            ""billQty"": 6,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN2597"": [
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 22.87,
            ""apprQty"": 22.87,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 1888.26,
            ""apprQty"": 1888.26,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 342,
            ""apprQty"": 342,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 1555,
            ""apprQty"": 1555,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 140,
            ""apprQty"": 140,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 64.8,
            ""apprQty"": 64.8,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 329,
            ""apprQty"": 329,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 152.32,
            ""apprQty"": 152.32,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 77,
            ""apprQty"": 77,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 534.69,
            ""apprQty"": 534.69,
            ""billQty"": 2200,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 284.32,
            ""apprQty"": 284.32,
            ""billQty"": 244,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 953.63,
            ""apprQty"": 953.63,
            ""billQty"": 900,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 443.51,
            ""apprQty"": 443.51,
            ""billQty"": 349,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 40,
            ""apprQty"": 40,
            ""billQty"": 1189,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 217,
            ""apprQty"": 217,
            ""billQty"": 868,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 49,
            ""apprQty"": 49,
            ""billQty"": 3000,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 165.44,
            ""apprQty"": 165.44,
            ""billQty"": 473,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 211.7,
            ""apprQty"": 211.7,
            ""billQty"": 1140,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 657.78,
            ""apprQty"": 657.78,
            ""billQty"": 36.78,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 376.44,
            ""apprQty"": 376.44,
            ""billQty"": 1310,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 5199,
            ""apprQty"": 5199,
            ""billQty"": 3499,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 6.4,
            ""apprQty"": 6.4,
            ""billQty"": 44,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 2.79,
            ""apprQty"": 2.79,
            ""billQty"": 52.97,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 1.9,
            ""apprQty"": 1.9,
            ""billQty"": 94.31,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 36,
            ""apprQty"": 36,
            ""billQty"": 195,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 59.12,
            ""apprQty"": 59.12,
            ""billQty"": 334,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 140.48,
            ""apprQty"": 140.48,
            ""billQty"": 7845,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 2.05,
            ""apprQty"": 2.05,
            ""billQty"": 136.8,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 6,
            ""apprQty"": 6,
            ""billQty"": 279,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 4,
            ""apprQty"": 4,
            ""billQty"": 633,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 1.35,
            ""apprQty"": 1.35,
            ""billQty"": 32.5,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 48.86,
            ""apprQty"": 48.86,
            ""billQty"": 331,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 76.49,
            ""apprQty"": 76.49,
            ""billQty"": 56,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""62/2025-26"",
            ""draftInv"": ""DRFT-2579"",
            ""vendBill"": ""VendBill-62/2025-26"",
            ""asnQty"": 22.03,
            ""apprQty"": 22.03,
            ""billQty"": 332.1,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3175"": [
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 0,
            ""apprQty"": 11.7,
            ""billQty"": 7985.48,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 251.77,
            ""apprQty"": 251.77,
            ""billQty"": 1888.26,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 46,
            ""apprQty"": 46,
            ""billQty"": 342,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 207,
            ""apprQty"": 207,
            ""billQty"": 1555,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 180,
            ""apprQty"": 180,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 18,
            ""apprQty"": 18,
            ""billQty"": 140,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 66,
            ""apprQty"": 66,
            ""billQty"": 329,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 30.46,
            ""apprQty"": 30.46,
            ""billQty"": 152.32,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 1195.98,
            ""apprQty"": 1195.98,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 39.56,
            ""apprQty"": 39.56,
            ""billQty"": 638.44,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 1242,
            ""apprQty"": 1242,
            ""billQty"": 8698,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""01/2026-27"",
            ""draftInv"": ""DRFT-3331"",
            ""vendBill"": ""VendBill-01/2026-27"",
            ""asnQty"": 4.88,
            ""apprQty"": 4.88,
            ""billQty"": 74.68,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3643"": [
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 40,
            ""apprQty"": 40,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 10,
            ""apprQty"": 10,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 600,
            ""apprQty"": 600,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 158,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 488.82,
            ""apprQty"": 488.82,
            ""billQty"": 1195.98,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 1998.36,
            ""apprQty"": 1998.36,
            ""billQty"": 1853.63,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 9,
            ""apprQty"": 9,
            ""billQty"": 1229,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 112,
            ""apprQty"": 112,
            ""billQty"": 678,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 2121.34,
            ""apprQty"": 2121.34,
            ""billQty"": 9940,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 0,
            ""apprQty"": 5.33,
            ""billQty"": 79.56,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 48.72,
            ""apprQty"": 48.72,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 377.65,
            ""apprQty"": 377.65,
            ""billQty"": 2140.03,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 68.57,
            ""apprQty"": 68.57,
            ""billQty"": 388,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 311.5,
            ""apprQty"": 311.5,
            ""billQty"": 1762,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 81,
            ""apprQty"": 81,
            ""billQty"": 180,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 45.76,
            ""apprQty"": 45.76,
            ""billQty"": 395,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""12/2026-27"",
            ""draftInv"": ""DRFT-3963"",
            ""vendBill"": ""VendBill-12/2026-27"",
            ""asnQty"": 20.25,
            ""apprQty"": 20.25,
            ""billQty"": 182.78,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3567"": [
          {
            ""docNo"": ""SB/2026-2027/16"",
            ""draftInv"": ""DRFT-3905"",
            ""vendBill"": ""VendBill-SB/2026-2027/16"",
            ""asnQty"": 0,
            ""apprQty"": 343669,
            ""billQty"": 8801243,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN2703"": [
          {
            ""docNo"": ""RE/008/26-27"",
            ""draftInv"": ""DRFT-2747"",
            ""vendBill"": ""VendBill-RE/008/26-27"",
            ""asnQty"": 3186257,
            ""apprQty"": 3186257,
            ""billQty"": 9631140.5,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3156"": [
          {
            ""docNo"": ""SWS003"",
            ""draftInv"": ""DRFT-3309"",
            ""vendBill"": ""VendBill-SWS003"",
            ""asnQty"": 0,
            ""apprQty"": 342.08,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3002"": [
          {
            ""docNo"": ""FMNG/RRE/26-27/03"",
            ""draftInv"": ""DRFT-3187"",
            ""vendBill"": ""VendBill-FMNG/RRE/26-27/03"",
            ""asnQty"": 3,
            ""apprQty"": 3,
            ""billQty"": 18.5,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""FMNG/RRE/26-27/03"",
            ""draftInv"": ""DRFT-3187"",
            ""vendBill"": ""VendBill-FMNG/RRE/26-27/03"",
            ""asnQty"": 0,
            ""apprQty"": 114,
            ""billQty"": 232,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3686"": [
          {
            ""docNo"": ""FWNG/RRE/26-27/04"",
            ""draftInv"": ""DRFT-4036"",
            ""vendBill"": ""VendBill-FWNG/RRE/26-27/04"",
            ""asnQty"": 1.5,
            ""apprQty"": 1.5,
            ""billQty"": 21.5,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""FWNG/RRE/26-27/04"",
            ""draftInv"": ""DRFT-4036"",
            ""vendBill"": ""VendBill-FWNG/RRE/26-27/04"",
            ""asnQty"": 10.5,
            ""apprQty"": 10.5,
            ""billQty"": 1,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""FWNG/RRE/26-27/04"",
            ""draftInv"": ""DRFT-4036"",
            ""vendBill"": ""VendBill-FWNG/RRE/26-27/04"",
            ""asnQty"": 0,
            ""apprQty"": 72,
            ""billQty"": 346,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN2586"": [
          {
            ""docNo"": ""VP/26-27/09"",
            ""draftInv"": ""DRFT-2666"",
            ""vendBill"": ""VendBill-VP/26-27/09"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2584"": [
          {
            ""docNo"": ""VP/26-27/07"",
            ""draftInv"": ""DRFT-2663"",
            ""vendBill"": ""VendBill-VP/26-27/07"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3481"": [
          {
            ""docNo"": ""VP/26-27/287"",
            ""draftInv"": ""DRFT-4049"",
            ""vendBill"": ""VendBill-VP/26-27/287"",
            ""asnQty"": 0,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2678"": [
          {
            ""docNo"": """",
            ""draftInv"": """",
            ""vendBill"": """",
            ""asnQty"": 2,
            ""apprQty"": 2,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": """",
            ""draftInv"": """",
            ""vendBill"": """",
            ""asnQty"": 4,
            ""apprQty"": 4,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": """",
            ""draftInv"": """",
            ""vendBill"": """",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2801"": [
          {
            ""docNo"": ""VES/2025-26/844"",
            ""draftInv"": ""DRFT-3014"",
            ""vendBill"": ""VendBill-VES/2025-26/844"",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": ""VES/2025-26/844"",
            ""draftInv"": ""DRFT-3014"",
            ""vendBill"": ""VendBill-VES/2025-26/844"",
            ""asnQty"": 4,
            ""apprQty"": 4,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": ""VES/2025-26/844"",
            ""draftInv"": ""DRFT-3014"",
            ""vendBill"": ""VendBill-VES/2025-26/844"",
            ""asnQty"": 2,
            ""apprQty"": 2,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2583"": [
          {
            ""docNo"": ""VP/26-27/06"",
            ""draftInv"": ""DRFT-2661"",
            ""vendBill"": ""VendBill-VP/26-27/06"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2649"": [
          {
            ""docNo"": ""VP/26-27/40"",
            ""draftInv"": ""DRFT-2677"",
            ""vendBill"": ""VendBill-VP/26-27/40"",
            ""asnQty"": 12,
            ""apprQty"": 12,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3331"": [
          {
            ""docNo"": ""VP/26-27/122"",
            ""draftInv"": ""DRFT-4045"",
            ""vendBill"": ""VendBill-VP/26-27/122"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 26,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3480"": [
          {
            ""docNo"": ""VP/26-27/286"",
            ""draftInv"": ""DRFT-4048"",
            ""vendBill"": ""VendBill-VP/26-27/286"",
            ""asnQty"": 0,
            ""apprQty"": 16,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3484"": [
          {
            ""docNo"": ""VP/26-27/340"",
            ""draftInv"": ""DRFT-4053"",
            ""vendBill"": ""VendBill-VP/26-27/340"",
            ""asnQty"": 0,
            ""apprQty"": 24,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2582"": [
          {
            ""docNo"": ""VP/26-27/05"",
            ""draftInv"": ""DRFT-2660"",
            ""vendBill"": ""VendBill-VP/26-27/05"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2648"": [
          {
            ""docNo"": ""VP/26-27/38"",
            ""draftInv"": ""DRFT-2675"",
            ""vendBill"": ""VendBill-VP/26-27/38"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2585"": [
          {
            ""docNo"": ""VP/26-27/08"",
            ""draftInv"": ""DRFT-2665"",
            ""vendBill"": ""VendBill-VP/26-27/08"",
            ""asnQty"": 28,
            ""apprQty"": 28,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": ""VP/26-27/08"",
            ""draftInv"": ""DRFT-2665"",
            ""vendBill"": ""VendBill-VP/26-27/08"",
            ""asnQty"": 7,
            ""apprQty"": 7,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN2650"": [
          {
            ""docNo"": ""VP/26-27/39"",
            ""draftInv"": ""DRFT-2676"",
            ""vendBill"": ""VendBill-VP/26-27/288,Vend"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": ""VP/26-27/288"",
            ""draftInv"": ""DRFT-4050"",
            ""vendBill"": ""VendBill-VP/26-27/288,Vend"",
            ""asnQty"": 0,
            ""apprQty"": 1,
            ""billQty"": 0,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3482"": [
          {
            ""docNo"": """",
            ""draftInv"": """",
            ""vendBill"": """",
            ""asnQty"": 1,
            ""apprQty"": 1,
            ""billQty"": 27,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3534"": [
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 12,
            ""apprQty"": 12,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 167.12,
            ""apprQty"": 167.12,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 3.82,
            ""apprQty"": 3.82,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 0,
            ""apprQty"": 195,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 46.73,
            ""apprQty"": 46.73,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 130.95,
            ""apprQty"": 130.95,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 25.73,
            ""apprQty"": 25.73,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 114.05,
            ""apprQty"": 114.05,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 9,
            ""apprQty"": 9,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 6.98,
            ""apprQty"": 6.98,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""09/2026-27"",
            ""draftInv"": ""DRFT-3823"",
            ""vendBill"": ""VendBill-09/2026-27"",
            ""asnQty"": 3.22,
            ""apprQty"": 3.22,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN2564"": [
          {
            ""docNo"": ""VFMS/542/25-26"",
            ""draftInv"": ""DRFT-2895"",
            ""vendBill"": ""VendBill-VFMS/542/25-26"",
            ""asnQty"": 21,
            ""apprQty"": 21,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN2588"": [
          {
            ""docNo"": ""VFMS/544/25-26"",
            ""draftInv"": ""DRFT-4087"",
            ""vendBill"": ""VendBill-VFMS/544/25-26,Ve"",
            ""asnQty"": 0,
            ""apprQty"": 26,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          },
          {
            ""docNo"": ""VFMS/544/25-26"",
            ""draftInv"": ""DRFT-2898"",
            ""vendBill"": ""VendBill-VFMS/544/25-26,Ve"",
            ""asnQty"": 0,
            ""apprQty"": 26,
            ""billQty"": 0,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3068"": [
          {
            ""docNo"": ""AD/G/231/25-26"",
            ""draftInv"": ""DRFT-3161"",
            ""vendBill"": ""VendBill-AD/G/231/25-26"",
            ""asnQty"": 0.05,
            ""apprQty"": 340609.05,
            ""billQty"": 280109,
            ""responsible"": ""Mahendra Gangan""
          }
        ],
        ""ASN3254"": [
          {
            ""docNo"": ""LIDCO/66/26-27"",
            ""draftInv"": ""DRFT-3427"",
            ""vendBill"": ""VendBill-LIDCO/66/26-27"",
            ""asnQty"": 0,
            ""apprQty"": 205,
            ""billQty"": 2,
            ""responsible"": ""Virendra Mishra""
          },
          {
            ""docNo"": ""LIDCO/66/26-27"",
            ""draftInv"": ""DRFT-3427"",
            ""vendBill"": ""VendBill-LIDCO/66/26-27"",
            ""asnQty"": 200,
            ""apprQty"": 200,
            ""billQty"": 2,
            ""responsible"": ""Virendra Mishra""
          }
        ],
        ""ASN3258"": [
          {
            ""docNo"": ""LIDCO/140/26-27"",
            ""draftInv"": ""DRFT-3495"",
            ""vendBill"": ""VendBill-LIDCO/140/26-27"",
            ""asnQty"": 0,
            ""apprQty"": 78,
            ""billQty"": 202,
            ""responsible"": ""Virendra Mishra""
          }
        ]
      },
      ""invLines"": {
        ""AP/INV/8/000862/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -0.12,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBLBR022"",
            ""desc"": ""Lumpsum for Construction Contracts (Sq.Ft.)"",
            ""qty"": 26,
            ""rate"": 653.85,
            ""amt"": 17000.1,
            ""tax"": -3230.02,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1530.01,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1530.01,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 170,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000825/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.5,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Department La"",
            ""qty"": 72,
            ""rate"": 650,
            ""amt"": 46800,
            ""tax"": -8892,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Supply of Bre"",
            ""qty"": 1.5,
            ""rate"": 1300,
            ""amt"": 1950,
            ""tax"": -370.5,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""XJOBLBR005"",
            ""desc"": ""Supply of labour for all types of works [Supply of Sup"",
            ""qty"": 10.5,
            ""rate"": 750,
            ""amt"": 7875,
            ""tax"": -1496.25,
            ""code"": ""70443"",
            ""cdesc"": ""Lumpsum for Construction Contracts""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5096.25,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5096.25,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 566.25,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000830/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.4,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - TC394CVK#W/TC281SJ  - As per approved only"",
            ""qty"": -28,
            ""rate"": 3990,
            ""amt"": -111720,
            ""tax"": -20109.6,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10054.8,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 10054.8,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000831/26-27"": [
          {
            ""item"": ""MEP : PLSWBS01"",
            ""desc"": ""Wash Basin[Kolher Make Forefront Vessel w/faucet deck "",
            ""qty"": -1,
            ""rate"": 8080,
            ""amt"": -8080,
            ""tax"": -1454.4,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 727.2,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 727.2,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000834/26-27"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""WC - REI Wall Hung Toilet - CW580RMUNW1 - As per appro"",
            ""qty"": -1,
            ""rate"": 5130,
            ""amt"": -5130,
            ""tax"": -923.4,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 461.7,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 461.7,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000837/26-27"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Re"",
            ""qty"": -16,
            ""rate"": 2490,
            ""amt"": -39840,
            ""tax"": -7171.2,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3585.6,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3585.6,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000838/26-27"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Re"",
            ""qty"": -28,
            ""rate"": 2490,
            ""amt"": -69720,
            ""tax"": -12549.6,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6274.8,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6274.8,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000839/26-27"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""SEATCOVER - Duroplast Seat and Cover w/ soft close (Re"",
            ""qty"": -1,
            ""rate"": 2490,
            ""amt"": -2490,
            ""tax"": -448.2,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 224.1,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 224.1,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000842/26-27"": [
          {
            ""item"": ""CBDMFIN004"",
            ""desc"": ""BASIN - Round Console Table Top_x000D_ Washbasin (490W"",
            ""qty"": -24,
            ""rate"": 3120,
            ""amt"": -74880,
            ""tax"": -13478.4,
            ""code"": ""26151"",
            ""cdesc"": ""Inventory AP Accrual""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6739.2,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 6739.2,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000869/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -2831,
            ""tax"": 0,
            ""code"": ""26154"",
            ""cdesc"": ""Retention Setup Account""
          }
        ],
        ""AP/INV/8/000807/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.5,
            ""tax"": -0.05,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.5,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""As per work progress on site"",
            ""qty"": 10,
            ""rate"": 6597.5,
            ""amt"": 65975,
            ""tax"": -18473,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5937.75,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 5937.75,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 6597.55,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000808/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.26,
            ""tax"": -0.03,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -0.11,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""30% On Issue of working drawings (GFC)."",
            ""qty"": 10,
            ""rate"": 18771.9,
            ""amt"": 187719,
            ""tax"": -52561.32,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""XCONSER019"",
            ""desc"": ""40% As per work progress on Site."",
            ""qty"": 4.79,
            ""rate"": 18771.9,
            ""amt"": 89912.14,
            ""tax"": -25175.39,
            ""code"": ""72001"",
            ""cdesc"": ""Architect Fees""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 24986.8,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 24986.8,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 27763.14,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000801/26-27"": [
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [H. E. NOC Remarks]"",
            ""qty"": 0,
            ""rate"": 25000,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Remarks from M.S. for existing"",
            ""qty"": 1,
            ""rate"": 9000,
            ""amt"": 9000,
            ""tax"": -2520,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Dry fitting permission (Terrac"",
            ""qty"": 1,
            ""rate"": 50000,
            ""amt"": 50000,
            ""tax"": -14000,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Wet fitting P-form for permane"",
            ""qty"": 1,
            ""rate"": 80000,
            ""amt"": 80000,
            ""tax"": -22400,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Capacity of water tank from wa"",
            ""qty"": 1,
            ""rate"": 15000,
            ""amt"": 15000,
            ""tax"": -4200,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Certification and Permanent wa"",
            ""qty"": 0,
            ""rate"": 75000,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Down take approval from (P&R)]"",
            ""qty"": 0,
            ""rate"": 50000,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Road opening permission from A"",
            ""qty"": 0,
            ""rate"": 25000,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [270 - A Certificate]"",
            ""qty"": 0,
            ""rate"": 25000,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [D.C.C. for  B.P.]"",
            ""qty"": 1,
            ""rate"": 35000,
            ""amt"": 35000,
            ""tax"": -9800,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Smoke test Certificate (For Dr"",
            ""qty"": 1,
            ""rate"": 15000,
            ""amt"": 15000,
            ""tax"": -4200,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          },
          {
            ""item"": ""XCONSER073"",
            ""desc"": ""Liasioning Consultancy [Ponding test Certificate (For "",
            ""qty"": 1,
            ""rate"": 15000,
            ""amt"": 15000,
            ""tax"": -4200,
            ""code"": ""72015"",
            ""cdesc"": ""Liasioning Consultancy""
          }
        ],
        ""AP/INV/8/000784/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.1,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBFLR102"",
            ""desc"": ""Supply & Fixing of marble ,base rate of Marble -280 Rs"",
            ""qty"": 45,
            ""rate"": 6853,
            ""amt"": 308385,
            ""tax"": -61677,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBSKR102"",
            ""desc"": ""Supply & Fixing of Marble skirting"",
            ""qty"": 1.33,
            ""rate"": 8600,
            ""amt"": 11438,
            ""tax"": -2287.6,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR113"",
            ""desc"": ""Supply  & Fixing of marble ,base rate of Marble -315 R"",
            ""qty"": 6.75,
            ""rate"": 9000,
            ""amt"": 60750,
            ""tax"": -12150,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Staircase Tread    Supply & Fixing of marble Tread wit"",
            ""qty"": 31.1,
            ""rate"": 2250,
            ""amt"": 69975,
            ""tax"": -13995,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Brown Mirror bevelled in Grid Pattern-Providing and fi"",
            ""qty"": 0.07,
            ""rate"": 8000,
            ""amt"": 560,
            ""tax"": -112,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS263"",
            ""desc"": ""Trap Door  with HDHMR MDF"",
            ""qty"": 3.13,
            ""rate"": 7136,
            ""amt"": 22335.68,
            ""tax"": -4467.13,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS263"",
            ""desc"": ""Vertical garden with aluminium frame"",
            ""qty"": 1,
            ""rate"": 75000,
            ""amt"": 75000,
            ""tax"": -15000,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Grooves at different finish junction"",
            ""qty"": 57.8,
            ""rate"": 98,
            ""amt"": 5664.4,
            ""tax"": -1132.89,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBMIS239"",
            ""desc"": ""Wall & Ceiling Paint -Providing & Applying of two coat"",
            ""qty"": 75.95,
            ""rate"": 410,
            ""amt"": 31139.5,
            ""tax"": -6227.91,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Paint Running Patta"",
            ""qty"": 400,
            ""rate"": 150,
            ""amt"": 60000,
            ""tax"": -12000,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          },
          {
            ""item"": ""XJOBMIS209"",
            ""desc"": ""Metallic PU on Rafter"",
            ""qty"": 299.74,
            ""rate"": 775,
            ""amt"": 232298.5,
            ""tax"": -46459.71,
            ""code"": ""70422"",
            ""cdesc"": ""Entrance and Typical Lobbies""
          }
        ],
        ""AP/INV/8/000786/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -0.12,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBFAB003"",
            ""desc"": ""Providing & Installing of steel single leaf door shutt"",
            ""qty"": 5.33,
            ""rate"": 10333,
            ""amt"": 55074.89,
            ""tax"": -11014.98,
            ""code"": ""70432"",
            ""cdesc"": ""Fire Fighting""
          },
          {
            ""item"": ""XJOBPAI102"",
            ""desc"": ""Providing & applying of two coats of Premium plastic e"",
            ""qty"": 2121.34,
            ""rate"": 210,
            ""amt"": 445481.4,
            ""tax"": -89096.29,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBFLR104"",
            ""desc"": ""Providing and laying 300 X 300 mm approved flooring an"",
            ""qty"": 112,
            ""rate"": 1237,
            ""amt"": 138544,
            ""tax"": -27708.8,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBMAR116"",
            ""desc"": ""\""Vitrified Tile Dado_x000D_ Providing and fixing of ap"",
            ""qty"": 9,
            ""rate"": 3220,
            ""amt"": 28980,
            ""tax"": -5796,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBPAI105"",
            ""desc"": ""External Textur & Paint (chajja &,Terrace Parafit)- Pr"",
            ""qty"": 1998.36,
            ""rate"": 495,
            ""amt"": 989188.2,
            ""tax"": -197837.64,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS237"",
            ""desc"": ""Miscellaneous items for Work (R. Meter)"",
            ""qty"": 488.82,
            ""rate"": 98,
            ""amt"": 47904.36,
            ""tax"": -9580.87,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBWOD112"",
            ""desc"": ""Providing and fixing superior quality factory made flu"",
            ""qty"": 20.25,
            ""rate"": 8755,
            ""amt"": 177288.75,
            ""tax"": -35457.76,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBWOD112"",
            ""desc"": ""Providing and fixing superior quality factory made flu"",
            ""qty"": 45.76,
            ""rate"": 8265,
            ""amt"": 378206.4,
            ""tax"": -75641.29,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBMIS255"",
            ""desc"": ""Miscellaneous items for Wood work (R Meter)"",
            ""qty"": 28,
            ""rate"": 502,
            ""amt"": 14056,
            ""tax"": -2811.2,
            ""code"": ""70421"",
            ""cdesc"": ""Doors""
          },
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Civil miscellaneous works(Number)"",
            ""qty"": 600,
            ""rate"": 95,
            ""amt"": 57000,
            ""tax"": -11400,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBMIS094"",
            ""desc"": ""Civil miscellaneous works(Bags)"",
            ""qty"": 10,
            ""rate"": 550,
            ""amt"": 5500,
            ""tax"": -1100,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          }
        ],
        ""AP/INV/8/000817/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -152060,
            ""tax"": 0,
            ""code"": ""26154"",
            ""cdesc"": ""Retention Setup Account""
          }
        ],
        ""AP/INV/8/000819/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -124417,
            ""tax"": 0,
            ""code"": ""26154"",
            ""cdesc"": ""Retention Setup Account""
          }
        ],
        ""AP/INV/8/000751/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -0.42,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBMIS915"",
            ""desc"": ""Miscellaneous items for Plumbing-drainage  work"",
            ""qty"": 343669,
            ""rate"": 1,
            ""amt"": 343669,
            ""tax"": -68733.8,
            ""code"": ""70424"",
            ""cdesc"": ""Plumbing Internal Work""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 30930.21,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 30930.21,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 6873.38,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000766/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -17183,
            ""tax"": 0,
            ""code"": ""26154"",
            ""cdesc"": ""Retention Setup Account""
          }
        ],
        ""AP/INV/8/000722/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 1.68,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""XJOBFLR113"",
            ""desc"": ""Providing and laying I.P.S. flooring of 1:2:4 grade, 4"",
            ""qty"": 195,
            ""rate"": 500,
            ""amt"": 97500,
            ""tax"": -19500,
            ""code"": ""70418"",
            ""cdesc"": ""Finishing Work Tiling and Flooring""
          },
          {
            ""item"": ""XJOBBRI125"",
            ""desc"": ""Providing & constructing AAC (Siporex block) masonry w"",
            ""qty"": 3.22,
            ""rate"": 1450,
            ""amt"": 4669,
            ""tax"": -933.8,
            ""code"": ""70417"",
            ""cdesc"": ""Finishing Masonary Work""
          },
          {
            ""item"": ""XJOBPLR101"",
            ""desc"": ""Providing and applying 12mm-15mm thick single Coat Int"",
            ""qty"": 3.82,
            ""rate"": 470,
            ""amt"": 1795.4,
            ""tax"": -359.09,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBSTP026"",
            ""desc"": ""Proviidng & Laying integral cement based treatment for"",
            ""qty"": 6.98,
            ""rate"": 4250,
            ""amt"": 29665,
            ""tax"": -5933,
            ""code"": ""70414"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XJOBLBR008"",
            ""desc"": ""Civil miscellaneous works(Sq. Meter) [Brickbat Coba - "",
            ""qty"": 9,
            ""rate"": 1250,
            ""amt"": 11250,
            ""tax"": -2250,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""XJOBSEC030"",
            ""desc"": ""Providing & Fixing  approved Vitrified tiles (Colour, "",
            ""qty"": 167.12,
            ""rate"": 1867,
            ""amt"": 312013.04,
            ""tax"": -62402.6,
            ""code"": ""70416"",
            ""cdesc"": ""Structural Fabrication Superstruct""
          },
          {
            ""item"": ""XJOBMIS237"",
            ""desc"": ""Miscellaneous items for Work (R. Meter) [Providing and"",
            ""qty"": 12,
            ""rate"": 375,
            ""amt"": 4500,
            ""tax"": -900,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Miscellaneous items for Work (Sq. Meter) [MDF Paneling"",
            ""qty"": 114.05,
            ""rate"": 6500,
            ""amt"": 741325,
            ""tax"": -148265,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Miscellaneous items for Work (Sq. Meter) [ceiling in p"",
            ""qty"": 25.73,
            ""rate"": 14375,
            ""amt"": 369868.75,
            ""tax"": -73973.76,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS238"",
            ""desc"": ""Miscellaneous items for Work (Sq. Meter) [Gypsum false"",
            ""qty"": 130.95,
            ""rate"": 1350,
            ""amt"": 176781.15,
            ""tax"": -35356.22,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          },
          {
            ""item"": ""XJOBMIS237"",
            ""desc"": ""Miscellaneous items for Work (R. Meter) [Gypsum False "",
            ""qty"": 46.73,
            ""rate"": 296,
            ""amt"": 13832.08,
            ""tax"": -2766.42,
            ""code"": ""70419"",
            ""cdesc"": ""Other Finishing Work""
          }
        ],
        ""AP/INV/8/000738/26-27"": [
          {
            ""item"": """",
            ""desc"": """",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -88160,
            ""tax"": 0,
            ""code"": ""26154"",
            ""cdesc"": ""Retention Setup Account""
          }
        ],
        ""AP/INV/8/000694/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Rounding Off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -0.18,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""SSECWRK002"",
            ""desc"": ""SECURITY WORK (Daily) [MAY2026 ( 1NOS FOR DAY DUTY & 1"",
            ""qty"": 62,
            ""rate"": 548.39,
            ""amt"": 34000.18,
            ""tax"": -6460.04,
            ""code"": ""71416"",
            ""cdesc"": ""Security Charges Site""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3060.02,
            ""tax"": 0,
            ""code"": ""19001"",
            ""cdesc"": ""Interim - RCM Input Tax Credit - S""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_SGST"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 3060.02,
            ""tax"": 0,
            ""code"": ""25304"",
            ""cdesc"": ""RCM Payable - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 340,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 3060.02,
            ""tax"": 0,
            ""code"": ""19002"",
            ""cdesc"": ""Interim - RCM Input Tax Credit - C""
          },
          {
            ""item"": ""Reverse Charge"",
            ""desc"": ""Reverse Charge_CGST"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 3060.02,
            ""tax"": 0,
            ""code"": ""25305"",
            ""cdesc"": ""RCM Payable - CGST""
          }
        ],
        ""AP/INV/8/000699/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the Amount Payable towards Extension of Car Poli"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 47142,
            ""tax"": -8485.56,
            ""code"": ""73218"",
            ""cdesc"": ""Insurance General""
          },
          {
            ""item"": """",
            ""desc"": ""Rounding Off"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 0.44,
            ""tax"": 0,
            ""code"": ""70207"",
            ""cdesc"": ""Rounding off""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4242.78,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 4242.78,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          }
        ],
        ""AP/INV/8/000671/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the amount payable towards electricity charges f"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 133641,
            ""tax"": 0,
            ""code"": ""71414"",
            ""cdesc"": ""Electricity Expenses""
          }
        ],
        ""AP/INV/8/000673/26-27"": [
          {
            ""item"": ""XJOBLBR009"",
            ""desc"": ""Removing of construction debris with manually loading "",
            ""qty"": 40,
            ""rate"": 5500,
            ""amt"": 220000,
            ""tax"": -2200,
            ""code"": ""70442"",
            ""cdesc"": ""Civil Miscellaneous Works""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 0,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 2200,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000646/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the amount payable towards Electricity Bill for "",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 17240,
            ""tax"": 0,
            ""code"": ""71414"",
            ""cdesc"": ""Electricity Expenses""
          }
        ],
        ""AP/INV/8/000647/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being amount payable towards Development management fe"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 14000000,
            ""tax"": -3920000,
            ""code"": ""74101"",
            ""cdesc"": ""Professional Fees (HO)""
          },
          {
            ""item"": ""CGST"",
            ""desc"": ""CGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1260000,
            ""tax"": 0,
            ""code"": ""18912"",
            ""cdesc"": ""Input Tax Credit - CGST""
          },
          {
            ""item"": ""SGST"",
            ""desc"": ""SGST"",
            ""qty"": 1,
            ""rate"": 0,
            ""amt"": 1260000,
            ""tax"": 0,
            ""code"": ""18911"",
            ""cdesc"": ""Input Tax Credit - SGST""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 1400000,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ],
        ""AP/INV/8/000630/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the amount payable towards Electricity Bill for "",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 5247,
            ""tax"": 0,
            ""code"": ""71414"",
            ""cdesc"": ""Electricity Expenses""
          }
        ],
        ""AP/INV/8/000615/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the amount payable as refund towards Cancellatio"",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": -2100000,
            ""tax"": 0,
            ""code"": ""25610"",
            ""cdesc"": ""Customer Refund Net Off Account""
          }
        ],
        ""AP/INV/8/000645/26-27"": [
          {
            ""item"": """",
            ""desc"": ""Being the amount payable towards @7.5%, Revenue Share "",
            ""qty"": 0,
            ""rate"": 0,
            ""amt"": 26000000,
            ""tax"": -2600000,
            ""code"": ""75414"",
            ""cdesc"": ""Premium on Debenture Redemption (P""
          },
          {
            ""item"": ""Tax Deduction "",
            ""desc"": ""Tax Deduction at Source"",
            ""qty"": -1,
            ""rate"": 0,
            ""amt"": 2600000,
            ""tax"": 0,
            ""code"": ""25001"",
            ""cdesc"": ""TDS Payable""
          }
        ]
      },
      ""vendorPerf"": [
        {
          ""name"": ""SUNIL CONSTRUCTION CO."",
          ""cat"": ""Contractor"",
          ""orders"": 3,
          ""orderValue"": 20.82,
          ""billed"": 20.14,
          ""paid"": 19.27,
          ""balance"": 0.87,
          ""invoices"": 50,
          ""retention"": 1.04,
          ""grn"": 0,
          ""billProgress"": 97,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""SHRIVYA REALTY LLP"",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 18.63,
          ""paid"": 18.63,
          ""balance"": 0,
          ""invoices"": 8,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""GREENHEART INFRAPROJECTS PRIVATE L"",
          ""cat"": ""Contractor"",
          ""orders"": 4,
          ""orderValue"": 17.83,
          ""billed"": 14.51,
          ""paid"": 13.18,
          ""balance"": 1.34,
          ""invoices"": 52,
          ""retention"": 0.89,
          ""grn"": 5,
          ""billProgress"": 81,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""NIVASA DEVELOPERS"",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 6.34,
          ""paid"": 6.34,
          ""balance"": 0,
          ""invoices"": 4,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""BRIHANMUMBAI MUNICIPAL CORPORATION"",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 5.53,
          ""paid"": 5.42,
          ""balance"": 0.11,
          ""invoices"": 16,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 94,
          ""rejBills"": 1
        },
        {
          ""name"": ""VINAY WINDOWS SYSTEM PRIVATE LIMIT"",
          ""cat"": ""Contractor"",
          ""orders"": 2,
          ""orderValue"": 4.33,
          ""billed"": 4.11,
          ""paid"": 3.77,
          ""balance"": 0.34,
          ""invoices"": 12,
          ""retention"": 0.22,
          ""grn"": 0,
          ""billProgress"": 95,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""JRD ENTERPRISES"",
          ""cat"": ""Contractor"",
          ""orders"": 1,
          ""orderValue"": 3.96,
          ""billed"": 2.78,
          ""paid"": 2.64,
          ""balance"": 0.13,
          ""invoices"": 18,
          ""retention"": 0.2,
          ""grn"": 0,
          ""billProgress"": 70,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""R-LITE ELECTRICALS"",
          ""cat"": ""Service"",
          ""orders"": 2,
          ""orderValue"": 2.83,
          ""billed"": 1.79,
          ""paid"": 1.51,
          ""balance"": 0.27,
          ""invoices"": 17,
          ""retention"": 0,
          ""grn"": 1,
          ""billProgress"": 63,
          ""apprPct"": 94,
          ""rejBills"": 1
        },
        {
          ""name"": ""CRESCENDO"",
          ""cat"": ""Service"",
          ""orders"": 1,
          ""orderValue"": 1.49,
          ""billed"": 1.25,
          ""paid"": 1.04,
          ""balance"": 0.2,
          ""invoices"": 14,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 84,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""SANATAN & BROTHERS"",
          ""cat"": ""Service"",
          ""orders"": 1,
          ""orderValue"": 1.48,
          ""billed"": 1.16,
          ""paid"": 1.02,
          ""balance"": 0.14,
          ""invoices"": 21,
          ""retention"": 0,
          ""grn"": 1,
          ""billProgress"": 78,
          ""apprPct"": 95,
          ""rejBills"": 1
        },
        {
          ""name"": ""STAMP DUTY ONLINE PAYMENT A/C"",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 1.42,
          ""paid"": 1.03,
          ""balance"": 0.39,
          ""invoices"": 7,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""KONE ELEVATOR INDIA PRIVATE LIMITE"",
          ""cat"": ""Service"",
          ""orders"": 1,
          ""orderValue"": 0.94,
          ""billed"": 0.92,
          ""paid"": 0.92,
          ""balance"": 0,
          ""invoices"": 16,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 98,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""TRINITY HEALTH TECHNOLOGIES PRIVAT"",
          ""cat"": ""Service"",
          ""orders"": 2,
          ""orderValue"": 0.74,
          ""billed"": 0,
          ""paid"": 0,
          ""balance"": 0,
          ""invoices"": 0,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 0,
          ""rejBills"": 0
        },
        {
          ""name"": ""MCM FLEXI CLADDING INDIA PRIVATE L"",
          ""cat"": ""Material"",
          ""orders"": 1,
          ""orderValue"": 0.63,
          ""billed"": 0.63,
          ""paid"": 0.63,
          ""balance"": 0,
          ""invoices"": 2,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 100,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""SALARY PAYABLE A/C."",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 0.63,
          ""paid"": 0.63,
          ""balance"": 0,
          ""invoices"": 13,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 100,
          ""rejBills"": 0
        }
      ],
      ""vendorInv"": {
        ""SUNIL CONSTRUCTION CO."": [
          {
            ""no"": ""AP/INV/8/004049/25-26"",
            ""glDate"": ""31-03-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 431573,
            ""paid"": 0,
            ""balance"": 431573,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/004033/25-26"",
            ""glDate"": ""31-03-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 910430,
            ""paid"": 0,
            ""balance"": 910430,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002962/25-26"",
            ""glDate"": ""01-01-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 272110,
            ""paid"": 0,
            ""balance"": 272110,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002878/25-26"",
            ""glDate"": ""01-01-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 6313020.82,
            ""paid"": 6313020.82,
            ""balance"": 0,
            ""wopo"": ""PO/8/000468/23-24""
          },
          {
            ""no"": ""AP/INV/8/001155/25-26"",
            ""glDate"": ""31-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 482905,
            ""paid"": 0,
            ""balance"": 482905,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001154/25-26"",
            ""glDate"": ""31-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 244232,
            ""paid"": 0,
            ""balance"": 244232,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001153/25-26"",
            ""glDate"": ""31-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 166656,
            ""paid"": 0,
            ""balance"": 166656,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001152/25-26"",
            ""glDate"": ""31-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 161011,
            ""paid"": 0,
            ""balance"": 161011,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001133/25-26"",
            ""glDate"": ""11-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Retention Invoice"",
            ""amt"": 0,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001132/25-26"",
            ""glDate"": ""11-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 339961,
            ""paid"": 0,
            ""balance"": 339961,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000945/25-26"",
            ""glDate"": ""11-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 8027458.51,
            ""paid"": 8027458.51,
            ""balance"": 0,
            ""wopo"": ""PO/8/000468/23-24""
          },
          {
            ""no"": ""AP/INV/8/003772/24-25"",
            ""glDate"": ""31-03-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 413531.15,
            ""paid"": 413531.15,
            ""balance"": 0,
            ""wopo"": ""PO/8/000124/24-25""
          },
          {
            ""no"": ""AP/INV/8/003801/24-25"",
            ""glDate"": ""31-03-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Retention Invoice"",
            ""amt"": 0,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003800/24-25"",
            ""glDate"": ""31-03-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 17825,
            ""paid"": 0,
            ""balance"": 17825,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003692/24-25"",
            ""glDate"": ""27-03-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15807991.39,
            ""paid"": 15807991.39,
            ""balance"": 0,
            ""wopo"": ""PO/8/000468/23-24""
          }
        ],
        ""SHRIVYA REALTY LLP"": [
          {
            ""no"": ""AP/INV/8/000645/26-27"",
            ""glDate"": ""04-06-26"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 23400000,
            ""paid"": 23400000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002826/25-26"",
            ""glDate"": ""22-12-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 13432499.1,
            ""paid"": 13432499.1,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001583/25-26"",
            ""glDate"": ""09-09-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 18652667.4,
            ""paid"": 18652667.4,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000496/25-26"",
            ""glDate"": ""02-06-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 43349227.2,
            ""paid"": 43349227.2,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000497/25-26"",
            ""glDate"": ""29-05-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 219353.4,
            ""paid"": 219353.4,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003715/24-25"",
            ""glDate"": ""28-03-25"",
            ""dept"": ""ACCOUNTS"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 23231792.7,
            ""paid"": 23231792.7,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003893/24-25"",
            ""glDate"": ""06-03-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 34000000,
            ""paid"": 34000000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003200/24-25"",
            ""glDate"": ""11-02-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 30000000,
            ""paid"": 30000000,
            ""balance"": 0,
            ""wopo"": """"
          }
        ],
        ""GREENHEART INFRAPROJECTS PRIVATE L"": [
          {
            ""no"": ""AP/INV/8/000819/26-27"",
            ""glDate"": ""20-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 124417,
            ""paid"": 0,
            ""balance"": 124417,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000784/26-27"",
            ""glDate"": ""20-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2886466.34,
            ""paid"": 0,
            ""balance"": 2886466.34,
            ""wopo"": ""PO/8/000113/25-26""
          },
          {
            ""no"": ""AP/INV/8/000817/26-27"",
            ""glDate"": ""20-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 152060,
            ""paid"": 0,
            ""balance"": 152060,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000786/26-27"",
            ""glDate"": ""20-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 3527792.98,
            ""paid"": 3527792.98,
            ""balance"": 0,
            ""wopo"": ""PO/8/000215/24-25""
          },
          {
            ""no"": ""AP/INV/8/000722/26-27"",
            ""glDate"": ""12-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2045313.01,
            ""paid"": 0,
            ""balance"": 2045313.01,
            ""wopo"": ""PO/8/000429/25-26""
          },
          {
            ""no"": ""AP/INV/8/000738/26-27"",
            ""glDate"": ""12-06-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 88160,
            ""paid"": 0,
            ""balance"": 88160,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000428/26-27"",
            ""glDate"": ""16-05-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 104630,
            ""paid"": 0,
            ""balance"": 104630,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000412/26-27"",
            ""glDate"": ""16-05-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2427408.14,
            ""paid"": 0,
            ""balance"": 2427408.14,
            ""wopo"": ""PO/8/000215/24-25""
          },
          {
            ""no"": ""AP/INV/8/000107/26-27"",
            ""glDate"": ""07-04-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 667697,
            ""paid"": 0,
            ""balance"": 667697,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000065/26-27"",
            ""glDate"": ""07-04-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15490568.23,
            ""paid"": 15490568.23,
            ""balance"": 0,
            ""wopo"": ""PO/8/000215/24-25""
          },
          {
            ""no"": ""AP/INV/8/003993/25-26"",
            ""glDate"": ""27-03-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 55323,
            ""paid"": 0,
            ""balance"": 55323,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003886/25-26"",
            ""glDate"": ""27-03-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1283500.68,
            ""paid"": 1283500.68,
            ""balance"": 0,
            ""wopo"": ""PO/8/000113/25-26""
          },
          {
            ""no"": ""AP/INV/8/003218/25-26"",
            ""glDate"": ""03-02-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2970985.11,
            ""paid"": 2970985.11,
            ""balance"": 0,
            ""wopo"": ""PO/8/000215/24-25""
          },
          {
            ""no"": ""AP/INV/8/003214/25-26"",
            ""glDate"": ""03-02-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 3812242.7,
            ""paid"": 3812242.7,
            ""balance"": 0,
            ""wopo"": ""PO/8/000182/25-26""
          },
          {
            ""no"": ""AP/INV/8/003394/25-26"",
            ""glDate"": ""03-02-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 128060,
            ""paid"": 0,
            ""balance"": 128060,
            ""wopo"": """"
          }
        ],
        ""NIVASA DEVELOPERS"": [
          {
            ""no"": ""AP/INV/8/000647/26-27"",
            ""glDate"": ""08-06-26"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15120000,
            ""paid"": 15120000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002825/25-26"",
            ""glDate"": ""22-12-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 8596799.08,
            ""paid"": 8596799.08,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001584/25-26"",
            ""glDate"": ""09-09-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 11937706.8,
            ""paid"": 11937706.8,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000499/25-26"",
            ""glDate"": ""02-06-25"",
            ""dept"": ""RESOURCE MOBILISATION "",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 27743505.9,
            ""paid"": 27743505.9,
            ""balance"": 0,
            ""wopo"": """"
          }
        ],
        ""BRIHANMUMBAI MUNICIPAL CORPORATION"": [
          {
            ""no"": ""AP/INV/8/000576/26-27"",
            ""glDate"": ""01-06-26"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15210,
            ""paid"": 15210,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000120/26-27"",
            ""glDate"": ""13-04-26"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 56850,
            ""paid"": 56850,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003858/25-26"",
            ""glDate"": ""20-02-26"",
            ""dept"": ""ACCOUNTS"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 722676,
            ""paid"": 722676,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003436/25-26"",
            ""glDate"": ""20-02-26"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 722676,
            ""paid"": 722676,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003430/25-26"",
            ""glDate"": ""19-02-26"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Rejected"",
            ""payStatus"": ""Cancelled"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 722676,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002329/25-26"",
            ""glDate"": ""13-11-25"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1389380,
            ""paid"": 1389380,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000300/25-26"",
            ""glDate"": ""09-05-25"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 433281,
            ""paid"": 433281,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003665/24-25"",
            ""glDate"": ""25-03-25"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 43430000,
            ""paid"": 43430000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002827/24-25"",
            ""glDate"": ""17-01-25"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 955296,
            ""paid"": 955296,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002306/24-25"",
            ""glDate"": ""26-11-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 12000,
            ""paid"": 12000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002299/24-25"",
            ""glDate"": ""26-11-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1182000,
            ""paid"": 1182000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002296/24-25"",
            ""glDate"": ""26-11-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1123500,
            ""paid"": 1123500,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002294/24-25"",
            ""glDate"": ""26-11-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1123500,
            ""paid"": 1123500,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002054/24-25"",
            ""glDate"": ""06-11-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2549198,
            ""paid"": 2549198,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001045/24-25"",
            ""glDate"": ""29-07-24"",
            ""dept"": ""APPROVAL/LIASON"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 391009,
            ""paid"": 0,
            ""balance"": 391009,
            ""wopo"": """"
          }
        ],
        ""VINAY WINDOWS SYSTEM PRIVATE LIMIT"": [
          {
            ""no"": ""AP/INV/8/003262/25-26"",
            ""glDate"": ""06-02-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 71553,
            ""paid"": 0,
            ""balance"": 71553,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003256/25-26"",
            ""glDate"": ""06-02-26"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1660037.66,
            ""paid"": 0,
            ""balance"": 1660037.66,
            ""wopo"": ""PO/8/000335/24-25""
          },
          {
            ""no"": ""AP/INV/8/002484/25-26"",
            ""glDate"": ""24-11-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 385517,
            ""paid"": 0,
            ""balance"": 385517,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002479/25-26"",
            ""glDate"": ""24-11-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 8943982.41,
            ""paid"": 8943982.41,
            ""balance"": 0,
            ""wopo"": ""PO/8/000335/24-25""
          },
          {
            ""no"": ""AP/INV/8/001706/25-26"",
            ""glDate"": ""12-09-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 218301,
            ""paid"": 0,
            ""balance"": 218301,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001676/25-26"",
            ""glDate"": ""12-09-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 260129,
            ""paid"": 0,
            ""balance"": 260129,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001642/25-26"",
            ""glDate"": ""12-09-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Retention Release"",
            ""amt"": 6034989.45,
            ""paid"": 6034989.45,
            ""balance"": 0,
            ""wopo"": ""PO/8/000438/24-25""
          },
          {
            ""no"": ""AP/INV/8/001638/25-26"",
            ""glDate"": ""12-09-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 5064578.68,
            ""paid"": 5064578.68,
            ""balance"": 0,
            ""wopo"": ""PO/8/000335/24-25""
          },
          {
            ""no"": ""AP/INV/8/000951/25-26"",
            ""glDate"": ""09-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 667665,
            ""paid"": 0,
            ""balance"": 667665,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000918/25-26"",
            ""glDate"": ""09-07-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15489836.86,
            ""paid"": 15489836.86,
            ""balance"": 0,
            ""wopo"": ""PO/8/000335/24-25""
          },
          {
            ""no"": ""AP/INV/8/000321/25-26"",
            ""glDate"": ""03-05-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 94052,
            ""paid"": 0,
            ""balance"": 94052,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000252/25-26"",
            ""glDate"": ""03-05-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2182003.25,
            ""paid"": 2182003.25,
            ""balance"": 0,
            ""wopo"": ""PO/8/000335/24-25""
          }
        ],
        ""JRD ENTERPRISES"": [
          {
            ""no"": ""AP/INV/8/003028/24-25"",
            ""glDate"": ""18-01-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Retention Invoice"",
            ""amt"": 0,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003027/24-25"",
            ""glDate"": ""18-01-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 650670,
            ""paid"": 0,
            ""balance"": 650670,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002828/24-25"",
            ""glDate"": ""18-01-25"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2526309.28,
            ""paid"": 2526309.28,
            ""balance"": 0,
            ""wopo"": ""PO/8/000194/23-24""
          },
          {
            ""no"": ""AP/INV/8/007129/23-24"",
            ""glDate"": ""31-03-24"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 14824,
            ""paid"": 14824,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/007224/23-24"",
            ""glDate"": ""30-03-24"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 43408,
            ""paid"": 0,
            ""balance"": 43408,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/006801/23-24"",
            ""glDate"": ""30-03-24"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 24206,
            ""paid"": 0,
            ""balance"": 24206,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/006484/23-24"",
            ""glDate"": ""30-03-24"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1911612,
            ""paid"": 1911612,
            ""balance"": 0,
            ""wopo"": ""PO/8/000194/23-24""
          },
          {
            ""no"": ""AP/INV/8/005921/23-24"",
            ""glDate"": ""01-01-24"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 14113,
            ""paid"": 0,
            ""balance"": 14113,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/004215/23-24"",
            ""glDate"": ""10-11-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 4843814,
            ""paid"": 4843814,
            ""balance"": 0,
            ""wopo"": ""PO/8/000194/23-24""
          },
          {
            ""no"": ""AP/INV/8/005057/23-24"",
            ""glDate"": ""10-11-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 104364,
            ""paid"": 0,
            ""balance"": 104364,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/005058/23-24"",
            ""glDate"": ""10-11-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 59280,
            ""paid"": 0,
            ""balance"": 59280,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/005039/23-24"",
            ""glDate"": ""08-11-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 180242,
            ""paid"": 0,
            ""balance"": 180242,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/004186/23-24"",
            ""glDate"": ""08-11-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 8628308,
            ""paid"": 8628308,
            ""balance"": 0,
            ""wopo"": ""PO/8/000194/23-24""
          },
          {
            ""no"": ""AP/INV/8/004858/23-24"",
            ""glDate"": ""07-10-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 71446,
            ""paid"": 0,
            ""balance"": 71446,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/004060/23-24"",
            ""glDate"": ""07-10-23"",
            ""dept"": ""CONSTRUCTION/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2108825,
            ""paid"": 2108825,
            ""balance"": 0,
            ""wopo"": ""PO/8/000194/23-24""
          }
        ],
        ""R-LITE ELECTRICALS"": [
          {
            ""no"": ""AP/INV/8/000137/26-27"",
            ""glDate"": ""15-04-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 159313,
            ""paid"": 0,
            ""balance"": 159313,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000129/26-27"",
            ""glDate"": ""15-04-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 3727920.43,
            ""paid"": 3727920.43,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/003393/25-26"",
            ""glDate"": ""17-02-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 79427,
            ""paid"": 0,
            ""balance"": 79427,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003387/25-26"",
            ""glDate"": ""17-02-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1858581.68,
            ""paid"": 1858581.68,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/002931/25-26"",
            ""glDate"": ""05-01-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 88799,
            ""paid"": 0,
            ""balance"": 88799,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002923/25-26"",
            ""glDate"": ""05-01-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2077891.25,
            ""paid"": 2077891.25,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/002877/25-26"",
            ""glDate"": ""26-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Rejected"",
            ""payStatus"": ""Cancelled"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2095651,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/002102/25-26"",
            ""glDate"": ""16-10-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1664940.74,
            ""paid"": 1664940.74,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/002133/25-26"",
            ""glDate"": ""16-10-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 71151,
            ""paid"": 0,
            ""balance"": 71151,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001717/25-26"",
            ""glDate"": ""08-09-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 62023,
            ""paid"": 0,
            ""balance"": 62023,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001570/25-26"",
            ""glDate"": ""08-09-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1451331.18,
            ""paid"": 1451331.18,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          },
          {
            ""no"": ""AP/INV/8/000927/25-26"",
            ""glDate"": ""10-07-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 66867.48,
            ""paid"": 66867.48,
            ""balance"": 0,
            ""wopo"": ""PO/8/000062/25-26""
          },
          {
            ""no"": ""AP/INV/8/000341/25-26"",
            ""glDate"": ""13-05-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 75348.99,
            ""paid"": 75348.99,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003683/24-25"",
            ""glDate"": ""22-03-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 89408,
            ""paid"": 0,
            ""balance"": 89408,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003553/24-25"",
            ""glDate"": ""22-03-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2092137.48,
            ""paid"": 2092137.48,
            ""balance"": 0,
            ""wopo"": ""PO/8/000266/24-25""
          }
        ],
        ""CRESCENDO"": [
          {
            ""no"": ""AP/INV/8/003447/25-26"",
            ""glDate"": ""20-02-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1740447.3,
            ""paid"": 0,
            ""balance"": 1740447.3,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/003070/25-26"",
            ""glDate"": ""19-01-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 4016035.02,
            ""paid"": 4016035.02,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/002037/25-26"",
            ""glDate"": ""08-10-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 23500,
            ""paid"": 0,
            ""balance"": 23500,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002016/25-26"",
            ""glDate"": ""08-10-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 545200,
            ""paid"": 545200,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/001374/25-26"",
            ""glDate"": ""16-08-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 30627,
            ""paid"": 0,
            ""balance"": 30627,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001354/25-26"",
            ""glDate"": ""16-08-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 710536.38,
            ""paid"": 710536.38,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/001292/25-26"",
            ""glDate"": ""05-08-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 67247,
            ""paid"": 0,
            ""balance"": 67247,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001228/25-26"",
            ""glDate"": ""05-08-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1560120.38,
            ""paid"": 1560120.38,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/000636/25-26"",
            ""glDate"": ""11-06-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 85775,
            ""paid"": 0,
            ""balance"": 85775,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000622/25-26"",
            ""glDate"": ""11-06-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1989980.99,
            ""paid"": 1989980.99,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/000406/25-26"",
            ""glDate"": ""19-05-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 48979,
            ""paid"": 0,
            ""balance"": 48979,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000385/25-26"",
            ""glDate"": ""19-05-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1136315.36,
            ""paid"": 1136315.36,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          },
          {
            ""no"": ""AP/INV/8/002603/24-25"",
            ""glDate"": ""26-12-24"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 20877,
            ""paid"": 0,
            ""balance"": 20877,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002593/24-25"",
            ""glDate"": ""26-12-24"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 484353.08,
            ""paid"": 484353.08,
            ""balance"": 0,
            ""wopo"": ""PO/8/000290/24-25""
          }
        ],
        ""SANATAN & BROTHERS"": [
          {
            ""no"": ""AP/INV/8/000766/26-27"",
            ""glDate"": ""17-06-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 17183,
            ""paid"": 0,
            ""balance"": 17183,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000751/26-27"",
            ""glDate"": ""17-06-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 398655.62,
            ""paid"": 0,
            ""balance"": 398655.62,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/003765/25-26"",
            ""glDate"": ""21-03-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Rejected"",
            ""payStatus"": ""Cancelled"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 532907,
            ""paid"": 0,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/003506/25-26"",
            ""glDate"": ""25-02-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 47035,
            ""paid"": 0,
            ""balance"": 47035,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003492/25-26"",
            ""glDate"": ""25-02-26"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1091201.2,
            ""paid"": 1091201.2,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/002894/25-26"",
            ""glDate"": ""30-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 78459,
            ""paid"": 0,
            ""balance"": 78459,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002882/25-26"",
            ""glDate"": ""30-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1820241.52,
            ""paid"": 1820241.52,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/002286/25-26"",
            ""glDate"": ""07-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 61569,
            ""paid"": 0,
            ""balance"": 61569,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002276/25-26"",
            ""glDate"": ""07-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1428390.58,
            ""paid"": 1428390.58,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/001655/25-26"",
            ""glDate"": ""15-09-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 730714.48,
            ""paid"": 730714.48,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/001700/25-26"",
            ""glDate"": ""15-09-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 31496,
            ""paid"": 0,
            ""balance"": 31496,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000971/25-26"",
            ""glDate"": ""12-07-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 20778,
            ""paid"": 0,
            ""balance"": 20778,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000946/25-26"",
            ""glDate"": ""12-07-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 482054.7,
            ""paid"": 482054.7,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          },
          {
            ""no"": ""AP/INV/8/000598/25-26"",
            ""glDate"": ""06-06-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Retention Invoice"",
            ""amt"": 66755,
            ""paid"": 0,
            ""balance"": 66755,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000584/25-26"",
            ""glDate"": ""06-06-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1548709.12,
            ""paid"": 1548709.12,
            ""balance"": 0,
            ""wopo"": ""PO/8/000249/24-25""
          }
        ],
        ""STAMP DUTY ONLINE PAYMENT A/C"": [
          {
            ""no"": ""AP/INV/8/001241/24-25"",
            ""glDate"": ""01-01-25"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1825600,
            ""paid"": 0,
            ""balance"": 1825600,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000833/24-25"",
            ""glDate"": ""10-07-24"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2066400,
            ""paid"": 0,
            ""balance"": 2066400,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000104/24-25"",
            ""glDate"": ""02-05-24"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1828221,
            ""paid"": 1828221,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/005926/23-24"",
            ""glDate"": ""14-02-24"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2019161,
            ""paid"": 2019161,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/005925/23-24"",
            ""glDate"": ""13-02-24"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1888000,
            ""paid"": 1888000,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/005924/23-24"",
            ""glDate"": ""13-02-24"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2093096,
            ""paid"": 2093096,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/006178/23-24"",
            ""glDate"": ""28-11-23"",
            ""dept"": ""RESIDENTIAL SALES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2454000,
            ""paid"": 2454000,
            ""balance"": 0,
            ""wopo"": """"
          }
        ],
        ""KONE ELEVATOR INDIA PRIVATE LIMITE"": [
          {
            ""no"": ""AP/INV/8/002585/25-26"",
            ""glDate"": ""04-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002584/25-26"",
            ""glDate"": ""04-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002567/25-26"",
            ""glDate"": ""03-12-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 329322.17,
            ""paid"": 329322.17,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002494/25-26"",
            ""glDate"": ""25-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 201132.51,
            ""paid"": 201132.51,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002492/25-26"",
            ""glDate"": ""25-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 329321.66,
            ""paid"": 329321.66,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002491/25-26"",
            ""glDate"": ""25-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002490/25-26"",
            ""glDate"": ""25-11-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/001043/25-26"",
            ""glDate"": ""22-07-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 658643.83,
            ""paid"": 658643.83,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/000487/25-26"",
            ""glDate"": ""29-05-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1479000,
            ""paid"": 1479000,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/000089/25-26"",
            ""glDate"": ""17-04-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 591600,
            ""paid"": 591600,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/000088/25-26"",
            ""glDate"": ""17-04-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 591600,
            ""paid"": 591600,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/003438/24-25"",
            ""glDate"": ""13-03-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1479000,
            ""paid"": 1479000,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/003437/24-25"",
            ""glDate"": ""13-03-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1479000,
            ""paid"": 1479000,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002831/24-25"",
            ""glDate"": ""18-01-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          },
          {
            ""no"": ""AP/INV/8/002830/24-25"",
            ""glDate"": ""18-01-25"",
            ""dept"": ""PLANNING  AND ESTIMATI"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 295800,
            ""paid"": 295800,
            ""balance"": 0,
            ""wopo"": ""PO/8/000247/24-25""
          }
        ],
        ""TRINITY HEALTH TECHNOLOGIES PRIVAT"": [],
        ""MCM FLEXI CLADDING INDIA PRIVATE L"": [
          {
            ""no"": ""AP/INV/8/003532/24-25"",
            ""glDate"": ""20-03-25"",
            ""dept"": ""PURCHASE"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 2851264,
            ""paid"": 2851264,
            ""balance"": 0,
            ""wopo"": ""PO/8/000288/24-25""
          },
          {
            ""no"": ""AP/INV/8/003510/24-25"",
            ""glDate"": ""18-03-25"",
            ""dept"": ""PURCHASE"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 3454219,
            ""paid"": 3454219,
            ""balance"": 0,
            ""wopo"": ""PO/8/000288/24-25""
          }
        ],
        ""SALARY PAYABLE A/C."": [
          {
            ""no"": ""AP/INV/8/000639/26-27"",
            ""glDate"": ""01-06-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 263853,
            ""paid"": 263853,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/000550/26-27"",
            ""glDate"": ""01-05-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 394422,
            ""paid"": 394422,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003964/25-26"",
            ""glDate"": ""31-03-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 347816,
            ""paid"": 347816,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003551/25-26"",
            ""glDate"": ""04-03-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 232399,
            ""paid"": 232399,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/003244/25-26"",
            ""glDate"": ""05-02-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 386528,
            ""paid"": 386528,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002921/25-26"",
            ""glDate"": ""03-01-26"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 386411,
            ""paid"": 386411,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002578/25-26"",
            ""glDate"": ""03-12-25"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 548352,
            ""paid"": 548352,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/002235/25-26"",
            ""glDate"": ""04-11-25"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 1140078,
            ""paid"": 1140078,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001918/25-26"",
            ""glDate"": ""03-10-25"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 725066,
            ""paid"": 725066,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001990/24-25"",
            ""glDate"": ""24-10-24"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 602327,
            ""paid"": 602327,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001660/24-25"",
            ""glDate"": ""02-10-24"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 599541,
            ""paid"": 599541,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001827/24-25"",
            ""glDate"": ""01-10-24"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 599541,
            ""paid"": 599541,
            ""balance"": 0,
            ""wopo"": """"
          },
          {
            ""no"": ""AP/INV/8/001108/24-25"",
            ""glDate"": ""06-08-24"",
            ""dept"": ""PAYROLL"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 109283,
            ""paid"": 109283,
            ""balance"": 0,
            ""wopo"": """"
          }
        ]
      }
    },
    ""TWR"": {
      ""project"": {
        ""name"": ""Twenty Five South — Tower C"",
        ""loc"": ""Mahalaxmi, Mumbai"",
        ""entity"": ""Joynest Premises Pvt Ltd"",
        ""group"": ""Hubtown"",
        ""id"": ""TWR"",
        ""area"": 347000
      },
      ""totals"": {
        ""budget"": 285.1,
        ""committed"": 254,
        ""woBilled"": 239.5,
        ""directBilled"": 9.95,
        ""balance"": 14.5,
        ""available"": 16.6,
        ""unapproved"": 0,
        ""unposted"": 0,
        ""retentionHeld"": 10.2,
        ""poValueMat"": 120.5,
        ""poValueDept"": 88,
        ""invTotal"": 249.45,
        ""invPaid"": 214,
        ""invBal"": 35.45,
        ""vendorCount"": 18,
        ""vendorOrderValue"": 254.5,
        ""vendorBilled"": 249.45,
        ""vendorPaid"": 214,
        ""vendorRetention"": 12.4,
        ""vendorContractors"": 6,
        ""counts"": {
          ""wo"": 4,
          ""poMat"": 2,
          ""poDept"": 2,
          ""po"": 2,
          ""asn"": 2,
          ""inv"": 4,
          ""vendor"": 18
        }
      },
      ""budget"": [
        {
          ""grp"": ""Budget Group for Civil Expenses"",
          ""code"": ""90101"",
          ""desc"": ""Civil Works"",
          ""A"": 221.1,
          ""B"": 205,
          ""C"": 195,
          ""D"": 6.55,
          ""E"": 10,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 6.1
        },
        {
          ""grp"": ""Budget group for RCC Consultants"",
          ""code"": ""90201"",
          ""desc"": ""RCC Consultants"",
          ""A"": 8,
          ""B"": 7.5,
          ""C"": 7,
          ""D"": 0.2,
          ""E"": 0.5,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget group for Architect Expenses"",
          ""code"": ""90301"",
          ""desc"": ""Architect Expenses"",
          ""A"": 12,
          ""B"": 11,
          ""C"": 10.5,
          ""D"": 0.3,
          ""E"": 0.5,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.5
        },
        {
          ""grp"": ""Budget group for Service Consultancy MEP"",
          ""code"": ""90401"",
          ""desc"": ""Service Consultancy MEP"",
          ""A"": 9,
          ""B"": 8.5,
          ""C"": 8,
          ""D"": 0.1,
          ""E"": 0.5,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0
        },
        {
          ""grp"": ""Budget group for Brokerage"",
          ""code"": ""90501"",
          ""desc"": ""Brokerage"",
          ""A"": 15,
          ""B"": 9,
          ""C"": 8,
          ""D"": 1,
          ""E"": 1,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 5
        },
        {
          ""grp"": ""Budget group for Legal Fees"",
          ""code"": ""90601"",
          ""desc"": ""Legal Fees"",
          ""A"": 6,
          ""B"": 4,
          ""C"": 3.5,
          ""D"": 0.2,
          ""E"": 0.5,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 1.5
        },
        {
          ""grp"": ""Budget group for Marketing Expenses"",
          ""code"": ""90701"",
          ""desc"": ""Marketing Expenses"",
          ""A"": 10,
          ""B"": 6,
          ""C"": 5,
          ""D"": 1.5,
          ""E"": 1,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 3
        },
        {
          ""grp"": ""Budget group for Environmental Consultant"",
          ""code"": ""90801"",
          ""desc"": ""Environmental Consultant"",
          ""A"": 4,
          ""B"": 3,
          ""C"": 2.5,
          ""D"": 0.1,
          ""E"": 0.5,
          ""unappr"": 0,
          ""unposted"": 0,
          ""avail"": 0.5
        }
      ],
      ""budgetTree"": {
        ""90101"": {
          ""desc"": ""Civil Works"",
          ""children"": [
            {
              ""code"": ""70101"",
              ""desc"": ""Excavation & Earthwork"",
              ""A"": 20,
              ""B"": 19,
              ""C"": 18.5,
              ""D"": 0.5,
              ""E"": 0.5,
              ""F"": 0.5
            },
            {
              ""code"": ""70102"",
              ""desc"": ""RCC Superstructure"",
              ""A"": 150,
              ""B"": 142,
              ""C"": 135,
              ""D"": 4,
              ""E"": 7,
              ""F"": 1
            },
            {
              ""code"": ""70103"",
              ""desc"": ""Finishing Works"",
              ""A"": 51.1,
              ""B"": 44,
              ""C"": 41.5,
              ""D"": 2.05,
              ""E"": 2.5,
              ""F"": 4.6
            }
          ]
        },
        ""90301"": {
          ""desc"": ""Architect Expenses"",
          ""children"": [
            {
              ""code"": ""70301"",
              ""desc"": ""Design & Drawings"",
              ""A"": 12,
              ""B"": 11,
              ""C"": 10.5,
              ""D"": 0.3,
              ""E"": 0.5,
              ""F"": 0.5
            }
          ]
        }
      },
      ""wos"": [
        {
          ""no"": ""PO/TC/000101/24-25"",
          ""glDate"": ""15-03-25"",
          ""dept"": ""CIVIL/EXECUTION"",
          ""vendor"": ""METRO INFRA PROJECTS"",
          ""amount"": 95,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Partially Received"",
          ""asnNo"": ""ASN3101"",
          ""asnDate"": ""18-03-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/TC/000102/24-25"",
          ""glDate"": ""15-03-25"",
          ""dept"": ""CIVIL/EXECUTION"",
          ""vendor"": ""SUNRISE STRUCTURES"",
          ""amount"": 62.5,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": ""ASN3102"",
          ""asnDate"": ""20-03-2026"",
          ""asnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/TC/000103/24-25"",
          ""glDate"": ""15-03-25"",
          ""dept"": ""FINISHING"",
          ""vendor"": ""GLEN INTERIORS"",
          ""amount"": 28,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Approved"",
          ""billStatus"": ""Fully Billed"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        },
        {
          ""no"": ""PO/TC/000104/24-25"",
          ""glDate"": ""15-03-25"",
          ""dept"": ""MEP"",
          ""vendor"": ""VOLT ELECTRO MECH"",
          ""amount"": 19.6,
          ""ret"": 5,
          ""wctRet"": null,
          ""retDate"": """",
          ""createdBy"": ""Yogesh Patel"",
          ""appr"": ""Pending Approval"",
          ""billStatus"": ""Pending Bill"",
          ""asnNo"": null,
          ""asnDate"": null,
          ""asnStatus"": null,
          ""memo"": """"
        }
      ],
      ""woLines"": {
        ""PO/TC/000101/24-25"": [
          {
            ""item"": ""XCIV001"",
            ""desc"": ""RCC M40 for raft & columns"",
            ""unit"": ""Cu.M"",
            ""qty"": 42000,
            ""rate"": 18500,
            ""amt"": 777000000,
            ""tax"": 0,
            ""bqty"": 38000,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          },
          {
            ""item"": ""XCIV002"",
            ""desc"": ""Excavation in hard strata"",
            ""unit"": ""Cu.M"",
            ""qty"": 56000,
            ""rate"": 750,
            ""amt"": 42000000,
            ""tax"": 0,
            ""bqty"": 56000,
            ""wpRet"": null,
            ""code"": ""70101"",
            ""cdesc"": ""Excavation & Earthwork""
          }
        ],
        ""PO/TC/000102/24-25"": [
          {
            ""item"": ""XCIV003"",
            ""desc"": ""Formwork & shuttering"",
            ""unit"": ""Sq.M"",
            ""qty"": 120000,
            ""rate"": 520,
            ""amt"": 62400000,
            ""tax"": 0,
            ""bqty"": 90000,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          }
        ],
        ""PO/TC/000103/24-25"": [
          {
            ""item"": ""XFIN001"",
            ""desc"": ""Wall plaster & putty"",
            ""unit"": ""Sq.M"",
            ""qty"": 210000,
            ""rate"": 240,
            ""amt"": 50400000,
            ""tax"": 0,
            ""bqty"": 210000,
            ""wpRet"": null,
            ""code"": ""70103"",
            ""cdesc"": ""Finishing Works""
          }
        ],
        ""PO/TC/000104/24-25"": [
          {
            ""item"": ""XMEP001"",
            ""desc"": ""HT/LT cabling package"",
            ""unit"": ""Lot"",
            ""qty"": 1,
            ""rate"": 196000000,
            ""amt"": 196000000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70301"",
            ""cdesc"": ""Service Consultancy MEP""
          }
        ]
      },
      ""pos"": [
        {
          ""no"": ""PO/TC/M/000045/24-25"",
          ""cat"": ""Material"",
          ""glDate"": ""02-02-25"",
          ""dept"": ""STORES"",
          ""supplier"": ""ULTRATECH CEMENT LTD"",
          ""item"": ""OPC 53 Grade Cement"",
          ""val"": 720,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Store Mgr"",
          ""status"": ""Fully Billed"",
          ""appr"": ""Approved"",
          ""grnNo"": ""GRN-TC-221"",
          ""grnDate"": ""10-02-2025"",
          ""grnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/TC/M/000046/24-25"",
          ""cat"": ""Material"",
          ""glDate"": ""09-02-25"",
          ""dept"": ""STORES"",
          ""supplier"": ""TATA STEEL LTD"",
          ""item"": ""TMT Fe550D Rebar"",
          ""val"": 485,
          ""ret"": 0,
          ""wctRet"": null,
          ""createdBy"": ""Store Mgr"",
          ""status"": ""Partially Received"",
          ""appr"": ""Approved"",
          ""grnNo"": ""GRN-TC-230"",
          ""grnDate"": ""18-02-2025"",
          ""grnStatus"": ""Approved"",
          ""memo"": """"
        },
        {
          ""no"": ""PO/TC/D/000012/24-25"",
          ""cat"": ""Department"",
          ""glDate"": ""14-01-25"",
          ""dept"": ""PLANNING"",
          ""supplier"": ""SOLIDWORKS CONSULTS"",
          ""item"": ""Structural peer review"",
          ""val"": 180,
          ""ret"": 5,
          ""wctRet"": null,
          ""createdBy"": ""Planning"",
          ""status"": ""Pending Bill"",
          ""appr"": ""Approved"",
          ""grnNo"": null,
          ""grnDate"": null,
          ""grnStatus"": null,
          ""memo"": """"
        }
      ],
      ""poLines"": {
        ""PO/TC/M/000045/24-25"": [
          {
            ""item"": ""MCEM001"",
            ""desc"": ""OPC 53 Grade Cement (bags)"",
            ""unit"": ""Bag"",
            ""qty"": 120000,
            ""rate"": 600,
            ""amt"": 72000000,
            ""tax"": 0,
            ""bqty"": 120000,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          }
        ],
        ""PO/TC/M/000046/24-25"": [
          {
            ""item"": ""MSTL001"",
            ""desc"": ""TMT Fe550D 8-32mm"",
            ""unit"": ""MT"",
            ""qty"": 6200,
            ""rate"": 78000,
            ""amt"": 483600000,
            ""tax"": 0,
            ""bqty"": 4800,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          }
        ],
        ""PO/TC/D/000012/24-25"": [
          {
            ""item"": ""DCON001"",
            ""desc"": ""Structural peer review"",
            ""unit"": ""Lot"",
            ""qty"": 1,
            ""rate"": 18000000,
            ""amt"": 18000000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70201"",
            ""cdesc"": ""RCC Consultants""
          }
        ]
      },
      ""asnHeaders"": {
        ""PO/TC/000101/24-25"": [
          {
            ""asnNo"": ""ASN3101"",
            ""challan"": ""MIP/CH/221"",
            ""date"": ""18-03-2026"",
            ""appDate"": ""22-03-2026"",
            ""status"": ""Approved"",
            ""asnQty"": 38000,
            ""apprQty"": 38000,
            ""billQty"": 34000,
            ""draftInv"": ""DRFT-TC-501"",
            ""responsible"": ""Site Engg A"",
            ""lineCount"": 1
          }
        ],
        ""PO/TC/M/000045/24-25"": [
          {
            ""asnNo"": ""ASN3050"",
            ""challan"": ""UTC/CH/990"",
            ""date"": ""10-02-2025"",
            ""appDate"": ""12-02-2025"",
            ""status"": ""Approved"",
            ""asnQty"": 120000,
            ""apprQty"": 120000,
            ""billQty"": 120000,
            ""draftInv"": ""DRFT-TC-410"",
            ""responsible"": ""Store Mgr"",
            ""lineCount"": 1
          }
        ]
      },
      ""asnLines"": {
        ""ASN3101"": [
          {
            ""docNo"": ""MIP/CH/221"",
            ""draftInv"": ""DRFT-TC-501"",
            ""vendBill"": ""VB-TC-501"",
            ""asnQty"": 38000,
            ""apprQty"": 38000,
            ""billQty"": 34000,
            ""responsible"": ""Site Engg A""
          }
        ],
        ""ASN3050"": [
          {
            ""docNo"": ""UTC/CH/990"",
            ""draftInv"": ""DRFT-TC-410"",
            ""vendBill"": ""VB-TC-410"",
            ""asnQty"": 120000,
            ""apprQty"": 120000,
            ""billQty"": 120000,
            ""responsible"": ""Store Mgr""
          }
        ]
      },
      ""invoices"": [
        {
          ""no"": ""AP/INV/TC/000901/25-26"",
          ""vendor"": ""METRO INFRA PROJECTS"",
          ""glDate"": ""28-03-26"",
          ""dept"": ""CIVIL/EXECUTION"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Open"",
          ""amt"": 88000000,
          ""paid"": 0,
          ""balance"": 88000000,
          ""ret"": 5,
          ""wopo"": ""PO/TC/000101/24-25"",
          ""memo"": ""RA-3 civil""
        },
        {
          ""no"": ""AP/INV/TC/000902/25-26"",
          ""vendor"": ""ULTRATECH CEMENT LTD"",
          ""glDate"": ""12-02-26"",
          ""dept"": ""STORES"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 72000000,
          ""paid"": 72000000,
          ""balance"": 0,
          ""ret"": 0,
          ""wopo"": ""PO/TC/M/000045/24-25"",
          ""memo"": ""Cement supply""
        },
        {
          ""no"": ""AP/INV/TC/000903/25-26"",
          ""vendor"": ""GLEN INTERIORS"",
          ""glDate"": ""05-03-26"",
          ""dept"": ""FINISHING"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Approved"",
          ""payStatus"": ""Paid In Full"",
          ""amt"": 50400000,
          ""paid"": 50400000,
          ""balance"": 0,
          ""ret"": 5,
          ""wopo"": ""PO/TC/000103/24-25"",
          ""memo"": ""Finishing RA-final""
        },
        {
          ""no"": ""AP/INV/TC/000904/25-26"",
          ""vendor"": ""BRIGHT MEDIA"",
          ""glDate"": ""20-01-26"",
          ""dept"": ""MARKETING"",
          ""type"": ""Vendor Invoice"",
          ""appr"": ""Pending Approval"",
          ""payStatus"": ""Pending Approval"",
          ""amt"": 15000000,
          ""paid"": 0,
          ""balance"": 15000000,
          ""ret"": 0,
          ""wopo"": """",
          ""memo"": ""Launch campaign (no PO)""
        }
      ],
      ""invLines"": {
        ""AP/INV/TC/000901/25-26"": [
          {
            ""item"": ""XCIV001"",
            ""desc"": ""RCC M40 RA-3"",
            ""unit"": ""Cu.M"",
            ""qty"": 4000,
            ""rate"": 18500,
            ""amt"": 74000000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          }
        ],
        ""AP/INV/TC/000902/25-26"": [
          {
            ""item"": ""MCEM001"",
            ""desc"": ""OPC 53 cement"",
            ""unit"": ""Bag"",
            ""qty"": 120000,
            ""rate"": 600,
            ""amt"": 72000000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70102"",
            ""cdesc"": ""RCC Superstructure""
          }
        ],
        ""AP/INV/TC/000903/25-26"": [
          {
            ""item"": ""XFIN001"",
            ""desc"": ""Plaster & putty"",
            ""unit"": ""Sq.M"",
            ""qty"": 210000,
            ""rate"": 240,
            ""amt"": 50400000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70103"",
            ""cdesc"": ""Finishing Works""
          }
        ],
        ""AP/INV/TC/000904/25-26"": [
          {
            ""item"": ""XMKT001"",
            ""desc"": ""Digital + hoardings"",
            ""unit"": ""Lot"",
            ""qty"": 1,
            ""rate"": 15000000,
            ""amt"": 15000000,
            ""tax"": 0,
            ""bqty"": 0,
            ""wpRet"": null,
            ""code"": ""70701"",
            ""cdesc"": ""Marketing Expenses""
          }
        ]
      },
      ""vendorPerf"": [
        {
          ""name"": ""METRO INFRA PROJECTS"",
          ""cat"": ""Contractor"",
          ""orders"": 1,
          ""orderValue"": 95,
          ""billed"": 88,
          ""paid"": 0,
          ""balance"": 88,
          ""invoices"": 1,
          ""retention"": 4.4,
          ""grn"": 1,
          ""billProgress"": 93,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""ULTRATECH CEMENT LTD"",
          ""cat"": ""Material"",
          ""orders"": 1,
          ""orderValue"": 7.2,
          ""billed"": 7.2,
          ""paid"": 7.2,
          ""balance"": 0,
          ""invoices"": 1,
          ""retention"": 0,
          ""grn"": 1,
          ""billProgress"": 100,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""SUNRISE STRUCTURES"",
          ""cat"": ""Contractor"",
          ""orders"": 1,
          ""orderValue"": 62.5,
          ""billed"": 40,
          ""paid"": 30,
          ""balance"": 10,
          ""invoices"": 6,
          ""retention"": 3.1,
          ""grn"": 1,
          ""billProgress"": 64,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""GLEN INTERIORS"",
          ""cat"": ""Contractor"",
          ""orders"": 1,
          ""orderValue"": 28,
          ""billed"": 27.5,
          ""paid"": 27.5,
          ""balance"": 0,
          ""invoices"": 4,
          ""retention"": 1.4,
          ""grn"": 0,
          ""billProgress"": 98,
          ""apprPct"": 100,
          ""rejBills"": 0
        },
        {
          ""name"": ""BRIGHT MEDIA"",
          ""cat"": ""Other"",
          ""orders"": 0,
          ""orderValue"": 0,
          ""billed"": 1.5,
          ""paid"": 0,
          ""balance"": 1.5,
          ""invoices"": 1,
          ""retention"": 0,
          ""grn"": 0,
          ""billProgress"": 0,
          ""apprPct"": 0,
          ""rejBills"": 0
        }
      ],
      ""vendorInv"": {
        ""METRO INFRA PROJECTS"": [
          {
            ""no"": ""AP/INV/TC/000901/25-26"",
            ""glDate"": ""28-03-26"",
            ""dept"": ""CIVIL/EXECUTION"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Open"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 88000000,
            ""paid"": 0,
            ""balance"": 88000000,
            ""wopo"": ""PO/TC/000101/24-25""
          }
        ],
        ""ULTRATECH CEMENT LTD"": [
          {
            ""no"": ""AP/INV/TC/000902/25-26"",
            ""glDate"": ""12-02-26"",
            ""dept"": ""STORES"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 72000000,
            ""paid"": 72000000,
            ""balance"": 0,
            ""wopo"": ""PO/TC/M/000045/24-25""
          }
        ],
        ""GLEN INTERIORS"": [
          {
            ""no"": ""AP/INV/TC/000903/25-26"",
            ""glDate"": ""05-03-26"",
            ""dept"": ""FINISHING"",
            ""appr"": ""Approved"",
            ""payStatus"": ""Paid In Full"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 50400000,
            ""paid"": 50400000,
            ""balance"": 0,
            ""wopo"": ""PO/TC/000103/24-25""
          }
        ],
        ""BRIGHT MEDIA"": [
          {
            ""no"": ""AP/INV/TC/000904/25-26"",
            ""glDate"": ""20-01-26"",
            ""dept"": ""MARKETING"",
            ""appr"": ""Pending Approval"",
            ""payStatus"": ""Pending Approval"",
            ""type"": ""Vendor Invoice"",
            ""amt"": 15000000,
            ""paid"": 0,
            ""balance"": 15000000,
            ""wopo"": """"
          }
        ]
      },
      ""boq"": [
        {
          ""code"": ""XCIV-RCC"",
          ""desc"": ""70102 RCC Superstructure"",
          ""u"": ""Cu.M"",
          ""dq"": 40000,
          ""oq"": 42000,
          ""dr"": 17800,
          ""or_"": 18500,
          ""bv"": 71.2,
          ""wv"": 77.7
        },
        {
          ""code"": ""XCIV-EXC"",
          ""desc"": ""70101 Excavation"",
          ""u"": ""Cu.M"",
          ""dq"": 54000,
          ""oq"": 56000,
          ""dr"": 700,
          ""or_"": 750,
          ""bv"": 3.78,
          ""wv"": 4.2
        },
        {
          ""code"": ""XFIN-PL"",
          ""desc"": ""70103 Plaster"",
          ""u"": ""Sq.M"",
          ""dq"": 200000,
          ""oq"": 210000,
          ""dr"": 230,
          ""or_"": 240,
          ""bv"": 46,
          ""wv"": 50.4
        }
      ],
      ""boqTot"": {
        ""design"": 121,
        ""order"": 132.3
      },
      ""retention"": [
        {
          ""vendor"": ""METRO INFRA PROJECTS"",
          ""bills"": 1,
          ""held"": 4.4,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        },
        {
          ""vendor"": ""SUNRISE STRUCTURES"",
          ""bills"": 6,
          ""held"": 3.1,
          ""released"": 0,
          ""due"": ""On DLP"",
          ""hold"": ""No""
        }
      ],
      ""cr"": []
    }
  }
}";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return System.Text.Json.JsonSerializer.Deserialize<Root>(jsonPayload, options);
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
                                                         IsReal = g.First().is_real ?? false ,
                                                         Budget= Convert.ToDecimal(g.First().budget),
                                                         Committed = Convert.ToDecimal(g.First().committed),
                                                         Billed = Convert.ToDecimal(g.First().billed),
                                                         Available = Convert.ToDecimal(g.First().available),
                                                         Retention = Convert.ToDecimal(g.First().retention),
                                                         Vendors = Convert.ToInt32(g.First().vendors),
                                                         Wos = Convert.ToInt32(g.First().wos),
                                                         BoqDesign = Convert.ToDecimal(g.First().boq_design),
                                                         BoqOrder = Convert.ToDecimal(g.First().boq_order),
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
                            F = x.f ?? 0
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
    }
}
