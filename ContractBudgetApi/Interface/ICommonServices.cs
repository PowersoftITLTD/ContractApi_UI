using ContractBudgetApi.Model;

namespace ContractBudgetApi.Interface
{
    public interface ICommonServices
    {
        byte[] GetKey(string keyString, int requiredLength);
        string EncryptionObje<T>(T obj, string keyString);
        T DecryptObject<T>(string encryptedBase64, string keyString);
        // Decrypt Files Method from Encrypt To Decryption Way
        byte[] DecryptFileBytes(string encryptedFileBase64, string keyString);
        string SendEmail(string sp_to, string sp_cc, string sp_bcc, string sp_subject, string sp_body, string sp_mailtype, string sp_display_name, List<string> lp_attachment, MailDetailsNT mailDetailsNT);

        //Task<string> InsertInvoice_DOC_TRl(DocumentUploadModel documentUpload);
        //Task<string> UpdateInvoice_DOC_TRl(DocumentUploadModel documentUpload);
        //Task<InvoiceDocDto> GetInvoiceDocAsync(decimal mkey, decimal srNo);


        ////Old Code Already Commented
        //byte[] EncryptBytes(byte[] data, string keyString);

        //Task<List<string>> EncryptFileDataWithNamesAsync(List<IFormFile> files, string keyString);
        //byte[] DecryptSecure(byte[] encryptedData, string keyString);
        //Task<T> DecryptObject_Angular<T>(string encryptedData);
        // Mapping DocumentUploadModel to DocumentUploadCryptoDto
        // Task<DocumentUploadCryptoDto> MapToCryptoDto(DocumentUploadModel model);
        //List<(string FileName, byte[] FileBytes)> DecryptFileDataWithNamesList(List<string> encryptedFilesBase64, string keyString);
       // Task<Root> GetBudgetSatementData();
        Task<CommonResponse> GetProjectDataAsync(string sessionUserId, string parameter1);
    }
}
