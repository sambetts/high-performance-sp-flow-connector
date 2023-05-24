
namespace Engine.Configuration;

public class AzureAdConfig : BaseConfig
{
    public AzureAdConfig(Microsoft.Extensions.Configuration.IConfigurationSection config) : base(config)
    {
    }

    [ConfigValue]
    public string ClientSecret { get; set; } = string.Empty;

    [ConfigValue]
    public string ClientId { get; set; } = string.Empty;

    [ConfigValue]
    public string TenantId { get; set; } = string.Empty;
}

