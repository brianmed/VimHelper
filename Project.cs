using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration.Assemblies;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Configuration;
using Roslyn;

using Microsoft.Extensions.PlatformAbstractions;

using System.Runtime.CompilerServices;

using ServiceStack.Text;

namespace VimHelper
{
    public class Symbol
    {
        public string SymbolName { get; set; }

        public string TypeName { get; set; }

        public AssemblyName AssemblyName { get; set; }
    }

    public class OffsetReturn
    {
        public List<OffsetProperty> StaticProperties { get; set; }

        public List<OffsetMethod> StaticMethods { get; set; }

        public List<OffsetProperty> Properties { get; set; }

        public List<OffsetMethod> Methods { get; set; }
    }

    public class OffsetProperty
    {
        public bool IsStatic { get; set; }

        public string PropertyType { get; set; }

        public string Name { get; set; }
    }

    public class OffsetParamter
    {
        public string Name { get; set; }

        public string TypeName { get; set; }
    }

    public class OffsetMethod
    {
        public bool IsStatic { get; set; }

        public string ReturnType { get; set; }

        public OffsetParamter[] Parameters { get; set; }

        public string Name { get; set; }
    }

    public class Project
    {
        public List<string> SourceFiles { get; set; }

        public Compilation Compilation { get; set; }

        public List<Assembly> Assemblies { get; set; }

        public IEnumerable<PortableExecutableReference> References { get; set; }

        public Project()
        {
            Assemblies = new List<Assembly>();  

            SourceFiles = new List<string>();
        }

        public void PopulateReferences()
        {
            References = Assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct()
                .Select(l => MetadataReference.CreateFromFile(l));
        }

        public void AllClasses(OmniSharpWorkspace ws, string sourceFile)
        {
            var doc = ws.GetDocument(sourceFile);
            var tree = doc.GetSyntaxTreeAsync().Result;
            var model = doc.GetSemanticModelAsync().Result;
            
            foreach (var c in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()) {                
                // Console.WriteLine(c.Identifier.Text);
				var fieldSymbol = model.GetDeclaredSymbol(c);

				foreach (var l in fieldSymbol.Locations) {
                    // Console.WriteLine(l);
					FindSymbolAtOffset(ws, sourceFile, l.SourceSpan.Start);
				}
            }
        }

        public void AllClassFields(OmniSharpWorkspace ws, string sourceFile)
        {
            var doc = ws.GetDocument(sourceFile);
            var tree = doc.GetSyntaxTreeAsync().Result;
            var model = doc.GetSemanticModelAsync().Result;
            
            foreach (var c in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()) {                
                foreach (var f in c.DescendantNodes().OfType<FieldDeclarationSyntax>()) {
					foreach (var v in f.Declaration.Variables)
					{
						var fieldSymbol = model.GetDeclaredSymbol(v);

						foreach (var l in fieldSymbol.Locations) {
							FindSymbolAtOffset(ws, sourceFile, l.SourceSpan.Start);
						}

                        // Console.WriteLine($"{v.SourceSpan.Start} {v.ToString()}");
                    	// Console.WriteLine($"{v.Identifier.SpanStart} - {v.Identifier.Text}");
					}
                }
            }
        }

        public void AllClassMethods(OmniSharpWorkspace ws, string sourceFile)
        {
            var doc = ws.GetDocument(sourceFile);
            var tree = doc.GetSyntaxTreeAsync().Result;
            var model = doc.GetSemanticModelAsync().Result;
            
            foreach (var c in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()) {                
                foreach (var m in c.DescendantNodes().OfType<MethodDeclarationSyntax>()) {
                    var fieldSymbol = model.GetDeclaredSymbol(m);

                    FindSymbolAtOffset(ws, sourceFile, m.Identifier.SpanStart);

                    // Console.WriteLine($"{v.SourceSpan.Start} {v.ToString()}");
                    // Console.WriteLine($"{v.Identifier.SpanStart} - {v.Identifier.Text}");
                }
            }
        }
        
