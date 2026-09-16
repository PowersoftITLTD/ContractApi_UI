using ContractBudgetApi.Interface;
using System.Data;
using System.Data.SqlClient;

namespace ContractBudgetApi.DapperDbConnections
{
    public class DapperDbConnection : IDapperDbConnection
    {
        public readonly string _connectionString;

        public DapperDbConnection(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ContractAuthentication");
        }
        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
