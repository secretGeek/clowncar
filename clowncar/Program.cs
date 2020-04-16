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
        public string Filter { get; set; }
        public string Path { get; set; }
        public string DefaultTemplate { get; set; }
        public string OutputPath { get; set; }
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
                {"f|filter=",       "filter",           v => s.Filter = v },
                {"p|path=",         "path",             v => s.Path = v },
                {"t|template=",     "default template", v => s.DefaultTemplate = v },
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

                Console.WriteLine($"Try '{FileName} --help' for more information.");
                return 1;
            }

            if (p.UnrecognizedOptions != null && p.UnrecognizedOptions.Count > 0)
            {
                ShowHelp(p);
                return 1;
            }

            if (show_help)
            {
                ShowHelp(p);
                return 0;
            }

            //Console.WriteLine("do it");
            //Console.ReadLine();
            var markdownText = "# hi";
            GoNuts(markdownText);
            return 0;
        }

        private static void GoNuts(string markdownText)
        {
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var f = Markdown.ToHtml(markdownText, pipeline);
            Console.WriteLine(f);
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
            Console.Write("If no options are specified, a "); Console.ForegroundColor = ConsoleColor.White;
            Console.Write("gui "); Console.ResetColor();
            Console.WriteLine("is presented.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
