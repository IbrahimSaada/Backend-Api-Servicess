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

        // Increase MaxPoints from 100 to 200 (or more) so it's harder to reach 5 stars
        private const int MaxPoints = 200;

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
                // Round rating to nearest 0.5
                user.rating = Math.Round(starRating * 2) / 2;

                // Mark only the 'rating' property as modified
                _context.Entry(user).Property(u => u.rating).IsModified = true;

                await _context.SaveChangesAsync();
            }
        }

        private async Task<int> CalculateTotalPoints(int userId)
        {
            // Retrieve all verified answers by this user
            var verifiedAnswers = await _context.answers
                .Include(a => a.questions)
                .Where(a => a.user_id == userId && a.is_verified)
                .ToListAsync();

            int totalPoints = 0;

            // Adjust the points logic (public vs. private)
            foreach (var answer in verifiedAnswers)
            {
                if (answer.questions.is_private)
                {
                    // Private question verified => 4 points
                    totalPoints += 4;
                }
                else
                {
                    // Public question verified => 2 points
                    totalPoints += 2;
                }
            }

            return totalPoints;
        }
    }
}
