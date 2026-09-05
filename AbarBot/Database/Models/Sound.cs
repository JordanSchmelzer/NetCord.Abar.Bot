using System;
using System.Collections.Generic;
using System.Text;


namespace NetCord.Abar.Bot.Database.Models
{
    public class Sound
    {
        public int Id { get; set; }
        public string SearchTerm { get; set; }
        public string? ResourcePath { get; set; }
    }
}
