using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
    public static class App
    {
        public static string BasePath;
        public static string Name;
        public static string Version;
        public static string BuildDate;
     
        public static string BaseDataDir;

        public static void Initialize()
        {
            App.BasePath = PlatformServices.Default.Application.ApplicationBasePath; 
            App.Name = PlatformServices.Default.Application.ApplicationName;      
            App.Version  = PlatformServices.Default.Application.ApplicationVersion; 

            App.BaseDataDir = App.BasePath;
        }

        public static void Log(object obj,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            Console.Error.WriteLine("{0}_{1}({2}): {3}", Path.GetFileName(file), member, line, obj?.ToString());
        }   

        public static Project Project;
    }

    class Program
    {
        static void Main(string[] args)
        {
            App.Initialize();        

            App.Project = new Project();
        
            foreach (var file in args.Skip(2).ToArray()) {
                App.Project.AddDocument(file);                    
            }

            App.Project.ProcessDepsFile(args[0].Split(':').ToList(), args[1]);
            App.Project.PopulateReferences();
            App.Project.Compile();

            var ws = App.Project.AdhocWorkspace();

			foreach (var file in args.Skip(2).ToList()) {
				App.Project.AllClasses(ws, file);
				App.Project.AllClassMethods(ws, file);
				App.Project.AllClassFields(ws, file);
				App.Project.AllLocalVariables(ws, file);
			}
        }
    }
}