using CognitoSyncGenerator.Templates;
using CognitoSyncGenerator.Templates.SourceFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CognitoSyncGenerator
{
    public class GeneratorDriver
    {

        private GeneratorOptions options;
        private ManifestConfig manifestConfig;
        private const string Prefix = "AWSSDK";

        private string sourceFolder;

        public GeneratorDriver(GeneratorOptions options)
        {
            this.options = options;

            sourceFolder = Path.Combine(options.SdkRootFolder, "src");
        }

        public void Execute()
        {
            manifestConfig = ManifestConfig.FromFile(Path.GetFullPath(options.Manifest));

            ExecuteNugetFileGenerators();
        }

        private void ExecuteNugetFileGenerators()
        {
            ExecuteNuspecFileGenerator(new NuSpec());
            ExecutePackagesConfigFileGenerator(new PackagesConfig());
        }

        private void ExecuteNuspecFileGenerator(BaseGenerator generator)
        {
            generator.Config = manifestConfig;
            var text = generator.TransformText();
            var nuspecFilename = string.Format("{0}.{1}.{2}", Prefix, manifestConfig.AssemblyName, "nuspec");
            WriteFile(sourceFolder, string.Empty, nuspecFilename, text);
        }

        private void ExecutePackagesConfigFileGenerator(BaseGenerator generator)
        {
            generator.Config = manifestConfig;
            var text = generator.TransformText();
            WriteFile(sourceFolder, string.Empty, "packages.config", text);
        }

        /// <summary>
        /// Writes the contents to disk. The content will by default be trimmed of all white space and 
        /// all tabs are replaced with spaces to make the output consistent.
        /// </summary>
        /// <param name="baseOutputDir">The root folder for the owning service's generated files</param>
        /// <param name="subNamespace">An optional sub namespace under the service. (e.g. Model or Model.Internal.MarshallTransformations)</param>
        /// <param name="filename">Filename to right to</param>
        /// <param name="content">The contents to write to the file</param>
        /// <param name="trimWhitespace"></param>
        /// <param name="replaceTabs"></param>
        /// <returns>Returns false if the file already exists and has the same content.</returns>
        internal static bool WriteFile(string baseOutputDir,
                                       string subNamespace,
                                       string filename,
                                       string content,
                                       bool trimWhitespace = true,
                                       bool replaceTabs = true)
        {
            var outputDir = !string.IsNullOrEmpty(subNamespace)
                ? Path.Combine(baseOutputDir, subNamespace.Replace('.', '\\'))
                : baseOutputDir;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var cleanContent = trimWhitespace ? content.Trim() : content;
            if (replaceTabs)
                cleanContent = cleanContent.Replace("\t", "    ");

            var filePath = Path.Combine(outputDir, filename);
            if (File.Exists(filePath))
            {
                var existingContent = File.ReadAllText(filePath);
                if (string.Equals(existingContent, cleanContent))
                    return false;
            }

            File.WriteAllText(filePath, cleanContent);
            Console.WriteLine("...created/updated {0}", filename);
            return true;
        }

    }
}
