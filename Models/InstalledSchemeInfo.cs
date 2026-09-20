using System.Collections.Generic;

namespace Curio.Models
{
    public class InstalledSchemeInfo
    {
        public string Name { get; set; } = string.Empty;
        public string RawRegistryString { get; set; } = string.Empty;
        public List<string> FilePaths { get; set; } = new();
        public bool IsCurrentActive { get; set; }
        public bool IsManagedByApp { get; set; }
    }
}
