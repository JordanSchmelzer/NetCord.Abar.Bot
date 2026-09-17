namespace NetCord.Abar.Bot.Models;


public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }

    // Relationships
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
}