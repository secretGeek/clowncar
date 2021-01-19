using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using clowncar.Helpers;
using clowncar.Meta;

namespace clowncar.Models
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
        public bool LessNoise { get; set; }
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
    }
    public class Runner
    {
        public State state { get; }
        public Runner(State state)
        {
            this.state = state;
        }
        internal async Task<(bool success, string errors)> Run()
        {
            var result = false;
            var drt = DryRunToken(state.DryRun);
            
            if (state.RawMarkdown != null) state.NoTemplate = state.NoTemplate || (state.DefaultTemplate == null);

            var (success, templateText, errors) = ResolveTemplate(state.NoTemplate, state.DefaultTemplate, state.InputPath, drt);

            if (!success) return (success, errors);

            if (state.RawMarkdown != null)
            {
                state.RawMarkdown = state.RawMarkdown.Replace("\\r\\n", "\\n").Replace("\\n", Environment.NewLine);

                var html = Apply(state.RawMarkdown, "", templateText);
                
                Console.Out.WriteLine($"{drt}{html}");

                return (true, errors);
            }

            if (state.FileName != null)
            {
                //Just one file...
                return await Generate(state.FileName, state.OutputPath, state.InputPath, templateText, state.DryRun, state.LessNoise);
            }

            if (state.InputPath != null)
            {
                (result, errors) = GenerateAll(state.InputPath, state.OutputPath, state.InputPath, templateText, state.DryRun, state.LessNoise, state.Recurse).GetAwaiter().GetResult();
            }

            return (result, errors);
        }

        private static async Task<(bool success, string errors)> GenerateAll(string inputPath, string outputPath, string inputRootPath, string templateText, bool dryRun, bool lessNoise, bool recurse)
        {
            var drt = DryRunToken(dryRun);
            int ii = -1;
            int iiFinished = -1;
            try
            {
                var allFiles = Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

                //foreach (var f in allFiles)
                //{
                //    Console.Out.WriteLine($"{Path.GetFileName(f)}");
                //}
                //Console.Out.WriteLine("FILES:" + allFiles.Count().ToString());
                //return (true, "ok");
                foreach (var f in Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                //var doall = Parallel.ForEach(allFiles,
                //    //async 
                //    (f, x, i) =>
                {
                    Interlocked.Increment(ref ii);

                    //        Console.WriteLine(f);

                    // foreach (var f in Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    //{
                    if (Path.GetExtension(f).ToLowerInvariant() == ".md")
                    {
                        //var (result, errors) = await Generate(f, outputPath, inputPath, templateText, dryRun, lessNoise);
                        var (success, errors) = Generate(f, outputPath, inputPath, templateText, dryRun, lessNoise).GetAwaiter().GetResult();
                        if (!success)
                        {
                            //x.Break();
                            throw new Exception(errors);
                        }
                    }
                    else
                        if ( //todo:linux case sensitive, todo: configurable set
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
                        if (!string.IsNullOrWhiteSpace(outputPath))
                        {
                            var inputFilePath = Path.GetDirectoryName(f);
                            var relativePath = Path.GetRelativePath(inputPath, inputFilePath);
                            var targetFileName = Path.Combine(outputPath, relativePath, Path.GetFileName(f));

                            //TODO: Linux case-insensitive bug
                            // Do NOT copy anything FROM the output path (avoid recursive explosion)
                            if (inputFilePath.ToLowerInvariant().StartsWith(outputPath.ToLowerInvariant() + Path.DirectorySeparatorChar))
                            {
                                if (!lessNoise)
                                {
                                    Console.Out.WriteLine($"{drt}xx> (skipped:subsite) {Path.Combine(relativePath, Path.GetFileName(f))}");
                                }
                                Interlocked.Increment(ref iiFinished);
                                continue;
                                //return;
                            }

                            lock (Console.Out)
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.Out.WriteLine($"{drt}++> {Path.Combine(relativePath, Path.GetFileName(f))}");
                                Console.ResetColor();
                            }
                            //TODO: async.
                            if (!dryRun) File.Copy(f, targetFileName, true);
                        }
                    }
                    else
                    {
                        var inputFilePath = Path.GetDirectoryName(f);
                        var relativePath = Path.GetRelativePath(inputPath, inputFilePath);

                        if (!lessNoise)
                        {
                            lock (Console.Out)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Out.WriteLine($"{drt}xx> (skipped) {Path.Combine(relativePath, Path.GetFileName(f))}");
                                Console.ResetColor();
                            }
                        }
                    }
                    Interlocked.Increment(ref iiFinished);
                }//  });
                //if (doall.IsCompleted)
                {
                    Console.Out.WriteLine($"FILES started,finished: {ii}, {iiFinished}");
                }
                //else
                //{
                //    Console.Out.WriteLine("NOT DONE");
                //}
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteLine(ex.GetType().Name);
                Console.Out.WriteLine(ex.Message);
                Console.Out.WriteLine(ex.ToString());
                if (ex.InnerException != null)
                {
                    Console.Out.WriteLine("INNER EXCEPTION");
                    Console.Out.WriteLine("===============");
                    Console.Out.WriteLine(ex.InnerException.GetType().Name);
                    Console.Out.WriteLine(ex.InnerException.Message);
                    Console.Out.WriteLine(ex.InnerException.ToString());
                }
                Console.ResetColor();

                var errors = ex.ToString();
                return (false, errors);
            }

            return (true, null);
        }

        private static (bool, string, string) ResolveTemplate(bool noTemplate, string defaultTemplate, string inputPath, string drt)
        {
            string errors = null;

            var templateText = noTemplate ? "{{body}}" : Defaults.TemplateText;

            if (string.IsNullOrWhiteSpace(defaultTemplate)) return (true, templateText, errors);

            if (!File.Exists(defaultTemplate))
            {
                if (!string.IsNullOrEmpty(inputPath) && File.Exists(Path.Combine(inputPath, defaultTemplate)))
                {
                    defaultTemplate = Path.Combine(inputPath, defaultTemplate);
                }
                else
                {
                    errors = $"{drt}Template not found ({defaultTemplate})";
                    templateText = null;
                    return (true, templateText, errors);
                }
            }

            templateText = File.ReadAllText(defaultTemplate);

            return (true, templateText, errors);
        }

        private static string DryRunToken(bool dryRun) => dryRun ? "(dry-run)" : "";
        private static async Task<(bool success, string errors)> Generate(string fileName, string outputPath, string inputRootPath, string templateText, bool dryRun, bool lessNoise)
        {
            var drt = DryRunToken(dryRun);
            var errors = string.Empty;
            if (!File.Exists(fileName))
            {
                errors = $"Input File does not exist ({fileName})";
                return (false, errors);
            }

            string s;
            //var s = File.ReadAllText(fileName);
            using (var reader = File.OpenText(fileName))
            {
                s = await reader.ReadToEndAsync();
            }

            var title = Path.GetFileNameWithoutExtension(fileName).Replace("_", " ");
            var result = Apply(s, title, templateText);

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

            string outputFile;

            if (outputPath != null)
            {
                outputFile = Path.Combine(outputPath, relativePath, fileandExtension);
                var targetPath =
                        (relativePath == ".") ?
                            outputPath :
                            Path.Combine(outputPath, relativePath);
                if (!Directory.Exists(targetPath))
                {
                    if (!dryRun) Directory.CreateDirectory(targetPath);

                    if (!lessNoise)
                    {
                        Console.Out.WriteLine($"{drt}+!> Created Directory: {targetPath}");
                    }
                }
            }
            else
            {
                outputFile = Path.Combine(inputRootPath, relativePath, fileandExtension);
            }

            //if (!dryRun) File.WriteAllText(outputFile, result);

            if (!dryRun)
            {
                using (var sw = new StreamWriter(outputFile))
                {
                    await sw.WriteAsync(result);
                }
            }

            Console.Out.WriteLine($"{drt}~~> {Path.Combine(relativePath, fileandExtension)}, {result.Length} chars");

            return (true, errors);
        }

        private static string Apply(string rawMarkdown, string title, string templateText)
        {
            return templateText.Replace("{{title}}", title).Replace("{{body}}", rawMarkdown.ToHtml());
        }
    }
}
