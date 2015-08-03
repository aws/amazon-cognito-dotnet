using CognitoSyncGenerator.Templates;
using CognitoSyncGenerator.Templates.Component;
using CognitoSyncGenerator.Templates.ProjectFiles;
using CognitoSyncGenerator.Templates.SourceFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            ExecuteGenerator45(new Net45ProjectTemplate());

            ExecuteGeneratorPCL(new PCLProjectTemplate());

            ExecuteGeneratorIOS(new IOSProjectTemplate());

            ExecuteGeneratorAssemblyInfo(new AssemblyInfo());

            ExecuteNugetFileGenerators();

            GenerateXamarinComponents();

            UpdateOtherProjects();

            UpdatePackageConfigs();

        }

        private void ExecuteGenerator45(BaseGenerator generator)
        {
            generator.Config = manifestConfig;
            var text = generator.TransformText();
            var projFilename = string.Format("{0}.{1}.Net45.{2}", Prefix, manifestConfig.AssemblyName, "csproj");
            WriteFile(sourceFolder, string.Empty, projFilename, text);
        }

        private void ExecuteGeneratorPCL(BaseGenerator generator)
        {
            generator.Config = manifestConfig;
            var text = generator.TransformText();
            var projFilename = string.Format("{0}.{1}.PCL.{2}", Prefix, manifestConfig.AssemblyName, "csproj");
            WriteFile(sourceFolder, string.Empty, projFilename, text);
        }

        private void ExecuteGeneratorIOS(BaseGenerator generator)
        {
            generator.Config = manifestConfig;
            var text = generator.TransformText();
            var projFilename = string.Format("{0}.{1}.iOS.{2}", Prefix, manifestConfig.AssemblyName, "csproj");
            WriteFile(sourceFolder, string.Empty, projFilename, text);
        }

        private void ExecuteGeneratorAssemblyInfo(BaseGenerator generator)
        {
            generator.Config = this.manifestConfig;
            var text = generator.TransformText();
            WriteFile(sourceFolder, "Properties", "AssemblyInfo.cs", text);
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

        private void GenerateXamarinComponents()
        {
            var componentsFolder = Path.Combine(options.SdkRootFolder, "xamarin-component", "CognitoSync");
            BaseGenerator generator = new Component() { Config = manifestConfig };
            var text = ConvertHtmlToMarkDown(generator.TransformText());
            WriteFile(componentsFolder, string.Empty, "component.yaml", text);

            generator = new Details() { Config = manifestConfig };
            text = ConvertHtmlToMarkDown(generator.TransformText());
            WriteFile(componentsFolder, string.Empty, "Details.md", text);

            generator = new GettingStarted() { Config = manifestConfig };
            text = ConvertHtmlToMarkDown(generator.TransformText());
            WriteFile(componentsFolder, string.Empty, "GettingStarted.md", text);
        }

        string ConvertHtmlToMarkDown(string text)
        {
            var htmlText = text.Replace("<fullname>", "<h1>").Replace("</fullname>", "</h1>");
            htmlText = htmlText.Replace("<note>", "<i>").Replace("</note>", "</i>");
            var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config(unknownTagsConverter: "raise", githubFlavored: true));
            try
            {
                var markdownText = converter.Convert(htmlText);
                return markdownText;
            }
            catch (Exception e)
            {
                Console.WriteLine("html text = {0}", text);
                Console.WriteLine(e.StackTrace);
                throw e;
            }

        }

        private void UpdateOtherProjects()
        {
            Dictionary<string, string> regexMatches = new Dictionary<string, string>();
            foreach (var d in manifestConfig.Dependencies)
            {
                regexMatches.Add(@"\\(" + Prefix + "." + d.Name + @")[.0-9]{8}([-a-z]{8})?\\", string.Format("\\{0}.{1}.{2}{3}\\", Prefix, d.Name, d.Version, d.InPreview ? BaseGenerator.PreviewFlag : ""));
            }

            var syncManagerExp = @"\\(" + Prefix + "." + manifestConfig.AssemblyName + @")[.0-9]{8}([-a-z]{8})?\\";
            var syncManagerReplaceExp = string.Format(@"\{0}.{1}.{2}{3}\", Prefix, manifestConfig.AssemblyName, manifestConfig.Version, manifestConfig.InPreview ? BaseGenerator.PreviewFlag : "");
            regexMatches.Add(syncManagerExp, syncManagerReplaceExp);


            foreach (var proj in manifestConfig.OtherProjects)
            {
                var file = Path.Combine(options.SdkRootFolder, proj);
                if (File.Exists(file))
                {
                    string projContent = File.ReadAllText(file, Encoding.UTF8);
                    foreach (var kvp in regexMatches)
                    {
                        Regex regex = new Regex(kvp.Key);
                        projContent = regex.Replace(projContent, kvp.Value);
                    }

                    File.WriteAllText(file, projContent, Encoding.UTF8);
                    Console.WriteLine("..created/Updated {0}", file);
                }
                else
                {
                    throw new Exception(string.Format("file {0} doesnot exist", file));
                }
            }

        }

        private void UpdatePackageConfigs()
        {
            Dictionary<string, string> regexMatches = new Dictionary<string, string>();
            foreach (var d in manifestConfig.Dependencies)
            {
                var exp = @"id=""" + Prefix + "." + d.Name + @"""\sversion=""[.0-9]{7}""";
                var replaceExp = string.Format(@"id=""{0}.{1}"" version=""{2}{3}""", Prefix, d.Name, d.Version, d.InPreview ? BaseGenerator.PreviewFlag : "");
                regexMatches.Add(exp, replaceExp);
            }

            var syncManagerExp = @"id=""" + Prefix + "." +manifestConfig.AssemblyName + @"""\sversion=""[.0-9]{7}""";
            var syncManagerReplaceExp = string.Format(@"id=""{0}.{1}"" version=""{2}{3}""", Prefix, manifestConfig.AssemblyName, manifestConfig.Version, manifestConfig.InPreview ? BaseGenerator.PreviewFlag : "");
            regexMatches.Add(syncManagerExp, syncManagerReplaceExp);

            foreach (var pkg in manifestConfig.PackagesFiles)
            {
                var file = Path.Combine(options.SdkRootFolder, pkg);
                if (File.Exists(file))
                {
                    string pkgContent = File.ReadAllText(file, Encoding.UTF8);
                    foreach (var kvp in regexMatches)
                    {
                        Regex regex = new Regex(kvp.Key);
                        pkgContent = regex.Replace(pkgContent, kvp.Value);
                    }

                    File.WriteAllText(file, pkgContent, Encoding.UTF8);
                    Console.WriteLine("..created/Updated {0}", file);
                }
                else
                {
                    throw new Exception(string.Format("file {0} doesnot exist", file));
                }
            }

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
