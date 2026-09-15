namespace NetCord.Abar.Bot.Database.Models;

public class Sound
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public bool IsDeleted { get; set; }

    // Relationships
    // Optional: link sounds to playlists
    public int? PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }
}