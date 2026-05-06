namespace LegacyApi.Models
{
    public class User
    {
        public int user_id { get; set; }          
        public string user_name { get; set; } = string.Empty;
        public string user_email { get; set; } = string.Empty;
        public string created_date { get; set; } = string.Empty;  
        public string status_code { get; set; } = "A";  
    }
}
