using ContractBudgetApi.Interface;
using ContractBudgetApi.Model;
using ContractBudgetApi.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Text.Json;
using System.Xml;

namespace ContractBudgetApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractBudgetController : Controller
    {


        private readonly ICommonServices _commonServices;
        private readonly IConfiguration _configuration;
        private readonly IDapperDbConnection _dbConnection;
        public ContractBudgetController(ICommonServices commonServices, IConfiguration configuration, IDapperDbConnection dbConnection)
        {
            _commonServices = commonServices;
            _configuration = configuration;
            _dbConnection = dbConnection;
        }

        [HttpGet("ContractBudget")]
         [Authorize]
        [AllowAnonymous]
        public async Task<IActionResult> ContractBudgets()   // [FromBody] Root request
        {
            var commonresponse = new CommonResponse();
            try
            {
                //if (request == null || !request.Projects.Any())
                //{
                //    commonresponse.Status = "Error";
                //    commonresponse.Message = "Invalid request. No projects found.";
                //    return BadRequest(commonresponse);
                //}

                string sessionUserId = "2693"; // Replace with actual session user ID
                string parameter1 = string.Empty;
                var rootObj = await _commonServices.GetProjectDataAsync(sessionUserId, parameter1);
                //var rootObj = await _commonServices.GetBudgetSatementData();

                //var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                //string jsonString = JsonConvert.SerializeObject(request, Newtonsoft.Json.Formatting.Indented);
                //Root root = System.Text.Json.JsonSerializer.Deserialize<Root>(jsonString, options)!;
                commonresponse.Status = "Success";
                commonresponse.Message = "Request processed successfully.";
                commonresponse.Data = rootObj.Data;
                //return Ok(commonresponse);
                // Process the request and return a response

            }
            catch(Exception ex)
            {
                commonresponse.Status = "Error";
                commonresponse.Message = $"An error occurred: {ex.Message}";
                commonresponse.Data = ex.StackTrace;
            }
            return Ok(commonresponse);
            
        }
    }
}