        public void AllLocalVariables(OmniSharpWorkspace ws, string sourceFile)        
        {
            var doc = ws.GetDocument(sourceFile);
            var tree = doc.GetSyntaxTreeAsync().Result;
            var model = doc.GetSemanticModelAsync().Result;
            
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()) {
                var methodBody = method.Body;
                var result = model.AnalyzeDataFlow(methodBody);

                var variableDeclarationAndUsages = result.VariablesDeclared.
                    Union(result.ReadInside).
                    Union(result.ReadOutside);

                foreach (var v in variableDeclarationAndUsages) {                
                    foreach (var l in v.Locations) {
                        // Console.WriteLine($"{l.SourceSpan.Start} {v.ToString()}");
                        FindSymbolAtOffset(ws, sourceFile, l.SourceSpan.Start);
                    }
                }           
            }
        }            

        public void ProcessDepsFile(List<string> depSearchPath, string depsFile)
        {
            var assets = JsonObject.Parse(File.ReadAllText(depsFile));

            var targets = JsonObject.Parse(assets.Child("targets"));
            var assemblyFullNames = new Dictionary<string, string>();

            var libraries = JsonObject.Parse(assets.Child("libraries"));

            foreach (var target in targets) {
                var names = JsonObject.Parse(target.Value);
                foreach (var name in names) {
                    if (!assemblyFullNames.ContainsKey(name.Key)) {
                        assemblyFullNames.Add(name.Key, $"{name.Key.Substring(0, name.Key.IndexOf('/'))}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    }
                }

                foreach (var fullName in assemblyFullNames.OrderBy(v => v.Key)) {
                    App.Project.LoadAssemblyWithReferenced(fullName.Value);
                    App.Project.LoadAssemblyWithReferenced($"{fullName.Key.Substring(0, fullName.Key.IndexOf('/'))}.dll", depSearchPath);
                }

                App.Project.LoadAssemblyWithReferenced("System, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                App.Project.LoadAssemblyWithReferenced("System.Console, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

                // For base stuff, I believe (maybe unnecessary)
                App.Project.LoadAssemblyWithReferenced("mscorlib, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                App.Project.LoadAssemblyWithReferenced((Type.GetType("System.Int32").Assembly.FullName));                    
            }
        }

        public void LoadAssemblyWithReferenced(string fullName, List<string> depSearchPath = null)
        {
            // Eww
            try {
                if (Assemblies.Any(a => Regex.IsMatch(fullName, $"^{a.GetName().Name},"))) {
                    return;
                }
                
                try {
                    Assemblies.Add(Assembly.Load(fullName));
                } catch {
                    try {
                        if (depSearchPath == null) {
                            throw;
                        } else {
                            foreach (var path in depSearchPath) {
                                try {                                    
                                    Assemblies.Add(Assembly.LoadFile(Path.Combine(path, fullName)));                                    
                                } catch {
                                    continue;
                                }

                                break;
                            }
                        }
                    } catch {
                        throw;
                    }
                }                    

                // Console.Error.WriteLine($"Loaded: {fullName}");
            } catch {   
                // Console.Error.WriteLine($"Skipped: {fullName}");

                return;
            }

            foreach (var asmName in Assemblies.Last().GetReferencedAssemblies()) {
                LoadAssemblyWithReferenced(asmName.FullName);
            }
        }

        public void AddDocument(string fullPath)
        {
            SourceFiles.Add(fullPath);   
        }

        public OmniSharpWorkspace AdhocWorkspace()
        {
            var ws = new OmniSharpWorkspace();
            var projectId = ProjectId.CreateNewId();
            
            ws.AddProject(ProjectInfo.Create(projectId, new VersionStamp(), "Joy", "Joy", LanguageNames.CSharp, metadataReferences: References));

            foreach (var sourceFile in SourceFiles) {
                ws.AddDocument(projectId, sourceFile, SourceCodeKind.Regular);
            }

            return ws;
        }

        public void Compile()
        {
            var ws = AdhocWorkspace();

            Compilation = ws.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            using (var ms = new MemoryStream())
            {
                var result = Compilation.Emit(ms);

                if (!result.Success) {
                    throw new Exception(String.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
                }
            }   
        }       

        public Symbol FindSymbolAtOffset(OmniSharpWorkspace ws, string path, int offset)
        {
            var doc = ws.GetDocument(path);
            var model = doc.GetSemanticModelAsync().Result;

            var symbol = Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(model, offset, ws).Result;

            ITypeSymbol type;

            // Must be a bette way
            if (symbol is ILocalSymbol) {
                type = ((ILocalSymbol) symbol).Type;
            } else if (symbol is IParameterSymbol) {
                type = ((IParameterSymbol) symbol).Type;
            } else if (symbol is ITypeSymbol) {
                type = symbol as ITypeSymbol;
            } else {
				type = symbol.ContainingType;
                // throw new Exception($"Unsupported symbol at offset {offset} [{symbol?.Name}] {symbol.GetType()}");
            }

            var needCompletions = type;     

            var typeName = $"{needCompletions.Name}";
            if (String.IsNullOrWhiteSpace(typeName)) {
                needCompletions = needCompletions.BaseType as ITypeSymbol;
                typeName = needCompletions.Name;
            }

			Console.WriteLine($"{symbol.Name}\t{path.Replace(Directory.GetCurrentDirectory(), "").Replace("/", "")}\t:normal {offset}go ;\"");
            // App.Log($"Looking Up: {typeName} {needCompletions.ContainingAssembly.ToDisplayString()} {offset} [{symbol.Name}]");

            return new Symbol() {
                SymbolName = symbol.Name, 
                AssemblyName = new AssemblyName(needCompletions.ContainingAssembly.ToDisplayString()),
                TypeName = typeName,
            };

        }

        public Type GetSymbolType(Symbol symbol)
        {
            return Type.GetType(
                $"{symbol.TypeName}, {symbol.AssemblyName}",
                (name) =>  {
                    return Assemblies.Where(z => z.FullName == name.FullName).FirstOrDefault();
                },
                (assembly, name, ignore) => {
                    return assembly.GetTypes().Where(z => z.Name == name).First();
                }, true
            );
        }

        public (List<OffsetProperty>, List<OffsetMethod>) GatherStatic(Type type) {
            var properties = new List<OffsetProperty>();
            var methods = new List<OffsetMethod>();

            foreach (var s in type.GetFields(BindingFlags.Static)) {        
                properties.Add(new OffsetProperty{
                    IsStatic = true,
                    PropertyType = s.FieldType.ToString(),
                    Name = s.Name
                });
            }

            foreach (var method in type.GetMethods(BindingFlags.Static)) {
                var parameters = new List<OffsetParamter>();

                foreach (var parameter in method.GetParameters()) {
                    parameters.Add(new OffsetParamter{
                        Name = parameter.Name,
                        TypeName = parameter.ParameterType.ToString(),
                    });
                }

                methods.Add(new OffsetMethod {
                    IsStatic = true,
                    Name = method.Name,
                    ReturnType = method.ReturnType.ToString(),
                    Parameters = parameters.ToArray(),
                });            
            }      

            return (properties, methods);      
        }

        public List<OffsetProperty> GatherProperties(Type type) {
            var properties = new List<OffsetProperty>();

            foreach (var p in type.GetProperties()) {
                properties.Add(new OffsetProperty{
                    IsStatic = true,
                    PropertyType = p.PropertyType.ToString(),
                    Name = p.Name
                });                
            }

            return properties;
        }

        public List<OffsetMethod> GatherMethods(Type type)
        {
            var methods = new List<OffsetMethod>();

            foreach (var method in type.GetMethods())
            {
                var parameters = new List<OffsetParamter>();

                foreach (var parameter in method.GetParameters()) {
                    parameters.Add(new OffsetParamter{
                        Name = parameter.Name,
                        TypeName = parameter.ParameterType.ToString(),
                    });
                }

                methods.Add(new OffsetMethod {
                    IsStatic = true,
                    Name = method.Name,
                    ReturnType = method.ReturnType.ToString(),
                    Parameters = parameters.ToArray(),
                });                            
            }

            return methods;
        }                    
    }

    // Gogo gadget copy 'n paste from https://github.com/OmniSharp/omnisharp-roslyn
    public class OmniSharpWorkspace : Workspace
    {
        public bool Initialized { get; set; }

        public OmniSharpWorkspace()
            : base(new HostServicesAggregator(null).CreateHostServices(), "Custom") {  }

        public override bool CanOpenDocuments => true;

        public override void OpenDocument(DocumentId documentId, bool activate = true)
        {
            var doc = this.CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                this.OnDocumentOpened(documentId, text.Container, activate);
            }
        }

        public override void CloseDocument(DocumentId documentId)
        {
            var doc = this.CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                var version = doc.GetTextVersionAsync(CancellationToken.None).Result;
                var loader = TextLoader.From(TextAndVersion.Create(text, version, doc.FilePath));
                this.OnDocumentClosed(documentId, loader);
            }
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            OnProjectAdded(projectInfo);
        }

        public void AddProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceAdded(projectId, projectReference);
        }

        public void RemoveProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceRemoved(projectId, projectReference);
        }

        public void AddMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceAdded(projectId, metadataReference);
        }

