namespace Backend_Api_services.Services.Interfaces
{
    public class AwsSettings : IAwsSettings
    {
        public string AccessKeyId { get; set; }
        public string SecretKey { get; set; }
        public Amazon.RegionEndpoint RegionEndpoint { get; set; }
    }
}