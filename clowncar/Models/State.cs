using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using clowncar.Helpers;
using clowncar.Meta;

namespace clowncar.Models
{
    public class Settings
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
        public Settings Settings { get; }
        public Runner(Settings settings)
        {
            Settings = settings;
        }

        internal async Task<(bool success, string errors)> Run()
        {
            var result = false;
            var drt = DryRunToken(Settings.DryRun);

            if (Settings.RawMarkdown != null) Settings.NoTemplate = Settings.NoTemplate || (Settings.DefaultTemplate == null);

            var (success, templateText, errors) = ResolveTemplate(Settings.NoTemplate, Settings.DefaultTemplate, Settings.InputPath, drt);

            if (!success) return (success, errors);

            if (Settings.RawMarkdown != null)
            {
                Settings.RawMarkdown = Settings.RawMarkdown.Replace("\\r\\n", "\\n").Replace("\\n", Environment.NewLine);

                var html = Apply(Settings.RawMarkdown, "", templateText);

                Console.Out.WriteLine($"{drt}{html}");

                return (true, errors);
            }

            if (Settings.FileName != null)
            {
                //Just one file...
                return await Generate(Settings.FileName, Settings.OutputPath, Settings.InputPath, templateText, Settings.DryRun, Settings.LessNoise);
            }

            if (Settings.InputPath != null)
            {
                (result, errors) = await GenerateAll(Settings.InputPath, Settings.OutputPath, Settings.InputPath, templateText, Settings.DryRun, Settings.LessNoise, Settings.Recurse);
            }

            return (result, errors);
        }

        public static ConcurrentQueue<string> generateQueue = new ConcurrentQueue<string>();
        public static ConcurrentQueue<string> copyQueue = new ConcurrentQueue<string>();
        //public static ConcurrentQueue<string> skipQueue = new ConcurrentQueue<string>();

        class Progress
        {
            int _Seen = 0;
            int _ToCopy = 0;
            int _Copied = 0;
            int _ToGenerate = 0;
            int _Generated = 0;
            bool _CollectorDone = false;

            public int Seen => _Seen;
            public int ToCopy => _ToCopy;
            public int Copied => _Copied;
            public int ToGenerate => _ToGenerate;
            public int Generated => _Generated;
            public bool CollectorDone => _CollectorDone;

            internal void IncrementSeen() { Interlocked.Increment(ref _Seen); }
            internal void IncrementToCopy() { Interlocked.Increment(ref _ToCopy); }
            internal void IncrementCopied() { Interlocked.Increment(ref _Copied); }
            internal void IncrementToGenerate() { Interlocked.Increment(ref _ToGenerate); }
            internal void IncrementGenerated() { Interlocked.Increment(ref _Generated); }
            internal void SetCollectorDone() { _CollectorDone = true; }

            internal void WriteHeading(TextWriter outy)
            {
                outy.WriteLine($"Seen\tToCopy\tCopied\tToGenerate\tGenerated");
            }

            internal void WriteProgress(TextWriter outy, int genCount, int copyCount)
            {
                outy.WriteLine($"{Seen}\t{ToCopy}\t{Copied}\t{ToGenerate}\t{Generated}\t{genCount}\t{copyCount}\t{CollectorDone}");
            }
        }


        private static async Task<(bool success, string errors)> GenerateAll(string inputPath, string outputPath, string inputRootPath, string templateText, bool dryRun, bool lessNoise, bool recurse)
        {
            var generateAvailable = new AutoResetEvent(false);
            var copyAvailable = new AutoResetEvent(false);
            //var skipAvailable = new AutoResetEvent(false);
            var progress = new Progress();

            // We only copy files if an output path has been provided.
            var copying = !string.IsNullOrWhiteSpace(outputPath) && (outputPath != inputPath);

            progress.WriteHeading(Console.Out);

            var tasks = new List<Task>
            {
                Task.Run(() => FindFilesToGenerate(progress, inputPath, copying, generateAvailable, copyAvailable, recurse)),
                Task.Run(() => GenerateFiles(progress, outputPath, inputRootPath, templateText, generateAvailable, dryRun, lessNoise)),
                Task.Run(() => CopyFiles(progress, inputPath, outputPath, copyAvailable, dryRun, lessNoise))
            };

            void MyCallBack(object state0)
            {
                progress.WriteProgress(Console.Out, generateQueue.Count, copyQueue.Count);

            }
            using (var t = new Timer(MyCallBack, null, 200, 200))
            {
                Console.Out.WriteLine("Waiting...");

                await Task.WhenAll(tasks);
                Console.Out.WriteLine("Finished waiting...");
            }

            // One final report.
            MyCallBack(null);

            return (true, "");
        }

