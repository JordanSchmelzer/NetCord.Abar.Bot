using System.ComponentModel.DataAnnotations.Schema;

namespace NetCord.Abar.Bot.Models;

public class Playlist
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int UserId { get; set; }        // ✅ foreign key
    public User User { get; set; } = null!;
    public ICollection<Sound> Sounds { get; set; } = new List<Sound>();
}