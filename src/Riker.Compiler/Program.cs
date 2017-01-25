using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Riker
{
    internal static class Program
    {
        private static void Main()
        {
            Task.Run(async () =>
            {
                const string path = "../../../Riker.sln";

                if (File.Exists(path) == false)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Could not find solution file at: '{0}'.", path);
                    Console.ResetColor();
                    return;
                }

                var workspace = ToInMemorySolution(await MSBuildWorkspace.Create().OpenSolutionAsync(path));

                {
                    //var document1 = workspace.CurrentSolution.Projects.First().Documents.First();
                    //var document2 = document1.WithText(SourceText.From("Invalid Code################", Encoding.UTF8));

                    //workspace.TryApplyChanges(document2.Project.Solution);
                }

                var diagnostics = await CompileSolution(workspace.CurrentSolution);

                foreach (var item in diagnostics)
                {
                    Console.WriteLine(item);
                }

            }).Wait();
        }

        private static Workspace ToInMemorySolution(Solution solution)
        {
            var workspace = new AdhocWorkspace();

            workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));

            foreach (var project in solution.Projects)
            {
                var projectInfo = ProjectInfo.Create(
                    id                 : ProjectId.CreateNewId(),
                    version            : VersionStamp.Default,
                    name               : project.Name,
                    assemblyName       : project.AssemblyName,
                    language           : project.Language,
                    filePath           : project.FilePath,
                    outputFilePath     : project.OutputFilePath,
                    compilationOptions : project.CompilationOptions,
                    metadataReferences : project.MetadataReferences,
                    analyzerReferences : project.AnalyzerReferences,
                    parseOptions       : project.ParseOptions,
                    projectReferences  : project.ProjectReferences);

                workspace.AddProject(projectInfo);

                foreach (var document in project.Documents)
                {
                    workspace.AddDocument(projectInfo.Id, document.Name, SourceText.From(File.ReadAllText(document.FilePath), encoding: Encoding.UTF8));
                }
            }

            return workspace;
        }

        private static async Task<IList<Diagnostic>> CompileSolution(Solution solution)
        {
            var diagnostics = new List<Diagnostic>();
            var dependencies = solution.GetProjectDependencyGraph();

            foreach (var id in dependencies.GetTopologicallySortedProjects())
            {
                var project = solution.GetProject(id);
                var compilation = await project.GetCompilationAsync();

                if (string.IsNullOrWhiteSpace(compilation?.AssemblyName))
                {
                    continue;
                }

                // Todo: Receive msbuild command line arguments: https://msdn.microsoft.com/en-us/library/ms164311.aspx
                // Todo: Determine if we're in debug or release more.

                var outputFolder = project.OutputFilePath ?? Directory.GetCurrentDirectory();

                var peName  = Path.Combine(outputFolder, $"{compilation.AssemblyName}.{GetOutputName(project.CompilationOptions.OutputKind)}");
                var xmlName = Path.Combine(outputFolder, $"{compilation.AssemblyName}.xml");
                var pdbName = Path.Combine(outputFolder, $"{compilation.AssemblyName}.pdb");

                using (var peStream  = new FileStream(peName,  FileMode.OpenOrCreate))
                using (var xmlStream = new FileStream(xmlName, FileMode.OpenOrCreate))
                using (var pdbStream = new FileStream(pdbName, FileMode.OpenOrCreate))
                {
                    var result = compilation.Emit(peStream, pdbStream, xmlStream);

                    if (result.Success == false)
                    {
                        diagnostics.AddRange(result.Diagnostics);
                        break;
                    }
                }

                var metadataReferences = GetMetadataReferencesToCopy(project.FilePath, project.MetadataReferences);

                foreach (var item in metadataReferences)
                {
                    // Todo: Do the same for Pdbs and Xml Documentation!
                    File.Copy(item.FilePath, Path.Combine(outputFolder, $"{Path.GetFileName(item.FilePath)}"), true);
                }
            }

            return diagnostics;
        }

        private static IEnumerable<PortableExecutableReference> GetMetadataReferencesToCopy(string path, IEnumerable<MetadataReference> metadataReferences)
        {
            var references = metadataReferences
                .OfType<PortableExecutableReference>()
                .Select(x => new
                {
                    Name = Path.GetFileNameWithoutExtension(x.FilePath),
                    Reference = x,
                })
                .ToDictionary(x => x.Name, x => x.Reference);

            var xmldoc = new XmlDocument();
            xmldoc.Load(path);

            foreach (XmlNode item in xmldoc.GetElementsByTagName("Reference"))
            {
                var include = item.Attributes?.OfType<XmlAttribute>().FirstOrDefault(x => x.Name == "Include")?.Value;

                if (include == null)
                {
                    continue;
                }

                var name = new AssemblyName(include).Name;

                bool copyLocal;

                if (bool.TryParse(item.ChildNodes.OfType<XmlNode>().FirstOrDefault(x => x.Name == "Private")?.InnerText, out copyLocal) &&
                    references.ContainsKey(name) &&
                    copyLocal)
                {
                    yield return references[name];
                }
            }
        }

        private static string GetOutputName(OutputKind kind)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (kind)
            {
                case OutputKind.WindowsApplication:
                case OutputKind.ConsoleApplication      : return "exe";
                case OutputKind.DynamicallyLinkedLibrary: return "dll";
            }

            throw new NotImplementedException();
        }
    }
}