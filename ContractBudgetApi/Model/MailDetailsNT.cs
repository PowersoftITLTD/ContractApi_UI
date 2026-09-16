using System.Text.Json.Serialization;

namespace ContractBudgetApi.Model
{
    public class MailDetailsNT
    {
        [JsonPropertyName("MKEY")]
        public string? MKEY { get; set; }
        [JsonPropertyName("MAIL_TYPE")]
        public string? MAIL_TYPE { get; set; }
        [JsonPropertyName("MAIL_FROM")]
        public string? MAIL_FROM { get; set; }
        [JsonPropertyName("MAIL_DISPLAY_NAME")]
        public string? MAIL_DISPLAY_NAME { get; set; }
        [JsonPropertyName("MAIL_PRIORITY")]
        public string? MAIL_PRIORITY { get; set; }
        [JsonPropertyName("SMTP_PORT")]
        public string? SMTP_PORT { get; set; }
        [JsonPropertyName("SMTP_HOST")]
        public string? SMTP_HOST { get; set; }
        [JsonPropertyName("SMTP_PASS")]
        public string? SMTP_PASS { get; set; }
        [JsonPropertyName("SMTP_ESSL")]
        public string? SMTP_ESSL { get; set; }
        [JsonPropertyName("SMTP_TIMEOUT")]
        public string? SMTP_TIMEOUT { get; set; }
        [JsonPropertyName("MAIL1")]
        public string? MAIL1 { get; set; }
        [JsonPropertyName("MAIL2")]
        public string? MAIL2 { get; set; }
        [JsonPropertyName("MAIL3")]
        public string? MAIL3 { get; set; }
        [JsonPropertyName("CREATED_BY")]
        public string? CREATED_BY { get; set; }
        [JsonPropertyName("CREATION_DATE")]
        public string? CREATION_DATE { get; set; }
        [JsonPropertyName("LAST_UPDATE_DATE")]
        public string? LAST_UPDATE_DATE { get; set; }
        [JsonPropertyName("LAST_UPDATED_BY")]
        public string? LAST_UPDATED_BY { get; set; }
        [JsonPropertyName("DELETE_FLAG")]
        public string? DELETE_FLAG { get; set; }
        [JsonPropertyName("ATTRIBUTE1")]
        public string? ATTRIBUTE1 { get; set; }
        [JsonPropertyName("ATTRIBUTE2")]
        public string? ATTRIBUTE2 { get; set; }
    }
}
