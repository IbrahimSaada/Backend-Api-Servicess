namespace Backend_Api_services.Services.Interfaces
{
    public interface IAwsSettings
    {
        string AccessKeyId { get; }
        string SecretKey { get; }
        Amazon.RegionEndpoint RegionEndpoint { get; }
    }

    public interface IEnvironmentSettings
    {
        string ShortName { get; }
    }
}
