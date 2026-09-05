using Microsoft.EntityFrameworkCore;
using NetCord.Abar.Bot.Database.Models;

public class SoundDbContext : DbContext
{
    public SoundDbContext(DbContextOptions<SoundDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sound> Sounds => Set<Sound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sound>(entity =>
        {
            entity.ToTable("Sounds");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.SearchTerm)
                  .IsRequired();

            entity.Property(s => s.ResourcePath)
                  .IsRequired(false);
        });
    }
}