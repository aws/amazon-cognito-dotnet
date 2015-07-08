using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Build.Utilities;
using System.IO;
using System.Xml;
using System.Reflection;

namespace CustomTasks
{
    public class UpdateFxCopProject : Task
    {
        public string Assemblies { get; set; }
        public string FxCopProject { get; set; }
        public string BinSuffix { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Assemblies))
                throw new ArgumentNullException("Assemblies");
            if (string.IsNullOrEmpty(FxCopProject))
                throw new ArgumentNullException("FxCopProject");
            if (string.IsNullOrEmpty(BinSuffix))
                throw new ArgumentNullException("BinSuffix");

            Assemblies = Path.GetFullPath(Assemblies);
            Log.LogMessage("Assemblies = " + Assemblies);

            FxCopProject = Path.GetFullPath(FxCopProject);
            Log.LogMessage("FxCopProject = " + FxCopProject);

            Log.LogMessage("Updating project...");
            FxCop.UpdateFxCopProject(Assemblies, FxCopProject, BinSuffix);
            Log.LogMessage("Project updated");

            return true;
        }
    }

    public static class FxCop
    {
        public static void UpdateFxCopProject(string assembliesFolder, string fxCopProjectPath, string binSuffix)
        {
            var allAssemblies = Directory.GetFiles(assembliesFolder, "*.dll").ToList();

            var doc = new XmlDocument();
            doc.Load(fxCopProjectPath);

            var referenceDirectoriesNode = doc.SelectSingleNode(AssemblyReferenceDirectoriesXpath);

            var targetsNode = doc.SelectSingleNode(TargetsXpath);
            RemoveAllNodes(doc, targetsNode, TargetXpath);
            ResetReferenceDirectories(doc, referenceDirectoriesNode, DirectoriesXpath);

            foreach (var assembly in allAssemblies)
            {
                var assemblyName = Path.GetFileName(assembly).ToLower();
                var assemblyFolderName = assemblyName.Split('.')[1];

                var newTarget = AddChildNode(targetsNode, "Target");
                AddAttribute(newTarget, "Name", MakeRelativePath(assembly));
                AddAttribute(newTarget, "Analyze", "True");

                var dirNode = AddChildNode(referenceDirectoriesNode, "Directory");

                /*
                <Target Name="$(ProjectDir)/Deployment/assemblies/net35/AWSSDK.SyncManager.dll" Analyze="True" AnalyzeAllChildren="True" />
                */
                AddAttribute(newTarget, "AnalyzeAllChildren", "True");

                // Add assembly reference directory for each service
                // <Directory>$(ProjectDir)/src/bin/Release/net35/</Directory>
                dirNode.InnerText = string.Format("$(ProjectDir)/src/bin/Release/{0}/", binSuffix);

            }
            doc.Save(fxCopProjectPath);
        }

        public static HashSet<string> NamespacePrefixesToSkip = new HashSet<string>(StringComparer.Ordinal)
        {
            "ThirdParty.BouncyCastle",
            "ThirdParty.Ionic.Zlib",
            "ThirdParty.Json",
        };
        public const string NamespacesXpath = "FxCopProject/Targets/Target/Modules/Module/Namespaces";
        public const string TargetsXpath = "FxCopProject/Targets";
        public const string AssemblyReferenceDirectoriesXpath = "FxCopProject/Targets/AssemblyReferenceDirectories";
        public const string DirectoriesXpath = "FxCopProject/Targets/AssemblyReferenceDirectories/Directory";
        public const string TargetXpath = "FxCopProject/Targets/Target";
        public const string CoreAssemblyName = "AWSSDK.Core.dll";

        public const string DeploymentPath = @"Deployment\assemblies";
        public const string ProjectDirRelative = @"$(ProjectDir)\..\";

        public static IEnumerable<string> GetNamespacesToExamine(Assembly assembly)
        {
            HashSet<string> namespaces = new HashSet<string>(StringComparer.Ordinal);

            var allTypes = assembly.GetTypes().ToList();
            foreach (var type in allTypes)
            {
                var ns = type.Namespace;
                if (ShouldSkip(ns))
                    continue;

                namespaces.Add(ns);
            }

            return namespaces;
        }

        private static bool ShouldSkip(string ns)
        {
            if (ns == null)
                return false;

            foreach (var toSkip in NamespacePrefixesToSkip)
                if (ns.StartsWith(toSkip, StringComparison.Ordinal))
                    return true;
            return false;
        }
        private static string MakeRelativePath(string assemblyPath)
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var deploymentIndex = fullPath.IndexOf(DeploymentPath, StringComparison.OrdinalIgnoreCase);
            var partialPath = fullPath.Substring(deploymentIndex);

            var relativePath = string.Concat(ProjectDirRelative, partialPath);
            return relativePath;
        }

        private static void AddAttribute(XmlNode node, string name, string value)
        {
            var doc = node.OwnerDocument;
            var attribute = doc.CreateAttribute(name);
            attribute.Value = value;
            node.Attributes.Append(attribute);
        }
        private static XmlNode AddChildNode(XmlNode parent, string name)
        {
            var doc = parent.OwnerDocument;
            var node = doc.CreateElement(name);
            parent.AppendChild(node);
            return node;
        }
        private static void RemoveAllNodes(XmlDocument doc, XmlNode targetsNode, string xpath)
        {
            var matchingNodes = doc.SelectNodes(xpath);
            foreach (XmlNode node in matchingNodes)
                targetsNode.RemoveChild(node);
        }

        private static void ResetReferenceDirectories(XmlDocument doc,
            XmlNode referenceDirectoriesNode, string xpath)
        {
            RemoveAllNodes(doc, referenceDirectoriesNode, xpath);
        }
    }
}
