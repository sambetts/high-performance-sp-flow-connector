using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Configuration;

public class AzureAdConfig : BaseConfig
{
    public AzureAdConfig(Microsoft.Extensions.Configuration.IConfigurationSection config) : base(config)
    {
    }

    [ConfigValue]
    public string? Secret { get; set; } = string.Empty;

    [ConfigValue]
    public string? ClientID { get; set; } = string.Empty;

    [ConfigValue]
    public string? TenantId { get; set; } = string.Empty;
}

