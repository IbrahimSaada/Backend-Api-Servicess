using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Models
{
    public class apiDbContext : DbContext
    {
        public apiDbContext(DbContextOptions<apiDbContext> options) : base(options) 
        {

        }
        public DbSet<Users> users {  get; set; }
    }
}
