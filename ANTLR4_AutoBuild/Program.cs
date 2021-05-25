using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ANTLR4_AutoBuild
{
    class Program
    {
        private static readonly string antlrFileName = "antlr-4.7.2.jar";
        private static string AntlrArgs = "-Dlanguage=JavaScript -visitor";
        private static readonly List<FileSystemWatcher> _Watchers = new List<FileSystemWatcher>();
        private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory; //@"C:\Users\jlips\Desktop";
        private static readonly string AntlrPath = EstablishAntlrJarPath(antlrFileName);

        static void Main(string[] args)
        {
            if (AntlrPath == null)
            {
                Console.WriteLine("Cant Located Antlr4 Jar File.\nPress Any Key to Exit.");
                Console.WriteLine($"Searching for {antlrFileName} in base dir {BaseDirectory}");
                Console.ReadKey(true);
                return;
            }
            if (args.Length > 0)
            {
                AntlrArgs = AntlrArgs + " -package " + args[0];
                Console.WriteLine($"namespace: {args[0]}");
            }
            ScanCurrentFolder();
            char input;
            while ((input = Console.ReadKey(true).KeyChar) != 'q')
            {
                if(input != 'r')
                    continue;
                ScanCurrentFolder();
            }
        }


        private static string EstablishAntlrJarPath(string fileName)
        {
            var lib = Path.Combine(BaseDirectory, "lib");
            return Directory.Exists(lib)
                ? Directory
                    .EnumerateFiles(lib, "*.jar", SearchOption.AllDirectories)
                    .FirstOrDefault(file => Path.GetFileName(file) == fileName)
                : null;
            //return Directory
            //    .EnumerateFiles(BaseDirectory,"*.jar", SearchOption.AllDirectories)
            //    .FirstOrDefault(file => Path.GetFileName(file) == fileName);
        }

        private static void ScanCurrentFolder()
        {
            foreach (var watcher in _Watchers)
            {
                watcher.Dispose();
            }
            _Watchers.Clear();

            foreach (var file in Directory.EnumerateFiles(BaseDirectory, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!ext.Equals(".g4", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                if(CheckGrammarType(file) ==  GrammarType.Lexer)
                    Console.WriteLine($"{Path.GetFileName(file)} is detected to be a lexer. Watch Disabled for this file.");
                else
                {
                    _Watchers.Add(Watch(file));
                }
            }
        }

        private static FileSystemWatcher Watch(string file)
        {
            Console.WriteLine($"Watching {Path.GetFileName(file)}");
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(file), Path.GetFileName(file))
            {
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite| NotifyFilters.CreationTime
            };
            var skip = false;
            watcher.Changed += (sender, args) =>
            {
                if(!skip)
                    RecompileFile(file);
                skip = !skip;
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private static void RecompileFile(string file)
        {
            Console.WriteLine($">> {Path.GetFileName(file)} changed.");
            if(!File.Exists(AntlrPath))
                throw new InvalidOperationException("ANTLR JAR MISSING.");
            var outputFolder = Path.Combine(Path.GetDirectoryName(file), "_Generated");
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            var q = "\"";
            var proccessConfig = new ProcessStartInfo()
            {
                FileName = "java",
                Arguments = $"-jar {q}{AntlrPath}{q} -o {q}{outputFolder}{q} {AntlrArgs} {q}{file}{q}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            //Console.WriteLine("ARGS");
            //Console.WriteLine(proccessConfig.Arguments);
            using (var proc = Process.Start(proccessConfig))
            using (var sr = proc.StandardOutput)
            {
                sr.ReadToEnd();
            }
            Console.WriteLine(">> Compiled!");
        }

        private static GrammarType CheckGrammarType(string file)
        {
            using (var sr = new StreamReader(file))
            {
                var firstLine = sr.ReadLine();
                if (string.IsNullOrEmpty(firstLine))
                {
                    Console.WriteLine($"First Line of {Path.GetFileName(file)} is empty, unable to determine if grammar or lexer.");
                    throw new InvalidOperationException("Invalid ANTLR Header. Verify lexer grammar or grammar is in first row of file.");
                }

                return firstLine.ToLowerInvariant().Contains("lexer") ? GrammarType.Lexer : GrammarType.Parsar;
            }
        }
    }

    public enum GrammarType
    {
        Lexer,
        Parsar,
        Invalid
    }
}