        private static Task CopyFiles(Progress progress, string inputPath, string outputPath, AutoResetEvent copyAvailable, bool dryRun, bool lessNoise)
        {
            do
            {
                if (copyQueue.Count == 0)
                {
                    try
                    {
                        while (!copyAvailable.WaitOne(TimeSpan.FromSeconds(1))) ;
                    }
                    catch (ObjectDisposedException)
                    {
                        //Console.Out.WriteLine("oe " + oe.ToString());
                        //break;
                    }
                }
                if (copyQueue.Count > 0 && copyQueue.TryDequeue(out var f))
                {
                    CopyFile(f, inputPath, outputPath, dryRun, lessNoise);
                    progress.IncrementCopied();
                }

            }
            while (!progress.CollectorDone || copyQueue.Count > 0);
            
            return Task.CompletedTask;
        }

        private static void CopyFile(string f, string inputPath, string outputPath, bool dryRun, bool lessNoise)
        {
            var drt = DryRunToken(dryRun);

            var inputFilePath = Path.GetDirectoryName(f);
            var relativePath = Path.GetRelativePath(inputPath, inputFilePath);
            var targetFileName = Path.Combine(outputPath, relativePath, Path.GetFileName(f));
            if (!inputFilePath.ToLowerInvariant().StartsWith(outputPath.ToLowerInvariant() + Path.DirectorySeparatorChar))
            {
                if (!lessNoise)
                {
                    Console.Out.WriteLine($"{drt}xx> (skipped:subsite) {Path.Combine(relativePath, Path.GetFileName(f))}");
                }
                return;
            }

            Console.Out.WriteLine($"{drt}++> {Path.Combine(relativePath, Path.GetFileName(f))}");

            if (!dryRun) File.Copy(f, targetFileName, true);
        }

        private static Task GenerateFiles(Progress progress, string outputPath, string inputRootPath, string templateText, AutoResetEvent generateAvailable, bool dryRun, bool lessNoise)
        {
            do
            {
                if (generateQueue.Count == 0)
                {
                    try
                    {
                        while (!generateAvailable.WaitOne(TimeSpan.FromSeconds(1))) ;
                    }
                    catch (ObjectDisposedException)
                    {
                        //Console.Out.WriteLine("object disposed..." + oe.ToString());
                        //break;
                    }
                }

                if (generateQueue.Count != 0 && generateQueue.TryDequeue(out var fileToGenerate))
                {
                    GenerateSync(fileToGenerate, outputPath, inputRootPath, templateText, dryRun, lessNoise);
                    progress.IncrementGenerated();
                }
            }
            while (!progress.CollectorDone || generateQueue.Count > 0);
            
            return Task.CompletedTask;
        }

        private static void TryRelease(AutoResetEvent sync)
        {
            sync.Set();
        }

