using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Configuration;

public class ConnectionStrings : BaseConfig
{
    public ConnectionStrings(Microsoft.Extensions.Configuration.IConfigurationSection config) : base(config)
    {
    }

    [ConfigValue]
    public string Storage { get; set; } = string.Empty;

    [ConfigValue]
    public string SQLConnectionString { get; set; } = string.Empty;

    [ConfigValue]
    public string ServiceBus { get; set; } = string.Empty;

}
