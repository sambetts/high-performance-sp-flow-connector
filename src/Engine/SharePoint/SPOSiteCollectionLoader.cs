using Engine.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.SharePoint
{
    public class SPOSiteCollectionLoader : BaseSharePointConnector, ISiteCollectionLoader<ListItemCollectionPosition>
    {
        public SPOSiteCollectionLoader(Config config, string siteUrl, ILogger tracer) : base(new SPOTokenManager(config, siteUrl, tracer), tracer)
        {
        }

        public async Task<List<IWebLoader<ListItemCollectionPosition>>> GetWebs()
        {
            var webs = new List<IWebLoader<ListItemCollectionPosition>>();

            var spClient = await TokenManager.GetOrRefreshContext();
            var rootWeb = spClient.Web;
            await TokenManager.EnsureContextWebIsLoaded(spClient);
            spClient.Load(rootWeb.Webs);
            await spClient.ExecuteQueryAsyncWithThrottleRetries(Tracer);


            return webs;
        }
    }


}
