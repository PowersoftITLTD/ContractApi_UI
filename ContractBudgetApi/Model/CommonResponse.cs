namespace ContractBudgetApi.Model
{
    public class CommonResponse
    {
        public string Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
    public class CommonServicesModel<T>
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public T Data { get; set; }
    }
    public class CommonSpParameters
    {
        public int? UserId { get; set; }
        public int? BusinessGroupId { get; set; }
        public string? Attribute1 { get; set; }
        public string? Attribute2 { get; set; }
        public string? Attribute3 { get; set; }
        public string? Attribute4 { get; set; }
    }

    public class ResponseObject
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public object Data { get; set; } // You can make Data generic if needed
        public object Data1 { get; set; } // You can make Data generic if needed
    }
    public class HostEnvironment
    {
        public string env { get; set; }
    }
    public class FileSettings
    {
        public string FilePath { get; set; }
    }
}
