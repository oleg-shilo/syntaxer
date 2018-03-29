using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Intellisense.Common;
using RoslynIntellisense;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.RegularExpressions;

namespace Syntaxer
{
    class SyntaxProvider
    {
        static public string ProcessRequest(Args args)
        {
            try
            {
                if (args.script.HasText() && Path.GetExtension(args.script).ToLower() == ".vb")
                    Autocompleter.Language = "VB";

                if (args.popen.HasText())
                {
                    string[] parts = args.popen.Split('|').ToArray();
                    string exe = parts.FirstOrDefault();
                    if (parts.Count() == 2)
                        Process.Start(parts.First(), parts.Last());
                    else if (parts.Count() == 1)
                        Process.Start(parts.First());
                    else
                        return "<error>Invalid 'popen' arguments. Must be <exe>[|<args>]";
                    return null;
                }

                if (args.pkill)
                {
                    PKill(args.pid, args.pname);
                    return null;
                }

                if (args.cscs_path != null)
                {
                    if (csscript.cscs_path != args.cscs_path)
                    {
                        csscript.cscs_path = args.cscs_path;
                        Output.WriteLine(" >> cscs.exe is remapped to: " + csscript.cscs_path);
                    }
                    return null;
                }

                if (!File.Exists(args.script))
                    if (args.script.HasText())
                        return $"<error>File '{args.script}' doesn't exist";
                    else
                        return null;

                string result = "";

                if (args.op == "references")
                    result = FindRefreneces(args.script, args.pos);
                else if (args.op.StartsWith("suggest_usings:"))
                    result = FindUsings(args.script, args.op.Split(':').Last(), args.rich);
                else if (args.op == "resolve")
                    result = Resolve(args.script, args.pos, args.rich);
                else if (args.op == "completion")
                    result = GetCompletion(args.script, args.pos);
                else if (args.op.StartsWith("tooltip:"))
                    result = GetTooltip(args.script, args.pos, args.op.Split(':').Last(), args.short_hinted_tooltips == 1);
                else if (args.op.StartsWith("memberinfo"))
                    result = GetMemberInfo(args.script, args.pos, args.collapseOverloads);
                else if (args.op == "project")
                    result = GenerateProjectFor(args.script);
                else if (args.op == "codemap")
                    result = CodeMap(args.script, args.rich, false);
                else if (args.op == "codemap_vscode")
                    result = CodeMap(args.script, args.rich, vsCode: true);
                else if (args.op == "format")
                {
                    Output.WriteLine("FormatCode>");
                    int caretPos = args.pos;
                    var formattedCode = FormatCode(args.script, ref caretPos);
                    Output.WriteLine("<FormatCode");
                    result = $"{caretPos}\n{formattedCode}";
                }
                if (string.IsNullOrEmpty(result))
                    return "<null>";
                else
                    return result;
            }
            catch (Exception e)
            {
                return "<error>" + e;
            }
            finally
            {
                Autocompleter.Language = "C#";
            }
        }

        internal static string Resolve(string script, int offset, bool rich_serialization)
        {
            Output.WriteLine("Resolve");

            DomRegion region = ResolveRaw(script, offset);

            if (rich_serialization)
            {
                return DomRegion.Serialize(region);
            }
            else
            {
                var result = new StringBuilder();
                result.AppendLine("file:" + region.FileName);
                result.AppendLine("line:" + region.BeginLine);
                return result.ToString();
            }
        }

        static string GetAssociatedFileExtensions(string directive)
        {
            return directive.FirstMatch(("//css_inc", ".cs"),
                                        ("//css_include", ".cs"),
                                        ("//css_ref", ".dll|.exe"),
                                        ("//css_reference", ".dll|.exe"));
        }

