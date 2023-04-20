using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.SharePoint;

public abstract class BaseSharePointConnector
{
    private readonly SPOTokenManager tokenManager;
    private readonly ILogger tracer;

    public BaseSharePointConnector(SPOTokenManager tokenManager, ILogger tracer)
    {
        this.tokenManager = tokenManager;
        this.tracer = tracer;
    }

    public ILogger Tracer => tracer;
    public SPOTokenManager TokenManager => tokenManager;
}
