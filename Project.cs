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
using ServiceStack.OrmLite;
using ServiceStack.DataAnnotations;
using DbUp;

namespace VimHelper
{
    public class Project
    {
        public OmniSharpWorkspace Ws { get; set; }

        public Compilation Compilation { get; set; }

        public List<Assembly> Assemblies { get; set; }

        public IEnumerable<PortableExecutableReference> References { get; set; }

        public Project()
        {
            Assemblies = new List<Assembly>();  

            AppDomain.CurrentDomain.AssemblyResolve += 
                        CurrentDomain_AssemblyResolve;
                      
        }

        public void PopulateReferences()
        {
            References = Assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct()
                .Select(l => MetadataReference.CreateFromFile(l));
        }
        
        public void AllVariables()        
        {
            var doc = Ws.GetDocument(App.Args[0]);
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
                        FindSymbolAtOffset(App.Args[0], l.SourceSpan.Start);
                    }
                }           
            }
        }            

        public void LoadAssemblyWithReferenced(string fullName)
        {
            try {
                if (Assemblies.Any(a => Regex.IsMatch(fullName, $"^{a.GetName().Name},"))) {
                    return;
                }
                
                Assemblies.Add(Assembly.Load(fullName));

                // Console.WriteLine($"Loaded: {assemblies.Last().FullName}");
            } catch (Exception ex) {                    
                // Console.WriteLine($"Warning loading: {fullName}: {ex.Message}");

                return;
            }

            foreach (var asmName in Assemblies.Last().GetReferencedAssemblies()) {
                LoadAssemblyWithReferenced(asmName.FullName);
            }
        }

        public void Compile() {
            Ws = new OmniSharpWorkspace();

            var version = new VersionStamp();
            var id = ProjectId.CreateNewId();

            var projectInfo = ProjectInfo.Create(id, version, "Joy", "Joy", LanguageNames.CSharp, metadataReferences: References);
            Ws.AddProject(projectInfo);
            var docId = Ws.AddDocument(id, App.Args[0], SourceCodeKind.Regular);

            Compilation = Ws.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            using (var ms = new MemoryStream())
            {
                var result = Compilation.Emit(ms);

                if (false == result.Success) {
                    throw new Exception(String.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
                }

                Assembly.Load(ms.ToArray()); 
            }   
        }       

        public void FindSymbolAtOffset(string path, int offset)
        {
            var doc = Ws.GetDocument(path);
            var model = doc.GetSemanticModelAsync().Result;

            var symbol = Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(model, offset, Ws).Result;

            ITypeSymbol type;

            // Must be a bette way
            if (symbol is ILocalSymbol) {
                type = ((ILocalSymbol) symbol).Type;
            } else if (symbol is IParameterSymbol) {
                type = ((IParameterSymbol) symbol).Type;
            } else if (symbol is ITypeSymbol) {
                type = symbol as ITypeSymbol;
            } else {
                throw new Exception($"Unsupported symbol at offset {offset}");
            }

            var needCompletions = type;     

            var typeName = $"{needCompletions.Name}";
            if (String.IsNullOrWhiteSpace(typeName)) {
                needCompletions = needCompletions.BaseType as ITypeSymbol;
                typeName = needCompletions.Name;
            }

            Console.WriteLine($"Looking Up: {typeName}");

            var t = Type.GetType(
                $"{typeName}, {needCompletions.ContainingAssembly.ToDisplayString()}",
                (name) => 
                {
                    return Assemblies.Where(z => z.FullName == name.FullName).FirstOrDefault();
                },
                (assembly, name, ignore) =>
                {
                    return assembly.GetTypes().Where(z => z.Name == name).First();
                }, 
                true);

            ShowStatic(t);
            ShowProperties(t);
            ShowMethods(t);
        }

        public void ShowStatic(Type type) {
            foreach (var s in type.GetFields(BindingFlags.Static | BindingFlags.Public)) {
                Console.WriteLine($"{s.FieldType} {s.Name}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                var parameters = method.GetParameters();
                var parameterDescriptions = string.Join
                    (", ", method.GetParameters()
                                .Select(x => x.ParameterType + " " + x.Name)
                                .ToArray());

                Console.WriteLine("{0} {1} ({2})",
                                method.ReturnType,
                                method.Name,
                                parameterDescriptions);
            }            
        }

        public void ShowProperties(Type type) {
            foreach (var p in type.GetProperties()) {
                Console.WriteLine($"{p.PropertyType} {p.Name}");
            }
        }

        public void ShowMethods(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public))
            {
                var parameters = method.GetParameters();
                var parameterDescriptions = string.Join
                    (", ", method.GetParameters()
                                .Select(x => x.ParameterType + " " + x.Name)
                                .ToArray());

                Console.WriteLine("{0} {1} ({2})",
                                method.ReturnType,
                                method.Name,
                                parameterDescriptions);
            }
        }        

        public static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;                

            // check for assemblies already loaded
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            // Maybe in the future we can load on the fly
            return null;
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