        internal static bool ParseAsCssDirective(string script, string code, int offset, Action<string> onDirective, Action<string, string, string> onDirectiveArg, bool ignoreEmptyArgs = true)
        {
            string word = code.WordAt(offset, true);
            string line = code.LineAt(offset);
            if (word.StartsWith("//css_") || line.TrimStart().StartsWith("//css_"))
            {
                var directive = line.TrimStart().WordAt(2, true);
                string extensions = GetAssociatedFileExtensions(directive);

                var arg = word;

                if (arg.IsEmpty() && ignoreEmptyArgs)
                {
                    onDirective(directive);
                }
                else if (word.StartsWith("//css_"))
                {
                    onDirective(directive);
                }
                else
                {
                    if (extensions == null) // directive that does not include file
                    {
                        onDirective(directive);
                    }
                    else
                    {
                        onDirectiveArg(directive, word, extensions);
                    }
                }

                return true;
            }

            return false;
        }

        internal static string LookopDirectivePath(string script, int offset, string directive, string word, string extensions = null)
        {
            extensions = extensions ?? GetAssociatedFileExtensions(directive);

            if (extensions != null && word.HasText()) // file of the //css_inc or //css_ref directive
            {
                bool ignoreCase = Utils.IsWinows;

                var probing_dirs = CSScriptHelper.GenerateProjectFor(script)
                                                 .SearchDirs;

                var match = probing_dirs.Select(dir => Directory.GetFiles(dir, "*")
                                                                .FirstOrDefault(file =>
                                                                {
                                                                    return Path.GetFileName(file).IsSameAs(word, ignoreCase)
                                                                                   ||
                                                                                  (Path.GetFileNameWithoutExtension(file).IsSameAs(word, ignoreCase) &&
                                                                                   extensions.Contains(Path.GetExtension(file)));
                                                                }))
                                        .FirstOrDefault(x => x != null);

                return match;
            }
            return null;
        }

        internal static string[] LookupDirectivePaths(string script, int offset, string directive, string word, string extensions)
        {
            if (extensions != null)
            {
                var originalFile = script.Replace(".$temp$.", "."); // it may be temp file
                return CSScriptHelper.GenerateProjectFor(script)
                                            .SearchDirs
                                            .SelectMany(dir => extensions.Split('|').Select(x => new { ProbingDir = dir, FileExtension = x }))
                                            .SelectMany(x => Directory.GetFiles(x.ProbingDir, "*" + x.FileExtension))
                                            .Where(x => !x.Contains(".$temp$.") && // exclude all temp files
                                                         x != originalFile)
                                            .ToArray();
            }
            return new string[0];
        }

        internal static DomRegion ResolveRaw(string script, int offset)
        {
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            DomRegion region = DomRegion.Empty;

            ParseAsCssDirective(script, code, offset,
                directive =>
                {
                    region = CssSyntax.Resolve(directive);
                },
                (directive, arg, extensions) =>
                {
                    if (LookopDirectivePath(script, offset, directive, arg) is string file)
                        region = new DomRegion
                        {
                            BeginColumn = -1,
                            BeginLine = -1,
                            EndLine = -1,
                            FileName = file,
                            IsEmpty = false
                        };
                    else
                        region = CssSyntax.Resolve(directive);
                });

            if (region.IsEmpty)
            {
                bool decorated = false;
                if (!script.EndsWith(".g.cs"))
                    decorated = CSScriptHelper.DecorateIfRequired(ref code, ref offset);

                Project project = CSScriptHelper.GenerateProjectFor(script);
                var sources = project.Files
                                     .Where(f => f != script)
                                     .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                     .ToArray();

                region = Autocompleter.ResolveSymbol(code, offset, script, project.Refs, sources);
                if (decorated && region.FileName == script)
                    CSScriptHelper.Undecorate(code, ref region);
            }

            return region;
        }

        static void PKill(int pid, string childNameHint = null)
        {
            try
            {
                bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
                bool isMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

                if (!isLinux && !isMac && childNameHint != null)
                    System.Diagnostics.Process.GetProcessById(pid)?.KillGroup(p => p.ProcessName.IsSameAs(childNameHint, true));
                else
                    System.Diagnostics.Process.GetProcessById(pid)?.Kill();
            }
            catch { }
        }

        internal static string FindRefreneces(string script, int offset)
        {
            Output.WriteLine("FindRefreneces");

            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            if (!script.EndsWith(".g.cs"))
                CSScriptHelper.DecorateIfRequired(ref code, ref offset);

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                 .ToArray();

            var regions = Autocompleter.FindReferencess(code, offset, script, project.Refs, sources);

            return regions.JoinBy("\n");
        }

