using Microsoft.EntityFrameworkCore;
using NetCord.Abar.Bot.Models;

public class AbarBotDbContext : DbContext
{
    public AbarBotDbContext(DbContextOptions<AbarBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<Sound> Sounds => Set<Sound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Name).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastModifiedAt).IsRequired();
        });

        // Playlists
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("Playlists");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired();

            // Foreign key to User
            entity.HasOne(p => p.User)
                  .WithMany(u => u.Playlists)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Sounds
        modelBuilder.Entity<Sound>(entity =>
        {
            entity.ToTable("Sounds");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.FileName).IsRequired();

            // Optional foreign key to Playlist
            entity.HasOne(s => s.Playlist)
                  .WithMany(p => p.Sounds)
                  .HasForeignKey(s => s.PlaylistId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}