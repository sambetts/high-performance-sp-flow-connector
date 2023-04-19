namespace Engine.Configuration;

public class DevConfig : BaseConfig
{
    public DevConfig(Microsoft.Extensions.Configuration.IConfigurationSection config) : base(config)
    {
    }

    [ConfigValue(true)]
    public string DefaultSharePointSite { get; set; } = string.Empty;
}
