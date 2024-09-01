using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs_Admin;
using Backend_Api_services.Models.Entites_Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

[Route("api/admin/[controller]")]
[ApiController]
[Authorize(Roles = "admin,superadmin")]  // Require the user to be either "admin" or "superadmin"
public class UserManagementControllerAdmin : ControllerBase
{
    private readonly apiDbContext _context;

    public UserManagementControllerAdmin(apiDbContext context)
    {
        _context = context;
    }

    // GET: api/admin/UserManagement/ViewUsers
    [HttpGet("ViewUsers")]
    public async Task<IActionResult> ViewUsers([FromQuery] UserFilterDTO filter)
    {
        IQueryable<Users> query = _context.users;

        // Apply time filter if provided
        if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            query = query.Where(u => u.verified_at >= filter.StartDate && u.verified_at <= filter.EndDate);
        }

        var users = await query.Select(u => new UserManagementDTO
        {
            UserId = u.user_id,
            Username = u.username,
            Email = u.email,
            ProfilePic = u.profile_pic,
            Bio = u.bio,
            Rating = u.rating,
            PhoneNumber = u.phone_number,
            VerifiedAt = u.verified_at,
            Dob = u.dob,
            Gender = u.gender,
            Fullname = u.fullname
        }).ToListAsync();

        return Ok(users);
    }

    // PUT: api/admin/UserManagement/UpdateUser/5
    [HttpPut("UpdateUser/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserManagementDTO userDetailsDTO)
    {
        if (id != userDetailsDTO.UserId)
        {
            return BadRequest("User ID mismatch.");
        }

        var user = await _context.users.FindAsync(id);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Update user details
        user.username = userDetailsDTO.Username;
        user.email = userDetailsDTO.Email;
        user.profile_pic = userDetailsDTO.ProfilePic;
        user.bio = userDetailsDTO.Bio;
        user.rating = userDetailsDTO.Rating;
        user.phone_number = userDetailsDTO.PhoneNumber;
        user.verified_at = userDetailsDTO.VerifiedAt.HasValue
                          ? DateTime.SpecifyKind(userDetailsDTO.VerifiedAt.Value, DateTimeKind.Utc)
                          : (DateTime?)null;
        user.dob = DateTime.SpecifyKind(userDetailsDTO.Dob, DateTimeKind.Utc);
        user.gender = userDetailsDTO.Gender;
        user.fullname = userDetailsDTO.Fullname;

        _context.Entry(user).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.users.Any(u => u.user_id == id))
            {
                return NotFound("User not found.");
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // DELETE: api/admin/UserManagement/DeleteUser/5
    [HttpDelete("DeleteUser/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.users.FindAsync(id);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        _context.users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok("User deleted successfully.");
    }
}
