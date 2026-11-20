using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class SyntaxProgram
{
    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "Assets/Scripts";
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        int files = 0, failures = 0;
        var modes = new[]
        {
            new CSharpParseOptions(LanguageVersion.Latest),
            new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols("UNITY_EDITOR")
        };

        foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(path);
            files++;
            foreach (var mode in modes)
            {
                var tree = CSharpSyntaxTree.ParseText(code, mode, path);
                var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
                foreach (var error in errors)
                {
                    Console.Error.WriteLine("SYNTAX ERROR [" +
                        (mode.PreprocessorSymbolNames.Contains("UNITY_EDITOR") ? "EDITOR" : "PLAYER") +
                        "] " + error);
                    failures++;
                }
            }
        }

        Console.WriteLine("C# grammar checked: " + files + " sources x 2 compile symbols, errors=" + failures);
        if (failures > 0) return 1;
        if (files < 100) throw new Exception("Source directory unexpectedly incomplete: " + files);
        return 0;
    }
}