        internal static string FindUsings(string script, string word, bool rich_serialization)
        {
            Output.WriteLine("FindUsings");
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f));

            var regions = Autocompleter.GetNamespacesFor(code, word, project.Refs, sources);
            if (rich_serialization)
                return regions.Select(Intellisense.Common.TypeInfo.Serialize).JoinSerializedLines();
            else
                return regions.Select(x => x.Namespace).JoinBy("\n");
        }

        internal static string FormatCode(string script, ref int caret)
        {
            Output.WriteLine("FormatCode");

            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            bool escape_vs_template_var = code.Contains("$safeprojectname$");

            if (escape_vs_template_var)
                code = code.Replace("$safeprojectname$", "_safeprojectname_");

            string formattedCode = RoslynIntellisense.Formatter.FormatHybrid(code, "code.cs");

            if (escape_vs_template_var)
                formattedCode = formattedCode.Replace("_safeprojectname_", "$safeprojectname$");

            caret = SyntaxMapper.MapAbsPosition(code, caret, formattedCode);

            return formattedCode;
        }

        internal static string CodeMap(string script, bool rich_serialization, bool vsCode)
        {
            csscript.Log("CodeMap");
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            CodeMapItem[] members = CSScriptHelper.GetMapOf(code, true).OrderBy(x => x.ParentDisplayName).ToArray();

            bool notepad_pp = rich_serialization;
            bool vs_code = vsCode;
            bool sublime = !vsCode && !notepad_pp;

            if (notepad_pp)
            {
                return members.Select(CodeMapItem.Serialize).JoinSerializedLines();
            }

            if (vs_code)
            {
                // https://github.com/oleg-shilo/codemap.vscode/wiki/Adding-custom-mappers
                // [indent]<name>|<line>|<icon>

                //see "static AutoCompiler.CodeMapItem[] GetMapOfCSharp(string code, bool decorated)" for possible MemberType

                var result = new StringBuilder();
                var lines = members.Select(x =>
                    new
                    {
                        ParentType = (x.ParentDisplayName).Trim(),
                        ParentTypeIcon = x.ParentDisplayType == "interface" ? "interface" : "class",
                        Indent = "  ",
                        Name = x.DisplayName,
                        Line = x.Line,
                        Icon = (x.MemberType == "Enum" ? "class" :
                                x.MemberType == "Method" ? "function" :
                                x.MemberType == "Property" ? "property" :
                                x.MemberType == "Field" ? "field" :
                                "none")
                    });

                if (lines.Any())
                {
                    foreach (var group in lines.GroupBy(x => x.ParentType))
                    {
                        bool first = true;
                        foreach (var item in group)
                        {
                            var parentType = group.Key;
                            if (first)
                            {
                                // do not pass any line number for class as it may be declared
                                // at multiple locations (e.g. partial classes)
                                first = false;
                                result.AppendLine($"{parentType}||{item.ParentTypeIcon}");
                            }
                            result.AppendLine($"{item.Indent}{item.Name}|{item.Line}|{item.Icon}");
                        }
                    }
                }
                return result.ToString().Trim();
            }

            if (sublime)
            {
                var result = new StringBuilder();
                var lines = members.Select(x =>
                                    {
                                        return new
                                        {
                                            Type = (x.ParentDisplayType + " " + x.ParentDisplayName).Trim(),
                                            Content = "    " + x.DisplayName,
                                            Line = x.Line
                                        };
                                    });

                if (lines.Any())
                {
                    int maxLenghth = lines.Select(x => x.Type.Length).Max();
                    maxLenghth = Math.Max(maxLenghth, lines.Select(x => x.Content.Length).Max());

                    string prevType = null;
                    foreach (var item in lines)
                    {
                        if (prevType != item.Type)
                        {
                            result.AppendLine();
                            result.AppendLine(item.Type);
                        }

                        prevType = item.Type;
                        var suffix = new string(' ', maxLenghth - item.Content.Length);
                        result.AppendLine($"{item.Content}{suffix} :{item.Line}");
                    }
                }
                return result.ToString().Trim();
            }

            return null;
        }

        internal static string GetCompletion(string script, int caret)
        {
            Output.WriteLine("GetCompletion");

            var result = new StringBuilder();

            foreach (ICompletionData item in GetCompletionRaw(script, caret))
            {
                string type = item.CompletionType.ToString().Replace("_event", "event").Replace("_namespace", "namespace");
                string completion = item.CompletionText;
                string display = item.DisplayText;
                if (item.CompletionType == CompletionType.method)
                {
                    if (item.HasOverloads)
                    {
                        display += "(...)";
                        //completion += "(";
                    }
                    else
                    {
                        if (item.InvokeParameters.Count() == 0)
                        {
                            display += "()";
                            completion += "()";
                        }
                        else
                        {
                            display += "(..)";
                            //completion += "(";
                        }
                    }
                }

                result.AppendLine($"{display}\t{type}|{completion}");
            }

            //Console.WriteLine(">>>>>" + result.ToString().Trim());
            return result.ToString().Trim();
        }

        internal static IEnumerable<ICompletionData> GetCompletionRaw(string script, int caret)
        {
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            IEnumerable<ICompletionData> completions = null;

            bool wasHandled = ParseAsCssDirective(script, code, caret,
              (directive) =>
              {
                  completions = CssCompletionData.AllDirectives;
              },
              (directive, arg, extensions) =>
              {
                  completions = LookupDirectivePaths(script, caret, directive, arg, extensions)
                                                .Select(file => new CssCompletionData
                                                {
                                                    CompletionText = Path.GetFileName(file),
                                                    DisplayText = Path.GetFileName(file),
                                                    Description = "File: " + file,
                                                    CompletionType = CompletionType.file
                                                });
              },
              ignoreEmptyArgs: false);

            if (!wasHandled)
            {
                if (!script.EndsWith(".g.cs"))
                    CSScriptHelper.DecorateIfRequired(ref code, ref caret);

                Project project = CSScriptHelper.GenerateProjectFor(script);
                var sources = project.Files
                                     .Where(f => f != script)
                                     .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                     .ToArray();

                completions = Autocompleter.GetAutocompletionFor(code, caret, project.Refs, sources);

                var count = completions.Count();
            }
            return completions;
        }

        internal static string GetTooltip(string script, int caret, string hint, bool shortHintedTooltips)
        {
            // Simplified API for ST3
            Output.WriteLine("GetTooltip");
            //Console.WriteLine("hint: " + hint);

            string result = null;
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            void loockupDirective(string directive)
            {
                var css_directive = CssCompletionData.AllDirectives
                                                        .FirstOrDefault(x => x.DisplayText == directive);
                if (css_directive != null)
                {
                    result = $"Directive: {css_directive.DisplayText}\n{css_directive.Description}";
                    result = result.NormalizeLineEnding().Replace("\r\n\r\n", "\r\n").TrimEnd();
                }
            };

            ParseAsCssDirective(script, code, caret,
               loockupDirective,
               (directive, arg, extensions) =>
               {
                   if (LookopDirectivePath(script, caret, directive, arg) is string file)
                       result = $"File: {file}";
                   else
                       loockupDirective(directive);
               });

            if (result.HasText())
            {
                return result;
            }

            if (!script.EndsWith(".g.cs"))
                CSScriptHelper.DecorateIfRequired(ref code, ref caret);

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                 .ToArray();

            int methodStartPosTemp;
            var items = Autocompleter.GetMemberInfo(code, caret, out methodStartPosTemp, project.Refs, sources, includeOverloads: hint.HasAny());
            if (hint.HasAny())
            {
                if (shortHintedTooltips)
                    items = items.Select(x => x.Split('\n').FirstOrDefault()).ToArray();

                int count = hint.Split(',').Count();
                result = items.FirstOrDefault(x =>
                {
                    return SyntaxMapper.GetArgumentCount(x) == count;
                })
                ?? items.FirstOrDefault();

                bool hideOverloadsSummary = false;
                if (result != null && hideOverloadsSummary)
                {
                    var lines = result.Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
                    //(+ 1 overloads)
                    if (lines[0].EndsWith(" overloads)"))
                    {
                        try
                        {
                            lines[0] = lines[0].Split(new[] { "(+" }, StringSplitOptions.None).First().Trim();
                        }
                        catch { }
                    }
                    result = lines.JoinBy("\n");
                }
            }
            else
                result = items.FirstOrDefault();

            if (result.HasText())
                result = result.NormalizeLineEnding().Replace("\r\n\r\n", "\r\n").TrimEnd();

            return result;
        }

        internal static string GetMemberInfo(string script, int caret, bool collapseOverloads)
        {
            // Complete API for N++

            Output.WriteLine("GetMemberInfo");
            //Console.WriteLine("hint: " + hint);

            string result = null;
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            void loockupDirective(string directive)
            {
                var css_directive = CssCompletionData.AllDirectives
                                                     .FirstOrDefault(x => x.DisplayText == directive);
                if (css_directive != null)
                {
                    result = $"Directive: {css_directive.DisplayText}\n{css_directive.Description}";
                    result = result.NormalizeLineEnding().Replace("\r\n\r\n", "\r\n").TrimEnd();
                }
            };

            ParseAsCssDirective(script, code, caret,
               loockupDirective,
               (directive, arg, extensions) =>
               {
                   if (LookopDirectivePath(script, caret, directive, arg) is string file)
                       result = $"File: {file}";
                   else
                       loockupDirective(directive);
               });

            if (result.HasText())
            {
                return MemberInfoData.Serialize(new MemberInfoData { Info = result });
            }

            if (!script.EndsWith(".g.cs"))
                CSScriptHelper.DecorateIfRequired(ref code, ref caret);

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                 .ToArray();

            int methodStartPosTemp;
            var items = Autocompleter.GetMemberInfo(code, caret, out methodStartPosTemp, project.Refs, sources, includeOverloads: !collapseOverloads);

            if (collapseOverloads)
                items = items.Take(1);

            result = items.Select(x => new MemberInfoData { Info = x, MemberStartPosition = methodStartPosTemp })
                          .Select(MemberInfoData.Serialize)
                          .JoinSerializedLines();

            return result;
        }

        static string GenerateProjectFor(string script)
        {
            //MessageBox.Show(typeof(Project).Assembly.Location, typeof(Project).Assembly.GetName().ToString());

            var result = new StringBuilder();

            Project project = CSScriptHelper.GenerateProjectFor(script);
            foreach (string file in project.Files)
                result.AppendLine($"file:{file}");

            foreach (string file in project.Refs)
                result.AppendLine($"ref:{file}");

            return result.ToString().Trim();
        }

        public static string testCode = @"using System;
