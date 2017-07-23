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
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;

namespace VimHelper
{
    public static class App
    {
        public static string BasePath;
        public static string Name;
        public static string Version;
        public static string BuildDate;
     
        public static string BaseDataDir;
       
        public static OrmLiteConnectionFactory DbFactory;

        public static string CodeDb
        { 
            get {
                return Path.Combine(BaseDataDir, "Code.sqlite");
            }

            private set {}
        }

        public static void Initialize()
        {
            App.BasePath = PlatformServices.Default.Application.ApplicationBasePath; 
            App.Name = PlatformServices.Default.Application.ApplicationName;      
            App.Version  = PlatformServices.Default.Application.ApplicationVersion; 
            App.BuildDate = File.ReadAllLines(Path.Combine(App.BasePath, "TimeBuilt.txt")).First();

            App.BaseDataDir = App.BasePath;

            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            App.DbFactory = new OrmLiteConnectionFactory();

            App.DbFactory.RegisterConnection("Code", new OrmLiteConnectionFactory($"Data Source={App.CodeDb};"));
        }

        public static void Log(object obj,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            Console.WriteLine("{0}_{1}({2}): {3}", Path.GetFileName(file), member, line, obj.ToString());
        }   

        public static string[] Args;             
    }
    
    class MigrateDb
    {
        public static void Run()
        {
            var orm = OrmLiteConnectionFactory.NamedConnections["Code"];
            using (var db = orm.OpenDbConnectionString($"Data Source={App.CodeDb};")) {
                foreach (var sql in Sql.CodeDb) {
                    using (var dbTrans = db.OpenTransaction()) {
                        db.ExecuteSql(sql);

                        dbTrans.Commit();
                    }
                }                        
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            App.Log($"{System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")}");

            App.Initialize();        
            App.Args = args;
            MigrateDb.Run();

            var project = new Project();

            if ("--depFile" == App.Args[0]) {
                var assets = JsonObject.Parse(File.ReadAllText(App.Args[1]));

                var targets = JsonObject.Parse(assets.Child("targets"));
                var assemblyFullNames = new Dictionary<string, string>();
                var assemblyFileNames = new Dictionary<string, string>();

                var libraries = JsonObject.Parse(assets.Child("libraries"));

                foreach (var target in targets) {
                    // Console.WriteLine(target.Key);

                    var things = JsonObject.Parse(target.Value);
                    foreach (var thing in things) {
                        var blobs = JsonObject.Parse(thing.Value);
                        // System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e
                        foreach (var blob in blobs) {
                            if ("dependencies" == blob.Key) {
                                foreach (var deps in JsonObject.Parse(blob.Value)) {                                    
                                    var libKey = $"{deps.Key}/{deps.Value}";
                                
                                    if ("{}" == things.Child(libKey)) {
                                        continue;
                                    }

                                    if (assemblyFullNames.ContainsKey(libKey)) {
                                        continue;
                                    }

                                    if (blobs.Keys.Contains("runtime")) {
                                        var runtime = JsonObject.Parse(blobs.Child("runtime"));
                                        
                                        var path = $"{thing.Key}/{runtime.First().Key}";

                                        assemblyFileNames.Add(libKey, path);
                                    }

                                    // Console.WriteLine(libKey);

                                    var assemblyName = new AssemblyName();
                                    assemblyName.Name = deps.Key;

                                    var version = (deps.Value.Contains('-') ? deps.Value.Split('-')[0] : deps.Value).Split('.').ToList();
                                    assemblyName.Version = Version.Parse(String.Join(".", version.ToArray()));

                                    if (libraries.Keys.Contains(libKey)) {                                        
                                        // assemblyFullNames.Add(libKey, $"{deps.Key}, Version={String.Join('.', version)}, Culture=neutral, PublicKeyToken=null");
                                        assemblyFullNames.Add(libKey, $"{deps.Key}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                                    }
                                }
                            }
                        }
                    }

                    foreach (var fullName in assemblyFullNames.OrderBy(v => v.Key)) {
                        if (assemblyFileNames.ContainsKey(fullName.Key)) {
                            project.LoadAssemblyWithReferenced(fullName.Value, assemblyFileNames[fullName.Key]);
                        } else {
                            project.LoadAssemblyWithReferenced(fullName.Value);
                        }                        

                        project.LoadAssemblyWithReferenced("System, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                        project.LoadAssemblyWithReferenced("System.Console, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    }

                    // For base stuff, I believe (maybe unnecessary)
                    project.LoadAssemblyWithReferenced("mscorlib, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    project.LoadAssemblyWithReferenced((Type.GetType("System.Int32").Assembly.FullName));                    
                }

                var shimmy = App.Args.ToList();
                shimmy.RemoveAt(0);
                shimmy.RemoveAt(0);

                App.Args = shimmy.ToArray();                
            }

            project.PopulateReferences();
            project.Compile();
            project.AllVariables();

            App.Log($"{System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")}");                
        }
    }
}