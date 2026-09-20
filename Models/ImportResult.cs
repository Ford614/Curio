using System.Collections.Generic;

namespace Curio.Models
{
    public class ImportResult
    {
        public List<CursorFileInfo> ImportedFiles { get; } = new();
        public List<string> LogMessages { get; } = new();
        public int SkippedExecutablesCount { get; set; }
        public int ErrorCount { get; set; }

        public int TotalDetectedCount => ImportedFiles.Count;
    }
}