        public void RemoveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceRemoved(projectId, metadataReference);
        }

        public void AddDocument(DocumentInfo documentInfo)
        {
            OnDocumentAdded(documentInfo);
        }

        public DocumentId AddDocument(ProjectId projectId, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var loader = new OmniSharpTextLoader(filePath);
            var documentInfo = DocumentInfo.Create(documentId, filePath, filePath: filePath, loader: loader, sourceCodeKind: sourceCodeKind);

            this.AddDocument(documentInfo);

            return documentId;
        }

        public void RemoveDocument(DocumentId documentId)
        {
            OnDocumentRemoved(documentId);
        }

        public void RemoveProject(ProjectId projectId)
        {
            OnProjectRemoved(projectId);
        }

        public void SetCompilationOptions(ProjectId projectId, CompilationOptions options)
        {
            OnCompilationOptionsChanged(projectId, options);
        }

        public void SetParseOptions(ProjectId projectId, ParseOptions parseOptions)
        {
            OnParseOptionsChanged(projectId, parseOptions);
        }

        public void OnDocumentChanged(DocumentId documentId, SourceText text)
        {
            OnDocumentTextChanged(documentId, text, PreservationMode.PreserveIdentity);
        }

        public DocumentId GetDocumentId(string filePath)
        {
            var documentIds = CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            return documentIds.FirstOrDefault();
        }

        public IEnumerable<Document> GetDocuments(string filePath)
        {
            return CurrentSolution
                .GetDocumentIdsWithFilePath(filePath)
                .Select(id => CurrentSolution.GetDocument(id));
        }

        public Document GetDocument(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            var documentId = GetDocumentId(filePath);
            if (documentId == null)
            {
                return null;
            }

            return CurrentSolution.GetDocument(documentId);
        }

        public override bool CanApplyChange(ApplyChangesKind feature)
        {
            return true;
        }
    }    

    public class OmniSharpTextLoader : TextLoader
    {
        private readonly string _filePath;

        public OmniSharpTextLoader(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!Path.IsPathRooted(filePath))
            {
                throw new ArgumentException("Expected an absolute file path", nameof(filePath));
            }

            this._filePath = filePath;
        }

        public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            var prevLastWriteTime = File.GetLastWriteTimeUtc(_filePath);

            TextAndVersion textAndVersion;

            using (var stream = File.OpenRead(_filePath))
            {
                var version = VersionStamp.Create(prevLastWriteTime);
                var text = SourceText.From(stream);
                textAndVersion = TextAndVersion.Create(text, version, _filePath);
            }

            var newLastWriteTime = File.GetLastWriteTimeUtc(_filePath);
            if (!newLastWriteTime.Equals(prevLastWriteTime))
            {
                throw new IOException($"File was externally modified: {_filePath}");
            }

            return Task.FromResult(textAndVersion);
        }
    }    

    public class HostServicesAggregator
    {
        private readonly ImmutableArray<Assembly> _assemblies;

        public HostServicesAggregator(
            IEnumerable<IHostServicesProvider> hostServicesProviders)
        {
            var builder = ImmutableHashSet.CreateBuilder<Assembly>();

            // We always include the default Roslyn assemblies, which includes:
            //
            //   * Microsoft.CodeAnalysis.Workspaces
            //   * Microsoft.CodeAnalysis.CSharp.Workspaces
            //   * Microsoft.CodeAnalysis.VisualBasic.Workspaces

            foreach (var assembly in MefHostServices.DefaultAssemblies)
            {
                builder.Add(assembly);
            }

            _assemblies = builder.ToImmutableArray();
        }

        public HostServices CreateHostServices()
        {
            return MefHostServices.Create(_assemblies);
        }
    }

    public interface IHostServicesProvider
    {
        ImmutableArray<Assembly> Assemblies { get; }
    }    
}