        private static Task FindFilesToGenerate(Progress progress, string inputPath, bool copying, AutoResetEvent generateAvailable, AutoResetEvent copyAvailable, bool recurse)
        {
            try
            {
                var allFiles = Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
                foreach (var f in Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    progress.IncrementSeen();
                    if (Path.GetExtension(f).ToLowerInvariant() == ".md")
                    {
                        progress.IncrementToGenerate();
                        generateQueue.Enqueue(f);
                        TryRelease(generateAvailable);
                    }
                    else if ( //todo:linux case sensitive, todo: configurable set
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
                        if (copying)
                        {
                            progress.IncrementToCopy();
                            copyQueue.Enqueue(f);
                            TryRelease(copyAvailable);
                        }
                    }
                    else
                    {
                        //skipQueue.Enqueue(f);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("ERRRRRRRRRRRRRRRRRRRRRRRRRRRRR" + ex.ToString());
                return Task.FromException(ex);
            }
            finally
            {
                copyAvailable.Dispose();
                generateAvailable.Dispose();
                progress.SetCollectorDone();
            }

            return Task.CompletedTask;
        }

        //private static async Task<(bool success, string errors)> GenerateAll_Old(string inputPath, string outputPath, string inputRootPath, string templateText, bool dryRun, bool lessNoise, bool recurse)
        //{
        //    var drt = DryRunToken(dryRun);
        //    int ii = -1;
        //    int iiFinished = -1;
        //    try
        //    {
        //        var allFiles = Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

        //        //foreach (var f in allFiles)
        //        //{
        //        //    Console.Out.WriteLine($"{Path.GetFileName(f)}");
        //        //}
        //        //Console.Out.WriteLine("FILES:" + allFiles.Count().ToString());
        //        //return (true, "ok");
        //        foreach (var f in Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        //        //var doall = Parallel.ForEach(allFiles,
        //        //    //async 
        //        //    (f, x, i) =>
        //        {
        //            Interlocked.Increment(ref ii);

        //            //        Console.WriteLine(f);

        //            // foreach (var f in Directory.EnumerateFiles(inputPath, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        //            //{
        //            if (Path.GetExtension(f).ToLowerInvariant() == ".md")
        //            {
        //                //var (result, errors) = await Generate(f, outputPath, inputPath, templateText, dryRun, lessNoise);
        //                var (success, errors) = Generate(f, outputPath, inputPath, templateText, dryRun, lessNoise).GetAwaiter().GetResult();
        //                if (!success)
        //                {
        //                    //x.Break();
        //                    throw new Exception(errors);
        //                }
        //            }
        //            else
        //                if ( //todo:linux case sensitive, todo: configurable set
        //                     !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)
        //                  && !f.Contains(Path.DirectorySeparatorChar + ".hg" + Path.DirectorySeparatorChar)
        //                  && !f.Contains(Path.DirectorySeparatorChar + "_book" + Path.DirectorySeparatorChar)
        //                  && !f.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar)
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".html"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".clowncar"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".clowntent"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".gitignore"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".pre"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".ok"
        //                  && Path.GetExtension(f).ToLowerInvariant() != ".ps1")
        //            {
        //                if (!string.IsNullOrWhiteSpace(outputPath))
        //                {
        //                    var inputFilePath = Path.GetDirectoryName(f);
        //                    var relativePath = Path.GetRelativePath(inputPath, inputFilePath);
        //                    var targetFileName = Path.Combine(outputPath, relativePath, Path.GetFileName(f));

        //                    //TODO: Linux case-insensitive bug
        //                    // Do NOT copy anything FROM the output path (avoid recursive explosion)
        //                    if (inputFilePath.ToLowerInvariant().StartsWith(outputPath.ToLowerInvariant() + Path.DirectorySeparatorChar))
        //                    {
        //                        if (!lessNoise)
        //                        {
        //                            Console.Out.WriteLine($"{drt}xx> (skipped:subsite) {Path.Combine(relativePath, Path.GetFileName(f))}");
        //                        }
        //                        Interlocked.Increment(ref iiFinished);
        //                        continue;
        //                        //return;
        //                    }

        //                    lock (Console.Out)
        //                    {
        //                        Console.ForegroundColor = ConsoleColor.White;
        //                        Console.Out.WriteLine($"{drt}++> {Path.Combine(relativePath, Path.GetFileName(f))}");
        //                        Console.ResetColor();
        //                    }
        //                    //TODO: async.
        //                    if (!dryRun) File.Copy(f, targetFileName, true);
        //                }
        //            }
        //            else
        //            {
        //                var inputFilePath = Path.GetDirectoryName(f);
        //                var relativePath = Path.GetRelativePath(inputPath, inputFilePath);

        //                if (!lessNoise)
        //                {
        //                    lock (Console.Out)
        //                    {
        //                        Console.ForegroundColor = ConsoleColor.Yellow;
        //                        Console.Out.WriteLine($"{drt}xx> (skipped) {Path.Combine(relativePath, Path.GetFileName(f))}");
        //                        Console.ResetColor();
        //                    }
        //                }
        //            }
        //            Interlocked.Increment(ref iiFinished);
        //        }//  });
        //        //if (doall.IsCompleted)
        //        {
        //            Console.Out.WriteLine($"FILES started,finished: {ii}, {iiFinished}");
        //        }
        //        //else
        //        //{
        //        //    Console.Out.WriteLine("NOT DONE");
        //        //}
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.Out.WriteLine(ex.GetType().Name);
        //        Console.Out.WriteLine(ex.Message);
        //        Console.Out.WriteLine(ex.ToString());
        //        if (ex.InnerException != null)
        //        {
        //            Console.Out.WriteLine("INNER EXCEPTION");
        //            Console.Out.WriteLine("===============");
        //            Console.Out.WriteLine(ex.InnerException.GetType().Name);
        //            Console.Out.WriteLine(ex.InnerException.Message);
        //            Console.Out.WriteLine(ex.InnerException.ToString());
        //        }
        //        Console.ResetColor();

        //        var errors = ex.ToString();
        //        return (false, errors);
        //    }

        //    return (true, null);
        //}

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


        private static (bool success, string errors) GenerateSync(string fileName, string outputPath, string inputRootPath, string templateText, bool dryRun, bool lessNoise)
        {
            var drt = DryRunToken(dryRun);
            var errors = string.Empty;
            if (!File.Exists(fileName))
            {
                errors = $"Input File does not exist ({fileName})";
                return (false, errors);
            }

            var s = File.ReadAllText(fileName);

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

            if (!dryRun) File.WriteAllText(outputFile, result);

            Console.Out.WriteLine($"{drt}~~> {Path.Combine(relativePath, fileandExtension)}, {result.Length} chars");

            return (true, errors);
        }

        private static string Apply(string rawMarkdown, string title, string templateText)
        {
            return templateText.Replace("{{title}}", title).Replace("{{body}}", rawMarkdown.ToHtml());
        }
    }
}
