using Backend_Api_services.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin,superadmin")]  // Ensure only admins or superadmins can access this controller
    public class PostManagementController : ControllerBase
    {
        private readonly apiDbContext _context;

        public PostManagementController(apiDbContext context)
        {
            _context = context;
        }

        // GET: api/PostManagement/Count
        [HttpGet("count")]
        public async Task<IActionResult> GetPostCount()
        {
            // Get the total number of posts in the system
            var postCount = await _context.Posts.CountAsync();

            return Ok(new { TotalPosts = postCount });
        }
    }
}
