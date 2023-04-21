using Engine.Configuration;
using Engine;
using Engine.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Engine.SharePoint;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class HighPerformanceSPController : ControllerBase
    {
        private readonly ILogger<HighPerformanceSPController> _logger;
        private readonly Config _config;

        public HighPerformanceSPController(ILogger<HighPerformanceSPController> logger, Config config)
        {
            _logger = logger;
            _config = config;
        }

        [HttpPost(Name = nameof(StartCopy))]
        public async Task<IActionResult> StartCopy([FromBody] StartCopyRequest startCopyInfo)
        {
            if (startCopyInfo == null)
            {
                return BadRequest();
            }

            var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
            var spClient = await sourceTokenManager.GetOrRefreshContext();
            var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);

            var guid = await SPOListLoader.GetList(sourceInfo, spClient, _logger);

            var m = new FileMigrationStartManager(_config, _logger);
            var r = await m.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger), new SBFileResultManager(_config, _logger));


            return Ok();
        }
    }
}