using System.Windows.Forms;

class Script
{
    [STAThread]
    static public void Main(string[] args)
    {
        MessageBox.Show(""Just a test!"");

        for (int i = 0; i<args.Length; i++)
        {
        	var t = args[0].Length.ToString().GetHashCode();
            Console.WriteLine(args[i]);
        }
    }
}";

        public static string testCode7b = @"
using System;
using System.Linq;
using System.Collections.Generic;
using static dbg; // to use 'print' instead of 'dbg.print'

class Script
{
    static public void Main(string[] args)
    {
        (string message, int version) setup_say_hello()
        {
            return (""Hello from C#"", 7);
        }

        var info = setup_say_hello();

        print(info.message, info.version);

        print(Environment.GetEnvironmentVariables()
                            .Cast<object>()
                            .Take(5));

        Console.WriteLine(777);
    }
}";

        public static string testCode7 = @"using System;
using System.Windows.Forms;

class Script
{
    [STAThread]
    static public void Main(string[] args)
    {
        MessageBox.Show(""Just a test!"");

        for (int i = 0; i<args.Length; i++)
        {
        	var t = args[0].Length.ToString().GetHashCode();
            Console.WriteLine(args[i]);

            void test()
            {
                Console.WriteLine(""Local function - C#7"");
            }

            tes
            // var tup = (1,2);
        }
    }
}";
    }
}