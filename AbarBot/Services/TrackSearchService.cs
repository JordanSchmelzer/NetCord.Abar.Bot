using NetCord.Abar.Bot.Services.Interfaces;


namespace NetCord.Abar.Bot.Services
{
    public class TrackSearchService: ITrackSearchService
    {
        private static string SoundsRoot = string.Empty;

        public TrackSearchService(string soundRoot)
        {
            SoundsRoot = soundRoot;
        }

        // Get all sounds from a folder or the sounds root
        public IEnumerable<string> GetAudioFiles(string? folder = null)
        {
            folder ??= SoundsRoot;

            return Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));
        }

        // Just get the top matching file using defaults
        public string? FindBestMatchingFile(List<string> files, string query)
        {
            query = query.ToLowerInvariant();

            // Extract just the filename without extension
            var candidates = files.Select(f => new
            {
                Path = f,
                Name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant()
            }).ToList();

            // 1. Exact match
            var exact = candidates.FirstOrDefault(c => c.Name == query);
            if (exact != null)
                return exact.Path;

            // 2. Starts-with match
            var starts = candidates.FirstOrDefault(c => c.Name.StartsWith(query));
            if (starts != null)
                return starts.Path;

            // 3. Contains match
            var contains = candidates.FirstOrDefault(c => c.Name.Contains(query));
            if (contains != null)
                return contains.Path;

            // 4. Levenshtein fuzzy match
            var best = candidates
                .Select(c => new { c.Path, Score = LevenshteinDistance(c.Name, query) })
                .OrderBy(c => c.Score)
                .FirstOrDefault();

            // Accept fuzzy match only if reasonably close
            return best?.Score < 5 ? best.Path : null;
        }

        // Configurable search for sounds
        public Dictionary<FileInfo, int> FindSoundsLikeQuery(string query, int fuzzyLimit = 5, int howManySounds = 3)
        {
            var results = new Dictionary<FileInfo, int>();

            // Get all audio files in the root folder
            var files = GetAudioFiles(SoundsRoot).ToList();

            if (files.Count == 0)
                return results;

            query = query.ToLowerInvariant();

            // Compute fuzzy score for each file
            var scored = files
                .Select(path =>
                {
                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    int score = LevenshteinDistance(name, query);
                    return new { Path = path, Score = score };
                })
                .Where(x => x.Score <= fuzzyLimit) // Only keep reasonably close matches
                .OrderBy(x => x.Score)             // Lower score = better match
                .Take(howManySounds)               // Limit results
                .ToList();

            foreach (var item in scored)
                results[new FileInfo(item.Path)] = item.Score;

            return results;
        }

        // String Fuzzy Match Closeness
        private int LevenshteinDistance(string a, string b)
        {
            int[,] dp = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                dp[i, 0] = i;

            for (int j = 0; j <= b.Length; j++)
                dp[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }

            return dp[a.Length, b.Length];
        }
    }
}
