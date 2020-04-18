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
        //public string Filter { get; set; }
        public string InputPath { get; set; }
        public string DefaultTemplate { get; set; }
        public string OutputPath { get; set; }
        public bool Recurse { get; set; }
        public bool NoTemplate { get; set; }
        public bool DryRun { get; set; }
        //public string IncludeFileTypes { get; set; } = "*.jpg;*.jpeg;*.png;*.gif;*.js;*.css;*.svg";

        internal bool Validate(out string errors)
        {
            if (RawMarkdown == null && FileName == null && InputPath == null)
            {
                errors = "You must specify some *input*, either `rawmarkdown (-m)`, `file (-i)`, or `path (-p)`";
                return false;
            }

            if ((RawMarkdown != null && FileName != null) ||
                (RawMarkdown != null && InputPath != null) ||
                (FileName != null && InputPath != null) ||
                (RawMarkdown != null && FileName != null && InputPath != null))
            {
                errors = "You must specify only *one* type of input, either `rawmarkdown (-m)`, `file (-i)`, or `path (-p)";
                return false;
            }

            if (RawMarkdown != null && Recurse)
            {
                errors = "There's no sense in combining `recurse (-r)` with `rawmarkdown (-m)`. Recurse works with a `path (-p)` (and, optionally, a `filter (-f)`)";
                return false;
            }

            if (FileName != null && Recurse)
            {
                errors = "There's no sense in combining `recurse (-r)` with `file (-i)`. Recurse works with a `path (-p)` (and, optionally, a `filter (-f)`)";
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
                RawMarkdown = RawMarkdown.Replace("\\r\\n", "\\n").Replace("\\n", "\n");
                var f = RawMarkdown.ToHtml();
                //if this.OutputPath != null
                if (!DryRun)
                {
                    Console.WriteLine(f);
                }
                else
                {
                    Console.WriteLine("(dryrun)" + f);
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
                    } else if (Path.GetExtension(f).ToLowerInvariant() != ".html"
                        && Path.GetExtension(f).ToLowerInvariant() != ".clowncar"
                        && Path.GetExtension(f).ToLowerInvariant() != ".clowntent"
                        && Path.GetExtension(f).ToLowerInvariant() != ".pre"
                        && Path.GetExtension(f).ToLowerInvariant() != ".ok"
                        && Path.GetExtension(f).ToLowerInvariant() != ".ps1")
                    {
                        if (!string.IsNullOrWhiteSpace(OutputPath))
                        {
                            var inputFilePath = Path.GetDirectoryName(f);
                            var relativePath = Path.GetRelativePath(InputPath, inputFilePath);
                            var targetFileName = Path.Combine(OutputPath, relativePath, Path.GetFileName(f));
                            // don't copy anything FROM the output path.....
                            //TODO: depends if OS is case-insensitive
                            if (inputFilePath.ToLowerInvariant().StartsWith(OutputPath.ToLowerInvariant()))
                            {
                                continue;
                            }
                            if (!DryRun)
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($"++> {Path.Combine(relativePath, targetFileName)}");
                                Console.ResetColor();
                                File.Copy(f, targetFileName, true);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"(dryrun)++> {Path.Combine(relativePath, targetFileName)}");
                                Console.ResetColor();
                            }
                        }
                        
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"skipped: {f}");
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

            var fileandExtension = Path.GetFileNameWithoutExtension(fileName) + ".html";
            var inputFilePath = Path.GetDirectoryName(fileName);
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
                        Console.WriteLine($"Created Directory: {Path.Combine(outputPath, relativePath)}");
                    }
                    else
                    {
                        Console.WriteLine($"(dryrun)Created Directory: {Path.Combine(outputPath, relativePath)}");
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
                    Console.WriteLine($"~~> {(relativePath + "\\" + fileandExtension)} {f.Length} chars");
                    File.WriteAllText(outputFile, f);
                }
                else
                {
                    Console.WriteLine($"(dryrun)~~> {(relativePath + "\\" + fileandExtension)} {f.Length} chars");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(defaultTemplate))
                {
                    var templateText = Defaults.TemplateText;
                    templateText = templateText.Replace("{{body}}", f);

                    if (!dryRun)
                    {
                        Console.WriteLine($"~~> {(relativePath + "\\" + fileandExtension)} {templateText.Length} chars, defaultTemplate");
                        File.WriteAllText(outputFile, templateText);
                    }
                    else
                    {
                        Console.WriteLine($"(dryrun)~~> {(relativePath + "\\" + fileandExtension)} {templateText.Length} chars, defaultTemplate");
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

                    //TODO: verbose
                    //Console.WriteLine($"template:{defaultTemplate}");
                    var templateText = File.ReadAllText(defaultTemplate);
                    templateText = templateText.Replace("{{body}}", f);
                    if (!dryRun)
                    {
                        Console.WriteLine($"~~> {(relativePath + "\\" + fileandExtension)} {templateText.Length} chars, template: {defaultTemplate}");
                        File.WriteAllText(outputFile, templateText);
                    }
                    else
                    {
                        Console.WriteLine($"(dryrun)~~> {(relativePath + "\\" + fileandExtension)} {templateText.Length} chars, template: {defaultTemplate}");
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
                {"m|rawmarkdown=",  "the raw markdown", v => s.RawMarkdown = v},
                {"i|file=",         "input file name",  v => s.FileName = v},
                {"p|path=",         "path",             v => s.InputPath = v },
                {"r|recurse",       "recurse",          v => s.Recurse = v != null },
                {"t|template=",     "default template", v => s.DefaultTemplate = v },
                {"n|notemplate",    "no template!",     v => s.NoTemplate = v != null },
                {"d|dryrun",        "dry run",          v => s.DryRun = v != null },
                {"o|output=",       "output path",      v => s.OutputPath = v },
                {"?|h|help",        "this message",     v => show_help = v != null },
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
                Console.WriteLine("\r\nError parsing arguments");

                Console.WriteLine(e.Message);
                Console.ResetColor();

                if (e.Option != null)
                {
                    int i = 0;
                    p.WriteOptionPrototype(Console.Out, e.Option, ref i);
                    p.WriteOptionDetails(Console.Out, e.Option, i);
                    Console.WriteLine();
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
                Console.WriteLine("\r\nError. Unrecognized commandline option" + (p.UnrecognizedOptions.Count > 1 ? "s" : "") + ".");
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
                var assemblyVersion = "0.0.1";
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"\r\n{FileName}"); Console.ResetColor();
                Console.WriteLine(" version " + assemblyVersion.ToString());
                Console.WriteLine("Turn markdown to html.\r\n");
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
</head>
<body>
<style>
body {
  max-width:70ch;
  padding:2ch;
  margin:auto;
  color:#333;
  font-size:1.2em;
  background-color:#F2F2F2;
}
pre {
  white-space:pre-wrap;
  margin-left:4ch;
  background-color:#FFF;
  padding:1ch;
  border-radius:4px;
}
</style>
{{body}}
</body>
<script>
</script>
</html>";
    }
}
