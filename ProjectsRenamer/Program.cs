using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProjectsRenamer
{
    class Program
    {
        const int LevelMultiplier = 2;

        static readonly string[] Extensions = new string[]
        {
           ".cs", ".csproj", ".xaml", ".xaml.cs", ".nuspec", ".config", ".sln"
        };
        static readonly string[] IgnoreDirectories = new string[]
        {
            "bin", "obj", ".NuGet", ".git", "packages"
        };

        static void Log(int level, string message)
        {
            Console.WriteLine($"{new String('.', level * LevelMultiplier)} {message}");
        }

        static bool RenameItemContents(string path, IEnumerable<Tuple<string, string>> replaces)
        {
            if (File.Exists(path))
            {

                    File.WriteAllLines(path, File.ReadAllLines(path).Select(line =>
                    {
                        string l = line;
                        foreach (var replace in replaces)
                        {
                            l = l.Replace(replace.Item1, replace.Item2);
                        }
                        return l;
                    }).ToList());
                return true;
            }
            return false;
        }

        static bool RenameItemName(string directory, IEnumerable<Tuple<string, string>> replaces, string itemName, out string resultItemName)
        {
            string path = Path.Combine(directory, itemName);

            string newItemName = itemName;
            foreach (var replace in replaces)
            {
                newItemName = newItemName.Replace(replace.Item1, replace.Item2);
            }
            string newPath = Path.Combine(directory, newItemName);
            resultItemName = itemName;

            bool success = false;
            if (itemName.Equals(newItemName, StringComparison.InvariantCultureIgnoreCase) == false)
            {
                if (Directory.Exists(path))
                {
                    Directory.Move(path, newPath);
                    resultItemName = newItemName;
                    success = true;
                }
                else if (File.Exists(path))
                {
                    File.Move(path, newPath);
                    resultItemName = newItemName;
                    success = true;
                }
            }

            return success;
        }

        static void RenameItem(string path, IEnumerable<Tuple<string, string>> replaces, int level)
        {
            string itemName = GetFileName(path);
            string newItemName = itemName;

            bool renamedContents = RenameItemContents(path, replaces);

            Log(level, $"/{itemName}: [{(renamedContents ? "UPDATED" : "-")}]");

            if (RenameItemName(GetDirectory(path), replaces, itemName, out newItemName))
            {
                Log(level, $"/{itemName} -> {newItemName}");
            }
        }
        static void RecursiveRename(string path, IEnumerable<Tuple<string, string>> replaces, IEnumerable<string> extensions, IEnumerable<string> ignoreDirectories, int level = 0)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    string fileName = GetFileName(path);

                    if (extensions.Any(e => e.Equals(Path.GetExtension(file), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        RenameItem(file, replaces, level);
                    }
                    else
                    {
                        Log(level, $"{fileName}: ignored");
                    }
                }

                foreach (var directory in Directory.GetDirectories(path))
                {
                    string dirName = GetFileName(directory);
                    Log(level, dirName + "/");
                    if (ignoreDirectories.Any(e => e.Equals(dirName, StringComparison.InvariantCultureIgnoreCase)) == false)
                    {
                        RecursiveRename(directory, replaces, extensions, ignoreDirectories, level + 1);
                        RenameItem(directory, replaces, level);
                    }
                }
            }
        }

        static string GetFileName(string path)
        {
            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        }

        static string GetDirectory(string path)
        {
            return Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));

        }



        static void Main(string[] args)
        {
            string sourcePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            IEnumerable<string> ignoreDirectories = IgnoreDirectories;
            IEnumerable<string> extensions = Extensions;
            IEnumerable<Tuple<string, string>> replaces = Enumerable.Empty<Tuple<string, string>>();

            if (args.Length > 0)
            {
                string replaceArg = args.FirstOrDefault(a => a.StartsWith(Arguments.REPLACE));

                if (args.Any(a => Arguments.HELP.Equals(a) || Arguments.HELP2.Equals(a) || Arguments.HELP3.Equals(a))
                    || replaceArg == null)
                {
                    PrintHelp();
                    return;
                }

                var tuples = replaceArg.Replace(Arguments.REPLACE, "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (tuples != null && tuples.Any())
                {
                    replaces = tuples.Select(t => t.TrimStart('(').TrimEnd(')').Split(new char[] { ',' }))
                                     .Select(t => new Tuple<string, string>(t[0].Trim(), t[1].Trim()))
                                     .Where(t => String.IsNullOrWhiteSpace(t.Item1) == false)
                                     .ToList();
                }


                if (args[0] != ".")
                {
                    sourcePath = Path.Combine(sourcePath, args[0]);
                    if (Directory.Exists(sourcePath) == false)
                    {
                        Console.WriteLine("Directory not found");
                        return;
                    }
                }

                string addExtensionsArg = args.FirstOrDefault(a => a.StartsWith(Arguments.EXTENSIONS));
                if (addExtensionsArg != null)
                {
                    var addExtensions = addExtensionsArg.Replace(Arguments.EXTENSIONS, "")
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.StartsWith(".") ? e : "." + e);
                    extensions = extensions.Concat(addExtensions);
                }

                string ignoreDirectoriesArg = args.FirstOrDefault(a => a.StartsWith(Arguments.IGNORE_DIRECTORIES));
                if (ignoreDirectoriesArg != null)
                {
                    var addIgnoreDirectories = ignoreDirectoriesArg.Replace(Arguments.IGNORE_DIRECTORIES, "")
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    ignoreDirectories = ignoreDirectories.Concat(addIgnoreDirectories);
                }
            }
            else
            {
                PrintHelp();
                return;
            }

            if (replaces.Any() == false)
            {
                Console.WriteLine("Nothing to replace");
                return;
            }

            Console.WriteLine($"Target path: {sourcePath}");
            Console.WriteLine($"Replaces extensions: {String.Join("; ", replaces.Select(t => t.ToString()))}");
            Console.WriteLine($"Accepted extensions: {String.Join("; ", extensions)}");
            Console.WriteLine($"Ignored directories: {String.Join("; ", ignoreDirectories)}");

            RecursiveRename(sourcePath, replaces, extensions, ignoreDirectories);
        }

        static void PrintHelp()
        {
            const int pad = -14;
            Console.WriteLine($"UXI.ProjectsRenamer.exe [FOLDER] [OPTIONS]");
            Console.WriteLine("OPTIONS:");
            Console.WriteLine($"  {Arguments.REPLACE,pad} Specifies replacements as tuples, separated with semicolon, e.g., (UXC.Core.,UXI.);(UXC.,PLUS.). This option is REQUIRED.");
            Console.WriteLine($"  {Arguments.EXTENSIONS,pad} Specifies additional file extensions to check contents of files, separated with semicolon, e.g., .exe;.obj");
            Console.WriteLine($"  {"",pad} Default values: {String.Join("; ", Extensions)}");
            Console.WriteLine($"  {Arguments.IGNORE_DIRECTORIES,pad} Specifies additional directories to ignore during rename process, separated with semicolon, e.g., ProjectName;AnotherProjectName");
            Console.WriteLine($"  {"",pad} Default values: {String.Join("; ", IgnoreDirectories)}");
        }

        static class Arguments
        {
            public const string EXTENSIONS = "--extensions=";
            public const string IGNORE_DIRECTORIES = "--ignore=";
            public const string REPLACE = "--replace=";
            public const string HELP = "-h";
            public const string HELP2 = "--h";
            public const string HELP3 = "--help";
        }
    }
}
