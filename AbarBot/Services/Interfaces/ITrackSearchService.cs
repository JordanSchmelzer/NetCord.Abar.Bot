namespace NetCord.Abar.Bot.Services.Interfaces
{
    public interface ITrackSearchService
    {
        public IEnumerable<string> GetAudioFiles(string? folder = null);
        public Dictionary<FileInfo, int> FindSoundsLikeQuery(string query, int fuzzyLimit = 5, int howManySounds = 3);
        public string? FindBestMatchingFile(List<string> files, string query);
    }
}
