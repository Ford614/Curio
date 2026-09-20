using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MouseCursorCustom.Models;

namespace MouseCursorCustom.Services
{
    public static class CursorMatcher
    {
        public static List<CursorRoleMapping> MatchRoles(List<CursorFileInfo> files)
        {
            var standardRoles = CursorRole.GetStandardRoles();
            var mappings = new List<CursorRoleMapping>();
            var usedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in standardRoles)
            {
                var mapping = new CursorRoleMapping(role);

                CursorFileInfo? bestMatch = FindBestMatch(role, files, usedFilePaths);
                if (bestMatch != null)
                {
                    mapping.SelectedFile = bestMatch;
                    usedFilePaths.Add(bestMatch.FilePath);
                }

                mappings.Add(mapping);
            }

            return mappings;
        }

        private static CursorFileInfo? FindBestMatch(CursorRole role, List<CursorFileInfo> files, HashSet<string> usedFiles)
        {
            CursorFileInfo? bestMatch = null;
            int highestScore = -1;

            foreach (var file in files)
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName).ToLowerInvariant();
                int score = 0;

                foreach (var keyword in role.Keywords)
                {
                    string kw = keyword.ToLowerInvariant();

                    if (nameWithoutExt.Equals(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 100);
                    }
                    else if (nameWithoutExt.StartsWith(kw + "_") || nameWithoutExt.StartsWith(kw + "-") ||
                             nameWithoutExt.EndsWith("_" + kw) || nameWithoutExt.EndsWith("-" + kw))
                    {
                        score = Math.Max(score, 80);
                    }
                    else if (nameWithoutExt.Contains(kw))
                    {
                        score = Math.Max(score, 50);
                    }
                }

                // If this file hasn't been assigned yet, give it preference
                if (!usedFiles.Contains(file.FilePath) && score > 0)
                {
                    score += 10;
                }

                if (score > highestScore && score > 0)
                {
                    highestScore = score;
                    bestMatch = file;
                }
            }

            return bestMatch;
        }
    }
}
