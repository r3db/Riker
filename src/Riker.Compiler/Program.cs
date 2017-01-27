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
    internal enum CallerType
    {
        Method,
        MethodGroup,
        Assignment,
        Unknown,
    }

    internal static class Program
    {
        private static void Main()
        {
            Task.Run(async () =>
            {
                const string path = "../../../Riker.sln";
                
                if (File.Exists(path) == false)
                {
                    Console.WriteLine("Could not find solution file at: '{0}'.", path);
                }
                else
                {
                    var workspace = ToInMemorySolution(await MSBuildWorkspace.Create().OpenSolutionAsync(path));
                    var diagnostics = await CompileSolution(workspace.CurrentSolution);

                    foreach (var item in diagnostics)
                    {
                        Console.WriteLine(item);
                    }

                    // Analyzer!
                    await Analize(workspace);
                }
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

        private static async Task Analize(Workspace workspace)
        {
            var methodsToSearch = new[]
            {
                typeof(Device).GetMethod("Run")
            };

            foreach (var document in GetDocuments(workspace))
            {                
                var editor = await DocumentEditor.CreateAsync(document);
                var members = GetMembers(editor);
                var hasMembers = false;

                foreach (var member in members)
                {
                    var symbol = editor.SemanticModel.GetSymbolInfo(member).Symbol;

                    if (symbol == null)
                    {
                        continue;
                    }

                    if (IsMatch(methodsToSearch, symbol) == false)
                    {
                        continue;
                    }

                    var methodCallType = GetCallerType(member);
                    var parent = GetParentDeclaration(member);

                    //Note: Debug-Info Only
                    hasMembers = true;
                    var line = member.Expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine("{0,20} as {1,12} [{2}]", symbol, methodCallType, line);

                    if (methodCallType == CallerType.Method)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        AnalizeMemoryAccessInKernel(member, editor);
                        Console.ResetColor();
                    }

                    // Todo: Analize Kernel
                    // Todo: This will probably be a loop! We go up the tree a method does not interact with anyone anymore!
                    // Todo: For now it will a "one-off".
                    if (parent != null)
                    {
                        switch (parent.Kind())
                        {
                            case SyntaxKind.FieldDeclaration:
                            {
                                Console.WriteLine("{0} : Field", ((FieldDeclarationSyntax)parent).Declaration.Variables.Last().Identifier);
                                break;
                            }
                            case SyntaxKind.MethodDeclaration:
                            {
                                Console.WriteLine("{0} : Method", ((MethodDeclarationSyntax) parent).Identifier);
                                break;
                            }
                            case SyntaxKind.LocalDeclarationStatement:
                            {
                                Console.WriteLine("{0} : Local", ((LocalDeclarationStatementSyntax)parent).Declaration.Variables.Last().Identifier);
                                break;
                            }
                            default:
                            {
                                throw new NotSupportedException();
                            }
                        }
                    }
                }

                if (hasMembers)
                {
                    Console.WriteLine(new string('-', 80));
                }
            }
        }
        
        private static IEnumerable<Document> GetDocuments(Workspace workspace)
        {
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    yield return document;
                }
            }
        }

        private static IEnumerable<MemberAccessExpressionSyntax> GetMembers(SyntaxEditor editor)
        {
            return editor
                    .OriginalRoot
                    .DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .ToList();
        }

        private static bool IsMatch(IEnumerable<MethodInfo> methods, ISymbol symbol)
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

        private static CallerType GetCallerType(SyntaxNode member)
        {
            switch (member.Parent.Kind())
            {
                case SyntaxKind.Argument:
                {
                    return CallerType.MethodGroup;
                }
                case SyntaxKind.InvocationExpression:
                {
                    return CallerType.Method;
                }
                default:
                {
                    var parent = member.Parent;

                    while (parent != null && parent.Kind() != SyntaxKind.VariableDeclaration)
                    {
                        parent = parent.Parent;
                    }

                    return parent != null && parent.Kind() == SyntaxKind.VariableDeclaration
                            ? CallerType.Assignment
                            : CallerType.Unknown;
                }
            }
        }

        private static SyntaxNode GetParentDeclaration(SyntaxNode member)
        {
            var parent = member.Parent;

            while (parent != null &&
                parent.Kind() != SyntaxKind.FieldDeclaration &&
                parent.Kind() != SyntaxKind.MethodDeclaration &&
                parent.Kind() != SyntaxKind.LocalDeclarationStatement)
            {
                parent = parent.Parent;
            }

            return parent;
        }

        private static void AnalizeMemoryAccessInKernel(SyntaxNode member, DocumentEditor editor)
        {
            var expression = ((InvocationExpressionSyntax)member.Parent)
                .ArgumentList
                .Arguments
                .First()
                .Expression;

            switch (expression.Kind())
            {
                case SyntaxKind.IdentifierName:
                {
                    Console.WriteLine("\tExternal Lambda : {0}", ((IdentifierNameSyntax)expression).Identifier);
                    break;
                }
                case SyntaxKind.ParenthesizedLambdaExpression:
                {
                    Console.WriteLine("\t   Local Lambda");

                    var access = ((ParenthesizedLambdaExpressionSyntax)expression)
                        .Body
                        .DescendantNodes()
                        .OfType<ElementAccessExpressionSyntax>()
                        .ToList();

                    foreach (var item in access)
                    {
                        var symbol = editor.SemanticModel.GetSymbolInfo(item.Expression).Symbol;
                        var line = item.Expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var identifier = item.DescendantNodes().OfType<IdentifierNameSyntax>().First().Identifier.ValueText;
                        ITypeSymbol type;
                        string locality;

                        switch (symbol.Kind)
                        {
                            case SymbolKind.Parameter:
                            {
                                type = ((IParameterSymbol)symbol).Type;
                                locality = "param";
                                break;
                            }
                            case SymbolKind.Local:
                            {
                                type = ((ILocalSymbol)symbol).Type;
                                locality = "local";
                                break;
                            }
                            default:
                            {
                                throw new NotSupportedException();
                            }
                        }

                        var parent = item.Parent;
                        string mode;

                        switch (parent.Kind())
                        {
                            case SyntaxKind.SimpleAssignmentExpression:
                            {
                                mode = "_w";
                                break;
                            }
                            case SyntaxKind.AddAssignmentExpression:
                            case SyntaxKind.SubtractAssignmentExpression:
                            case SyntaxKind.MultiplyAssignmentExpression:
                            case SyntaxKind.DivideAssignmentExpression:
                            case SyntaxKind.ModuloAssignmentExpression:
                            case SyntaxKind.AndAssignmentExpression:
                            case SyntaxKind.ExclusiveOrAssignmentExpression:
                            case SyntaxKind.OrAssignmentExpression:
                            case SyntaxKind.LeftShiftAssignmentExpression:
                            case SyntaxKind.RightShiftAssignmentExpression:
                            {
                                mode = "rw";
                                break;
                            }
                            default:
                            {
                                mode = "r_";
                                break;
                            }
                        }

                        Console.WriteLine("\t{0,8} {1} {2} {3} [{4}]", identifier, type, locality, mode, line);
                    }

                    break;
                }
            }
        }
    }
}