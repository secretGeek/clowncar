using clowncar.Models;
using Mono.Options;
using System;
using System.Collections.Generic;

namespace clowncar
{
    class Program
    {
        static readonly string FileName = "clowncar";
        static readonly string AssemblyVersion = "0.0.3";

        static int Main(string[] args)
        {
            var state = new Settings();
            var show_help = false;
            var p = new OptionSet() {
                {"m|rawmarkdown=",  "the raw markdown *", v => state.RawMarkdown = v},
                {"f|file=",         "input file name *",  v => state.FileName = v},
                {"p|path=",         "path *",             v => state.InputPath = v },
                {"r|recurse",       "recurse",            v => state.Recurse = v != null },
                {"o|output=",       "output path",        v => state.OutputPath = v },
                {"t|template=",     "template file name",      v => state.DefaultTemplate = v },
                {"n|notemplate",    "no template!",       v => state.NoTemplate = v != null },
                {"d|dry-run",       "dry run doesn't change files",            v => state.DryRun = v != null },
                {"z|lessnoise",     "less noise in output",v=> state.LessNoise = v != null },
                {"?|h|help",        "this message",       v => show_help = v != null },
            };

            List<string> extra;

            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
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

            if (!state.Validate(out string validationError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(validationError);
                Console.ResetColor();
                return 3;
            }

            //************************************************
            //************************************************
            var (success, errors) = new Runner(state).Run().GetAwaiter().GetResult();
            //************************************************
            //************************************************

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(errors);
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
                Console.Error.WriteLine("Error. Unrecognized commandline option" + (p.UnrecognizedOptions.Count > 1 ? "s" : "") + ".");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.ResetColor();
                foreach (var s in p.UnrecognizedOptions)
                {
                    Console.Out.Write("Unrecognized: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Out.WriteLine(s);
                    Console.ResetColor();
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Out.WriteLine("Please see below for all valid options.");
                Console.ResetColor();
            }
            else
            {
                WriteLogo();

                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.Write($"{Environment.NewLine}{FileName}"); Console.ResetColor();
                Console.Out.WriteLine(" version " + AssemblyVersion.ToString());
                Write("`aturn markdown to html `W(`Ro`W:`E>`Y*");
            }

            Console.Out.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Out.Write("clowncar ");
            Console.ResetColor();
            Console.Out.WriteLine("[options]");
            Console.Out.WriteLine();
            Console.Out.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static void WriteLogo()
        {
            lock (Console.Out)
            {
                Write(Environment.NewLine);
                Write(@"`r===================================================`M  *");
                Write(@"`R-------------------------------------------------`E   /`Mo`E\");
                Write(@"`Y......`C__`Y........................................`R  {`W('`RO`W')`R}");
                Write(@"`C.---.|  |.----.--.--.--.-----.----.---.-.----.  `WC`E((/ `Mo`E \))`WD");
                Write(@"`C|  __|  |  _  |  |  |  |     |  __|  _  |   _| `M __`E(__|__)`M__");
                Write(@"`C|____|__|_____|________|__|__|____|___._|__|  `M (_____|_____)");
            }
        }
        private static void Write(string message, bool noNewLine = false)
        {
            // Always put a color at the start of the line, if it doesn't start with a color.
            if (!message.StartsWith("`")) message = "`G" + message;

            var parts = message.Split("`", StringSplitOptions.RemoveEmptyEntries);
            if (parts != null && parts.Length > 0)
            {
                foreach (var p in parts)
                {
                    var color = ParseColor(p[0]);
                    Console.ForegroundColor = color;
                    Console.Write(p.Substring(1));
                    Console.ResetColor();
                }
            }

            if (!noNewLine) Console.Write(Environment.NewLine);
        }

        private static readonly Dictionary<char, ConsoleColor> colors = new Dictionary<char, ConsoleColor>
        {
            { 'l', ConsoleColor.Black },
            { 'b', ConsoleColor.DarkBlue },
            { 'g', ConsoleColor.DarkGreen },
            { 'c', ConsoleColor.DarkCyan },
            { 'r', ConsoleColor.DarkRed },
            { 'm', ConsoleColor.DarkMagenta },
            { 'y', ConsoleColor.DarkYellow },
            { 'A', ConsoleColor.Gray },
            { 'a', ConsoleColor.DarkGray },
            { 'B', ConsoleColor.Blue },
            { 'E', ConsoleColor.Green },
            { 'C', ConsoleColor.Cyan },
            { 'R', ConsoleColor.Red },
            { 'M', ConsoleColor.Magenta },
            { 'Y', ConsoleColor.Yellow },
            { 'W', ConsoleColor.White },
        };

        private static ConsoleColor ParseColor(char v)
            => colors.TryGetValue(v, out ConsoleColor color) ? color : ConsoleColor.Gray;
    }
}
