using ContractBudgetApi.Interface;
using ContractBudgetApi.Model;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace ContractBudgetApi.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ICommonServices _commonService;
        private readonly string keyString;
        public IActionResult Index()
        {
            return View();
        }
        public AuthController(IAuthService authService, SqlConnection sqlConnection, IConfiguration configuration, ICommonServices commonService)  //
        {
            _authService = authService;
            _configuration = configuration;
            //_commonService = commonServices;
            keyString = _configuration["EncryptionKey"];
            _commonService = commonService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserModel userModel)   //UserModel userModel
        {
            var keyString = _configuration["EncryptionKey"];
            var responseObject = new ResponseObject();
            try
            {
                var EncrypteduserModel = _commonService.EncryptionObje<UserModel>(userModel, keyString);
                if (userModel == null || string.IsNullOrEmpty(userModel.Username) || string.IsNullOrEmpty(userModel.Password))
                {
                    responseObject.Status = "Error";
                    responseObject.Message = "Please enter a valid username and password.";
                    return Ok(responseObject);
                    //return Ok(new { message = "Please Entry Valide User & Password " });

                }
                var token = await _authService.Authenticate(userModel.Username, userModel.Password);
                var UserEncrypted = _authService.UserEncryptedReponsone(userModel, keyString);
                if (token == null || token == "Invalid login name or password")
                {
                    responseObject.Status = "Error";
                    responseObject.Message = "Invalid username or password.";
                    return Unauthorized(responseObject);
                }
                //if (UserEncrypted == null)
                //{
                //    responseObject.Status = "Error";
                //    responseObject.Message = "Invalid user details.";
                //    return Ok(responseObject);
                //}
                //var responseObject = new { status = "Ok", Message = "Token Generate Successfully", Token = token, UserEncryptedDetails = UserEncrypted };
                //return Ok(new { Token= token , UserEncryptedDetails = UserEncrypted  ,Status= "OK"});
                //return Ok(responseObject);
                responseObject.Status = "Ok";
                responseObject.Message = "Token generated successfully.";
                responseObject.Data = new { Token = token, UserEncryptedDetails = UserEncrypted };
                return Ok(responseObject);

            }
            catch (Exception ex)
            {
                responseObject.Status = "Error";
                responseObject.Message = $"Error Due to {ex.Message}";
                //return Ok(responseObject);
                //return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
            return Ok(responseObject);
        }

        //[HttpPost("login_PS")]
        //public async Task<IActionResult> Login_PS([FromBody] CommonEncryption_Model jsonEncrypt)   //UserModel userModel
        //{
        //    var keyString = _configuration["EncryptionKey"];
        //    var responseObject = new ResponseObject();
        //    //var loginecrypt = _commonService.EncryptionObje<UserModel>(jsonEncrypt, keyString);
        //    var userModel = _commonService.DecryptObject<UserModel>(jsonEncrypt.jsonEncrypt, keyString);

        //    try
        //    {
        //        if (userModel == null || string.IsNullOrEmpty(userModel.Username) || string.IsNullOrEmpty(userModel.Password))
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = "Please enter a valid username and password.";
        //            return Ok(responseObject);
        //            //return Ok(new { message = "Please Entry Valide User & Password " });

        //        }
        //        var token = await _authService.Authenticate(userModel.Username, userModel.Password);
        //        var UserEncrypted = _authService.UserEncryptedReponsone(userModel, keyString);
        //        var UserDetails = await _authService.GetUserDetailsWhenLoginIn(userModel.Username, userModel.Password);
        //        var userEncryptedDetails = _commonService.EncryptionObje<UserLoginModel>(UserDetails.Data, keyString);
        //        var userDeEncryptedDetails = _commonService.DecryptObject<UserLoginModel>(userEncryptedDetails, keyString);
        //        if (token == null || token == "Invalid login name or password")
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = "Invalid username or password.";
        //            return Unauthorized(responseObject);
        //        }
        //        if (UserEncrypted == null)
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = "Invalid user details.";
        //            return Ok(responseObject);
        //        }
        //        //var responseObject = new { status = "Ok", Message = "Token Generate Successfully", Token = token, UserEncryptedDetails = UserEncrypted };
        //        //return Ok(new { Token= token , UserEncryptedDetails = UserEncrypted  ,Status= "OK"});
        //        //return Ok(responseObject);
        //        responseObject.Status = "Ok";
        //        responseObject.Message = "Token generated successfully.";
        //        responseObject.Data = new { Token = token, UserEncryptedDetails = UserEncrypted, User = userEncryptedDetails };
        //        return Ok(responseObject);

        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        //    }
        //}

        //[HttpPost("Login/Registration")]

        //public async Task<IActionResult> LoginRegistration(UserLoginModel userLogin)   //, jsonEncryptModel jsonEncrypt
        //{
        //    var responseObject = new ResponseObject();
        //    try
        //    {
        //        //var userLogin = _commonService.DecryptObject<UserLoginModel>(jsonEncrypt.jsonEncrypt, keyString);
        //        if (userLogin == null || string.IsNullOrEmpty(userLogin.LoginName) || string.IsNullOrEmpty(userLogin.PasswordHash))
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = "Please enter a valid LoginName and password.";
        //            return Ok(responseObject);
        //            //return Ok(new { message = "Please Entry Valide User & Password " });
        //        }

        //        var result = await _authService.LoginRegistration(userLogin);
        //        if (result.Contains("Success"))
        //        {
        //            // Return result as an Ok response
        //            responseObject.Status = "Ok";
        //            responseObject.Message = result;
        //            //responseObject.Data = userLogin;
        //            return Ok(responseObject);
        //        }
        //        else
        //        {
        //            // Return result as an Ok response
        //            responseObject.Status = "Error";
        //            responseObject.Message = result;
        //            responseObject.Data = userLogin;
        //            return Ok(responseObject);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //        responseObject.Status = "Error";
        //        responseObject.Message = "An error occurred during login/registration.";
        //        responseObject.Data = new { error = ex.Message };
        //        return StatusCode(500, responseObject);
        //        // Log and return error response
        //        // return StatusCode(500, new { message = "An error occurred", error = ex.Message });

        //    }
        //}

        //[HttpPost("Login/Registration_PS")]

        //public async Task<IActionResult> LoginRegistration_PS([FromBody] CommonEncryption_Model jsonEncrypt)   //jsonEncryptModel jsonEncrypt   UserLoginModel userLogin
        //{
        //    var responseObject = new ResponseObject();
        //    try
        //    {
        //        //var EncryptionLoginRegistration= _commonService.EncryptionObje<UserLoginModel>(userLogin, keyString);
        //        var userLogin = _commonService.DecryptObject<UserLoginModel>(jsonEncrypt.jsonEncrypt, keyString);
        //        if (userLogin == null || string.IsNullOrEmpty(userLogin.LoginName) || string.IsNullOrEmpty(userLogin.PasswordHash))
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = "Please enter a valid LoginName and password.";
        //            return Ok(responseObject);
        //            //return Ok(new { message = "Please Entry Valide User & Password " });
        //        }

        //        var result = await _authService.LoginRegistration(userLogin);
        //        if (result.Contains("Success"))
        //        {
        //            // Return result as an Ok response
        //            responseObject.Status = "Ok";
        //            responseObject.Message = result;
        //            responseObject.Data = jsonEncrypt;
        //            return Ok(responseObject);
        //        }
        //        else
        //        {
        //            // Return result as an Ok response
        //            responseObject.Status = "Error";
        //            responseObject.Message = result;
        //            responseObject.Data = jsonEncrypt;
        //            return Ok(responseObject);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //        responseObject.Status = "Error";
        //        responseObject.Message = "An error occurred during login/registration.";
        //        responseObject.Data = new { error = ex.Message };
        //        return StatusCode(500, responseObject);
        //        // Log and return error response
        //        // return StatusCode(500, new { message = "An error occurred", error = ex.Message });

        //    }
        //}

        //[Authorize]
        //[HttpGet("UserDecryptedPasswordVerifying")]

        //public async Task<IActionResult> UserDecryptedPasswordVerifying(string Password)
        //{
        //    var responseObject = new ResponseObject();
        //    var userModel = new UserModel();
        //    try
        //    {
        //        //var Passwordhash = Convert.ToByte(Password);
        //        var keyString = _configuration["EncryptionKey"];
        //        var PassworsHash = _authService.DecryptPassword(Password, keyString);
        //        if (PassworsHash != null)
        //        {
        //            string[] strDatat = PassworsHash.Split(':');
        //            if (strDatat.Length >= 0)
        //            {
        //                userModel = new UserModel
        //                {
        //                    Username = strDatat[0],
        //                    Password = strDatat[1]
        //                };
        //            }
        //        }
        //        var result = await _authService.VerifyingResponse(userModel.Username, userModel.Password);
        //        if (result == "User successfully logged in")
        //        {
        //            responseObject.Status = "Ok";
        //            responseObject.Message = "User successfully Decrypted logged in Credential";
        //            responseObject.Data = userModel;
        //            //return Ok(new { Message = userModel, Status = "Ok" });
        //            return Ok(responseObject);
        //        }
        //        else
        //        {
        //            responseObject.Status = "Error";
        //            responseObject.Message = result;
        //            responseObject.Data = userModel;
        //            //return Ok(new { Message = userModel, Status = "Ok" });
        //            return Ok(responseObject);
        //        }

        //        //return Ok(new { message = result, Status = "Ok" });
        //    }
        //    catch (Exception ex)
        //    {

        //        responseObject.Status = "Error";
        //        responseObject.Message = "An error occurred during login/registration.";
        //        responseObject.Data = new { error = ex.Message };
        //        return StatusCode(500, responseObject);
        //        //throw;
        //        // return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        //    }
        //}
    }
}
