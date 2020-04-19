using Markdig;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace clowncar
{
    public class State
    {
        public string RawMarkdown { get; set; }
        public string FileName { get; set; }
        public string InputPath { get; set; }
        public string DefaultTemplate { get; set; }
        public string OutputPath { get; set; }
        public bool Recurse { get; set; }
        public bool NoTemplate { get; set; }
        public bool DryRun { get; set; }

        internal bool Validate(out string errors)
        {
            if (RawMarkdown == null && FileName == null && InputPath == null)
            {
                errors = "You must specify some *input*, either `rawmarkdown (-m)`, `file (-f)`, or `path (-p)`";
                return false;
            }

            if ((RawMarkdown != null && FileName != null) ||
                (RawMarkdown != null && InputPath != null) ||
                (FileName != null && InputPath != null) ||
                (RawMarkdown != null && FileName != null && InputPath != null))
            {
                errors = "You must specify only *one* type of input, either `rawmarkdown (-m)`, `file (-f)`, or `path (-p)`";
                return false;
            }

            if (RawMarkdown != null && Recurse)
            {
                errors = "There's no sense in combining `recurse (-r)` with `rawmarkdown (-m)`. Recurse only works with a `path (-p)`";
                return false;
            }

            if (FileName != null && Recurse)
            {
                errors = "There's no sense in combining `recurse (-r)` with `file (-f)`. Recurse only works with a `path (-p)`";
                return false;
            }

            if (InputPath != null && !Directory.Exists(InputPath))
            {
                errors = $"The `path (-p)` does not exist {InputPath}";
                return false;
            }

            if (OutputPath != null && !Directory.Exists(OutputPath))
            {
                errors = "The `outputpath (-o)` does not exist";
                return false;
            }

            errors = null;
            return true;
        }

        internal bool Run(out string errors)
        {
            errors = null;

            if (RawMarkdown != null)
            {
                RawMarkdown = RawMarkdown.Replace("\\r\\n", "\\n").Replace("\\n", Environment.NewLine);
                var f = RawMarkdown.ToHtml();

                if (!DryRun)
                {
                    Console.Out.WriteLine(f);
                }
                else
                {
                    Console.Out.WriteLine("(dryrun)" + f);
                }
                return true;
            }

            if (FileName != null)
            {
                return Generate(FileName, OutputPath, InputPath, NoTemplate, DefaultTemplate, DryRun, out errors);
            }

            if (InputPath != null)
            {
                foreach (var f in Directory.EnumerateFiles(InputPath, "*.*", Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(f).ToLowerInvariant() == ".md")
                    {
                        var result = Generate(f, OutputPath, InputPath, NoTemplate, DefaultTemplate, DryRun, out errors);
                        if (!result) return false;
                    } else if ( //todo:linux case sensitive, todo: configurable set
                           !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + ".hg" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "_book" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar)
                        && Path.GetExtension(f).ToLowerInvariant() != ".html"
                        && Path.GetExtension(f).ToLowerInvariant() != ".clowncar"
                        && Path.GetExtension(f).ToLowerInvariant() != ".clowntent"
                        && Path.GetExtension(f).ToLowerInvariant() != ".gitignore"
                        && Path.GetExtension(f).ToLowerInvariant() != ".pre"
                        && Path.GetExtension(f).ToLowerInvariant() != ".ok"
                        && Path.GetExtension(f).ToLowerInvariant() != ".ps1")
                    {
                        if (!string.IsNullOrWhiteSpace(OutputPath))
                        {
                            var inputFilePath = Path.GetDirectoryName(f);
                            var relativePath = Path.GetRelativePath(InputPath, inputFilePath);
                            var targetFileName = Path.Combine(OutputPath, relativePath, Path.GetFileName(f));

                            //TODO: Linux case-insensitive bug
                            // Do NOT copy anything FROM the output path (avoid recursive explosion)
                            if (inputFilePath.ToLowerInvariant().StartsWith(OutputPath.ToLowerInvariant() + Path.DirectorySeparatorChar))
                            {
                                Console.Out.WriteLine($"xx> (skipped:subsite) {Path.Combine(relativePath, Path.GetFileName(f))}");
                                continue;
                            }

                            if (!DryRun)
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.Out.WriteLine($"++> {Path.Combine(relativePath, Path.GetFileName(f))}");
                                Console.ResetColor();
                                File.Copy(f, targetFileName, true);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Out.WriteLine($"(dryrun)++> {Path.Combine(relativePath, Path.GetFileName(f))}");
                                Console.ResetColor();
                            }
                        }
                        
                    }
                    else
                    {
                        var inputFilePath = Path.GetDirectoryName(f);
                        var relativePath = Path.GetRelativePath(InputPath, inputFilePath);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        if (!DryRun)
                        {
                            Console.Out.WriteLine($"xx> (skipped) {Path.Combine(relativePath, Path.GetFileName(f))}");
                        }
                        else
                        {
                            Console.Out.WriteLine($"(dryrun)xx> (skipped) {Path.Combine(relativePath, Path.GetFileName(f))}");
                        }
                        Console.ResetColor();
                    }
                }
            }

            return true;
        }

        private static bool Generate(string fileName, string outputPath, string inputRootPath, bool noTemplate, string defaultTemplate, bool dryRun, out string errors)
        {
            errors = null;
            if (!File.Exists(fileName))
            {
                errors = $"Input File does not exist ({fileName})";
                return false;
            }

            var s = File.ReadAllText(fileName);
            var f = s.ToHtml();
            var title = Path.GetFileNameWithoutExtension(fileName).Replace("_", " ");
            var fileandExtension = Path.GetFileNameWithoutExtension(fileName) + ".html";
            var inputFilePath = Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(inputRootPath))
            {
                inputRootPath = Directory.GetCurrentDirectory();
            }
            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                inputFilePath = Directory.GetCurrentDirectory();
            }

            var relativePath = Path.GetRelativePath(inputRootPath, inputFilePath);
            var outputFile = (string)null;

            if (outputPath != null)
            {
                outputFile = Path.Combine(outputPath, relativePath, fileandExtension);
                if (!Directory.Exists(Path.Combine(outputPath, relativePath)))
                {
                    if (!dryRun)
                    {
                        Directory.CreateDirectory(Path.Combine(outputPath, relativePath));
                        //Verbose:
                        Console.Out.WriteLine($"Created Directory: {Path.Combine(outputPath, relativePath)}");
                    }
                    else
                    {
                        Console.Out.WriteLine($"(dryrun) Created Directory: {Path.Combine(outputPath, relativePath)}");
                    }
                }
            }
            else
            {
                outputFile = Path.Combine(inputRootPath, relativePath, fileandExtension);
            }
            
            //Consider: Encoding??
            if (noTemplate)
            {
                if (!dryRun)
                {
                    Console.Out.WriteLine($"~~> {Path.Combine(relativePath, fileandExtension)}, {f.Length} chars");
                    File.WriteAllText(outputFile, f);
                }
                else
                {
                    Console.Out.WriteLine($"(dryrun)~~> {Path.Combine(relativePath, fileandExtension)}, {f.Length} chars");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(defaultTemplate))
                {
                    var templateText = Defaults.TemplateText;
                    templateText = templateText.Replace("{{title}}", title);
                    templateText = templateText.Replace("{{body}}", f);

                    if (!dryRun)
                    {
                        Console.Out.WriteLine($"~~> {Path.Combine(relativePath, fileandExtension)}, {templateText.Length} chars, defaultTemplate");
                        File.WriteAllText(outputFile, templateText);
                    }
                    else
                    {
                        Console.Out.WriteLine($"(dryrun)~~> {Path.Combine(relativePath, fileandExtension)}, {templateText.Length} chars, defaultTemplate");
                    }
                }
                else
                {
                    if (!File.Exists(defaultTemplate))
                    {
                        if (File.Exists(Path.Combine(inputRootPath, defaultTemplate)))
                        {
                            defaultTemplate = Path.Combine(inputRootPath, defaultTemplate);
                        }
                        else
                        {
                            errors = $"Template not found ({defaultTemplate})";
                            return false;
                        }
                    }

                    var templateText = File.ReadAllText(defaultTemplate);
                    templateText = templateText.Replace("{{title}}", title);
                    templateText = templateText.Replace("{{body}}", f);
                    if (!dryRun)
                    {
                        Console.WriteLine($"~~> {Path.Combine(relativePath,fileandExtension)}, {templateText.Length} chars, template: {Path.GetFileName(defaultTemplate)}");
                        File.WriteAllText(outputFile, templateText);
                    }
                    else
                    {
                        Console.WriteLine($"(dryrun)~~> {Path.Combine(relativePath, fileandExtension)}, {templateText.Length} chars, template: {Path.GetFileName(defaultTemplate)}");
                    }
                }
            }

            return true;
        }
    }

    class Program
    {
        static string FileName = "clowncar";

        static int Main(string[] args)
        {
            var s = new State();
            var show_help = false;
            var p = new OptionSet() {
                {"m|rawmarkdown=",  "the raw markdown *", v => s.RawMarkdown = v},
                {"f|file=",         "input file name *",  v => s.FileName = v},
                {"p|path=",         "path *",             v => s.InputPath = v },
                {"r|recurse",       "recurse",            v => s.Recurse = v != null },
                {"t|template=",     "template file name",      v => s.DefaultTemplate = v },
                {"n|notemplate",    "no template!",       v => s.NoTemplate = v != null },
                {"d|dryrun",        "dry run",            v => s.DryRun = v != null },
                {"o|output=",       "output path",        v => s.OutputPath = v },
                {"?|h|help",        "this message",       v => show_help = v != null },
            };

            List<string> extra;

            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                //Consider: Log.WriteLine(e);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteLine(Environment.NewLine + "Error parsing arguments");

                Console.Out.WriteLine(e.Message);
                Console.ResetColor();

                if (e.Option != null)
                {
                    int i = 0;
                    p.WriteOptionPrototype(Console.Out, e.Option, ref i);
                    p.WriteOptionDetails(Console.Out, e.Option, i);
                    Console.Out.WriteLine();
                }

                Console.Error.WriteLine($"Try '{FileName} --help' for more information.");
                return 1;
            }

            if (p.UnrecognizedOptions != null && p.UnrecognizedOptions.Count > 0)
            {
                ShowHelp(p);
                return 2;
            }

            if (show_help)
            {
                ShowHelp(p);
                return 0;
            }

            if (!s.Validate(out string validationError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(validationError);
                Console.ResetColor();
                return 3;
            }

            if (!s.Run(out string runError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(runError);
                Console.ResetColor();
                return 4;
            }

            return 0;
        }

        private static void ShowHelp(OptionSet p)
        {
            if (p.UnrecognizedOptions != null && p.UnrecognizedOptions.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(Environment.NewLine + "Error. Unrecognized commandline option" + (p.UnrecognizedOptions.Count > 1 ? "s" : "") + ".");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.ResetColor();
                foreach (var s in p.UnrecognizedOptions)
                {
                    Console.Write("Unrecognized: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(s);
                    Console.ResetColor();
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please see below for all valid options.");
                Console.ResetColor();
            }
            else
            {
                var assemblyVersion = "0.0.2";
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{Environment.NewLine}{FileName}"); Console.ResetColor();
                Console.WriteLine(" version " + assemblyVersion.ToString());
                Console.WriteLine("Turn markdown to html." + Environment.NewLine);
            }

            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("clowncar ");
            Console.ResetColor();
            Console.WriteLine("[options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }

    public static class Marko
    {
        static MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        public static string ToHtml(this string rawMarkdown)
        {
            rawMarkdown = rawMarkdown.Replace(".md)", ".html)");
            return Markdown.ToHtml(rawMarkdown, pipeline);
        }
    }
    public static class Defaults
    {
        public static string TemplateText = @"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>
<title>{{title}}</title>
<link rel='icon' href='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>🤡</text></svg>'>
</head>
<body>
<style>
body {
  max-width:70ch;
  padding:2ch;
  margin:auto;
  color:#333;
  font-size:1.2em;
  background-color:#FFF;
}
pre {
  white-space:pre-wrap;
  margin-left:4ch;
  background-color:#EEE;
  padding:1ch;
  border-radius:1ch;
}
</style>
{{body}}
</body>
</html>";
    }
}
