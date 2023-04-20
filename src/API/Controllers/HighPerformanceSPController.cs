using Engine.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class HighPerformanceSPController : ControllerBase
    {
        private readonly ILogger<HighPerformanceSPController> _logger;

        public HighPerformanceSPController(ILogger<HighPerformanceSPController> logger)
        {
            _logger = logger;
        }

        [HttpPost(Name = nameof(StartCopy))]
        public IActionResult StartCopy([FromBody] StartCopyRequest startCopyInfo)
        {
            if (startCopyInfo == null)
            {
                return BadRequest();
            }

    
            return Ok();
        }
    }
}
