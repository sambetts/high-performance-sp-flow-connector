namespace Engine.Configuration;


public class Config : BaseConfig
{
    public Config(Microsoft.Extensions.Configuration.IConfiguration config) : base(config)
    {
    }

    [ConfigValue]
    public string BaseSPOAddress { get; set; } = string.Empty;


    [ConfigValue]
    public string BaseFunctionsAppAddress { get; set; } = string.Empty;

    [ConfigValue]
    public string KeyVaultUrl { get; set; } = string.Empty;

    [ConfigSection("AzureAd")]
    public AzureAdConfig AzureAdConfig { get; set; } = null!;

    [ConfigValue]
    public string QueueNameOperations { get; set; } = null!;

    [ConfigSection("ConnectionStrings")]
    public ConnectionStrings ConnectionStrings { get; set; } = null!;

}
