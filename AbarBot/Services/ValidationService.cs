using Microsoft.EntityFrameworkCore;

using NetCord.Abar.Bot.Definitions.Models;


namespace NetCord.Abar.Bot.Services;

public class ValidationService(
    AbarBotDbContext db) 
{
    public async Task EnsureUserAsync(ulong userId, string discordUserName)
    {
        // Maybe put this in a validation service
        Definitions.Models.User? existingUser = await db.Users
            .Where(u => u.Id == (int)userId)
            .FirstOrDefaultAsync();

        // If user exists update them
        if (existingUser != null)
        {
            // Update user data
            existingUser.Name = discordUserName;
            if (existingUser.Name != discordUserName)
            {
                await db.SaveChangesAsync();
                return;
            }
        }
        else
        {
            await db.Users.AddAsync(new Definitions.Models.User
            {
                Id = (int)userId,
                Name = discordUserName,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
