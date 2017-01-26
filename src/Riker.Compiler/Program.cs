using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
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

                Console.ForegroundColor = ConsoleColor.Cyan;

                if (File.Exists(path) == false)
                {
                    Console.WriteLine("Could not find solution file at: '{0}'.", path);
                }
                else
                {
                    var workspace = ToInMemorySolution(await MSBuildWorkspace.Create().OpenSolutionAsync(path));

                    await EditDocuments(workspace);

                    var diagnostics = await CompileSolution(workspace.CurrentSolution);

                    foreach (var item in diagnostics)
                    {
                        Console.WriteLine(item);
                    }
                }

                Console.ResetColor();
            }).Wait();

            Console.WriteLine("Done!");
            Console.ReadLine();
        }
        
        private static Workspace ToInMemorySolution(Solution solution)
        {
            var workspace = new AdhocWorkspace();

            workspace.AddSolution(SolutionInfo.Create(solution.Id, solution.Version));

            foreach (var project in solution.Projects)
            {
                var projectInfo = ProjectInfo.Create(
                    id                : project.Id,
                    version           : project.Version,
                    name              : project.Name,
                    assemblyName      : project.AssemblyName,
                    language          : project.Language,
                    filePath          : project.FilePath,
                    outputFilePath    : project.OutputFilePath,
                    compilationOptions: project.CompilationOptions,
                    metadataReferences: project.MetadataReferences.ToList(),
                    analyzerReferences: project.AnalyzerReferences.ToList(),
                    parseOptions      : project.ParseOptions,
                    projectReferences : project.ProjectReferences.ToList());

                workspace.AddProject(projectInfo);

                foreach (var document in project.Documents)
                {
                    workspace.AddDocument(projectInfo.Id, document.Name, SourceText.From(File.ReadAllText(document.FilePath), encoding: Encoding.UTF8));
                }
            }

            return workspace;
        }

        // Todo: Receive msbuild command line arguments: https://msdn.microsoft.com/en-us/library/ms164311.aspx
        // Todo: Determine if we're in debug or release more, do we need to do that?
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

                var outputFolder = (Path.GetDirectoryName(project.OutputFilePath) ?? Directory.GetCurrentDirectory()) + @"\gpu\";

                if (Directory.Exists(outputFolder) == false)
                {
                    Directory.CreateDirectory(outputFolder);
                }

                var extension = Path.GetExtension(project.OutputFilePath);

                var exeName = Path.Combine(outputFolder, $"{compilation.AssemblyName}.{extension}");
                var pdbName = Path.Combine(outputFolder, $"{compilation.AssemblyName}.pdb");
                var xmlName = Path.Combine(outputFolder, $"{compilation.AssemblyName}.xml");
                
                using (var exeStream = new FileStream(exeName, FileMode.OpenOrCreate))
                using (var pdbStream = new FileStream(pdbName, FileMode.OpenOrCreate))
                using (var xmlStream = new FileStream(xmlName, FileMode.OpenOrCreate))
                {
                    var result = compilation.Emit(exeStream, pdbStream, xmlStream);

                    await exeStream.FlushAsync();
                    await pdbStream.FlushAsync();
                    await xmlStream.FlushAsync();

                    if (result.Success == false)
                    {
                        diagnostics.AddRange(result.Diagnostics);
                        break;
                    }
                }

                CopyProjectReferences(project.ProjectReferences.Select(x => solution.GetProject(x.ProjectId)), outputFolder);
                CopyMetadataReferences(GetMetadataReferencesToCopy(project.FilePath, project.MetadataReferences), outputFolder);
            }

            return diagnostics;
        }
        
        private static void CopyProjectReferences(IEnumerable<Project> projects, string outputFolder)
        {
            foreach (var project in projects)
            {
                var files = GetRelatedBuildFiles(project.OutputFilePath);

                foreach (var file in files.Where(File.Exists))
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    File.Copy(file, Path.Combine(outputFolder, Path.GetFileName(file)), true);
                }
            }
        }

        private static void CopyMetadataReferences(IEnumerable<PortableExecutableReference> metadataReferences, string outputFolder)
        {
            foreach (var reference in metadataReferences)
            {
                var files = GetRelatedBuildFiles(reference.FilePath);

                foreach (var file in files.Where(File.Exists))
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    File.Copy(file, Path.Combine(outputFolder, Path.GetFileName(file)), true);
                }
            }
        }

        private static IEnumerable<string> GetRelatedBuildFiles(string file)
        {
            var a = Path.GetDirectoryName(file);
            var b = Path.GetFileNameWithoutExtension(file);

            yield return file;
            yield return $"{a}\\{b}.pdb";
            yield return $"{a}\\{b}.xml";
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

        private static async Task EditDocuments(Workspace workspace)
        {
            var methods = new[]
            {
                typeof(Device).GetMethod("Run")
            };


            foreach (var project in workspace.CurrentSolution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    var editor = await DocumentEditor.CreateAsync(document);
                    var syntaxRoot = editor.OriginalRoot;
                    
                    var methodCalls = syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

                    foreach (var methodCall in methodCalls)
                    {
                        var symbol = editor.SemanticModel.GetSymbolInfo(methodCall).Symbol;


                        if (MatchInvocationExpression(methods, symbol) == false)
                        {
                            var arguments = methodCall.ArgumentList.Arguments;

                            foreach (var item in arguments)
                            {
                                var argExpression = item.Expression;
                                var argSymbol = editor.SemanticModel.GetSymbolInfo(argExpression).Symbol;

                                switch (argExpression.Kind())
                                {
                                    case SyntaxKind.SimpleMemberAccessExpression:
                                    {
                                        var w = (MemberAccessExpressionSyntax)argExpression;

                                        if (MatchInvocationExpression(methods, argSymbol))
                                        {
                                            Console.WriteLine("{0,50} : {1} as Argument", w.Name, argExpression.Kind());
                                        }
                                       
                                        break;
                                    }
                                }
                            }

                            continue;
                        }

                        var expression = methodCall.Expression;

                        switch (expression.Kind())
                        {
                            case SyntaxKind.SimpleMemberAccessExpression:
                            {
                                var w = (MemberAccessExpressionSyntax)expression;
                                Console.WriteLine("{0,50} : {1} as Call", w.Name, expression.Kind());
                                break;
                            }
                            case SyntaxKind.ConditionalAccessExpression:
                            {
                                var w = (ConditionalAccessExpressionSyntax)expression;
                                Console.WriteLine("{0,50} : {1} as Call", w, expression.Kind());
                                throw new InvalidOperationException();
                            }
                            case SyntaxKind.IdentifierName:
                            {
                                var w = (IdentifierNameSyntax)expression;
                                Console.WriteLine("{0,50} : {1} as Call", w.Identifier, expression.Kind());
                                break;
                            }
                            case SyntaxKind.MemberBindingExpression:
                            {
                                var w = (MemberBindingExpressionSyntax)expression;
                                Console.WriteLine("{0,50} : {1} as Call", w.Name, expression.Kind());
                                break;
                            }
                            default:
                            {
                                throw new InvalidCastException();
                            }
                        }
                    }
                }

                Console.WriteLine(new string('-', 20));
            }
        }

        private static bool MatchInvocationExpression(IEnumerable<MethodInfo> methods, ISymbol symbol)
        {
            foreach (var item in methods)
            {
                var dt = item.DeclaringType;

                if (dt == null)
                {
                    continue;
                }

                if (dt.Name == symbol.ContainingType.Name &&
                    dt.Namespace == symbol.ContainingNamespace.Name &&
                    dt.Assembly.FullName == symbol.ContainingAssembly.Identity.GetDisplayName())
                {
                    return true;
                }
            }

            return false;
        }
    }
}