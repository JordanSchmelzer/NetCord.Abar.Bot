using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetCord.Abar.Bot.Models
{
    public class TrackMatchResultDto
    {
        private static readonly string SoundsRoot = string.Empty;
        public Dictionary<string, int> SearchResults = new();
    }
}
