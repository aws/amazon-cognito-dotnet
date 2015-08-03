using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace CognitoSyncGenerator
{
    public class ManifestConfig
    {
        public static ManifestConfig FromFile(string path)
        {
            return JsonConvert.DeserializeObject<ManifestConfig>(File.ReadAllText(path));
        }

        public string PackagesPath
        {
            get
            {
                return @"..\packages";
            }
        }

        public string AssemblyName { get; set; }

        public string Version { get; set; }

        public bool InPreview { get; set; }

        public string ServiceVersion
        {
            get
            {
                var fileVersion = new Version(Version);
                var version = new Version(fileVersion.Major, fileVersion.Minor);
                var text = version.ToString();
                return text;
            }
        }

        public List<Dependency> Dependencies { get; set; }

        public class Dependency
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public bool InPreview { get; set; }
            public bool Primary { get; set; }
            public bool TestOnly { get; set; }
            public Dictionary<string, string> ReferencePaths { get; set; }
        }

    }
}
