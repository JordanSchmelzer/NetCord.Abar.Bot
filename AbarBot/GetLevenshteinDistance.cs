using System;
using System.Collections.Generic;
using System.Text;


namespace NetCord.Abar.Bot
{
    internal static class StringExtensions
    {
        /// <summary>
        /// Calculates the Levenshtein Distance between two strings.
        /// Sources: 
        /// https://medium.com/@ethannam/understanding-the-levenshtein-distance-equation-for-beginners-c4285a5604f0
        /// https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560
        /// </summary>
        public static int GetLevenshteinDistance(string source, string target) // O(n*m)
        {
            var sourceLength = source.Length;
            var targetLength = target.Length;

            var matrix = new int[sourceLength + 1, targetLength + 1];

            // First calculation, if one entry is empty return full length
            if (sourceLength == 0)
            {
                return targetLength;
            }

            if (targetLength == 0)
            {
                return sourceLength;
            }

            // Initialization of matrix with row size sourceLength and columns size targetLength
            for (var i = 0; i <= sourceLength; matrix[i, 0] = i++) { }
            for (var j = 0; j <= targetLength; matrix[0, j] = j++) { }

            // Calculate rows and collumns distances
            for (var i = 1; i <= sourceLength; i++)
            {
                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }
            // return result
            return matrix[sourceLength, targetLength];
        }
    }
}
