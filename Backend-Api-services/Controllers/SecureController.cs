using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SecureController : ControllerBase
    {
        // Example secure action
        [HttpGet("data")]
        public IActionResult GetSecureData()
        {
            return Ok(new { Message = "This is a secure endpoint" });
        }
    }
}
