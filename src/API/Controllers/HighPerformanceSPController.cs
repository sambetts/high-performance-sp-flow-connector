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
            var m = new SharePointFileMigrationManager(_config, _logger);
            await m.StartCopyAndSendToServiceBus(startCopyInfo);

            return Ok();
        }
    }
}
