namespace ContractBudgetApi.Model
{
    public class UserModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class UserLoginModel
    {
        public int UserId { get; set; }

        public string LoginName { get; set; }

        public string? PasswordHash { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }


        //public bool IsActive { get; set; }

        //public DateTime CreatedDate { get; set; }

        //public string? ModifiedBy { get; set; }

        //public DateTime? ModifiedDate { get; set; }  
    }
}
