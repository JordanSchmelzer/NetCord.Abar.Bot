using System.ComponentModel.DataAnnotations.Schema;
namespace NetCord.Abar.Bot.Models;


public class User
{
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("last_modified_at")]
    public DateTime LastModifiedAt { get; set; }

    // Relationships
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
}