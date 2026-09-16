using System.Data;

namespace ContractBudgetApi.Interface
{
    public interface IDapperDbConnection
    {
        public IDbConnection CreateConnection();
    }
}
