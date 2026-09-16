using ContractBudgetApi.Model;

namespace ContractBudgetApi.Interface
{
    public interface IAuthService
    {
        Task<string> Authenticate(string username, string password);
        Task<string> LoginRegistration(UserLoginModel userLogin);
        string DecryptPassword(string encryptedPassword, string keyString);
        byte[] UserEncryptedReponsone(UserModel userModel, string keyString);

        Task<string> VerifyingResponse(string userLogin, string Password);
        Task<int> GetUserIdbyUserName(string userName);
        // Encrypted and Decrypted The User 

        byte[] EncryptedInputbuUser(string userInput, string keyString);

        Task<IEnumerable<T>> GetDataFromSpAsync<T>(string storedProcedureName, CommonSpParameters parametersModel);
        Task<CommonServicesModel<UserLoginModel>> GetUserDetailsWhenLoginIn(string username, string password);

        //Task<string> InsertILogResponse(LogResponseObject logResponseObject);

        //string UserDecryptedResponse(string encryptedData, string keyString);
    }
}
