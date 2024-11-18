using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.Entities;

namespace Backend_Api_services.Services.RatingService
{
    public class RatingService
    {
        private readonly apiDbContext _context;
        private const int MaxPoints = 100; // Maximum points for 5 stars

        public RatingService(apiDbContext context)
        {
            _context = context;
        }

        public async Task UpdateUserRating(int userId)
        {
            // Calculate total points for the user
            var totalPoints = await CalculateTotalPoints(userId);

            // Calculate star rating
            double starRating = (double)totalPoints / MaxPoints * 5;

            // Ensure rating does not exceed 5 stars
            if (starRating > 5)
                starRating = 5;

            // Update user's rating in the database
            var user = await _context.users.FindAsync(userId);
            if (user != null)
            {
                user.rating = Math.Round(starRating * 2) / 2; // Round to nearest 0.5

                // Mark only the 'rating' property as modified
                _context.Entry(user).Property(u => u.rating).IsModified = true;

                await _context.SaveChangesAsync();
            }
        }

        private async Task<int> CalculateTotalPoints(int userId)
        {
            // Retrieve all verified ***REMOVED***s by the user
            var verified***REMOVED***s = await _context.***REMOVED***s
                .Include(a => a.***REMOVED***s)
                .Where(a => a.user_id == userId && a.is_verified)
                .ToListAsync();

            int totalPoints = 0;

            // Calculate points based on ***REMOVED*** type
            foreach (var ***REMOVED*** in verified***REMOVED***s)
            {
                if (***REMOVED***.***REMOVED***s.is_private)
                    totalPoints += 20; // Private ***REMOVED***
                else
                    totalPoints += 10; // Public ***REMOVED***
            }

            return totalPoints;
        }
    }
}
