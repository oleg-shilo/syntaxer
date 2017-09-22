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

namespace Syntaxer
{
    public class Test
    {
        public static void All()
        {
            // Test.Resolving();
            // Test.Format();
            // Test.Project();
            // Test.Completion();

            // Test.CSSCompletion();
            // Test.CSSResolving();
            Test.CSSResolving2();
            // Test.CSSTooltipResolving();
        }

        public static void Format()
        {
            Output.Write("Formatting: ");

            // $safeprojectname$

            try
            {
                // var dummyWorkspace = MSBuildWorkspace.Create();
                // SyntaxTree tree = CSharpSyntaxTree.ParseText(SyntaxProvider.testCode.Trim());
                // SyntaxNode root = Microsoft.CodeAnalysis.Formatting.Formatter.Format(tree.GetRoot(), dummyWorkspace);
                RoslynIntellisense.Formatter.FormatHybrid(SyntaxProvider.testCode, "code.cs");
                Output.WriteLine("OK");
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
        }

        static void TestScript(Action<string> action, bool local = false)
        {
            var script = Path.GetTempFileName();

            if (local)
            {
                var localFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                    .PathJoin(Path.GetFileName(script));

                File.Move(script, localFile);

                script = localFile;
            }

            try
            {
                var currDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var cscs = Path.Combine(currDir, "cscs.exe");
                if (File.Exists(cscs))
                    csscript.cscs_path = cscs;
                else
                {
                    cscs = Path.Combine(Path.GetDirectoryName(currDir), "cscs.exe");
                    if (File.Exists(cscs))
                        csscript.cscs_path = cscs;
                    else
                        csscript.cscs_path = "./cscs.exe";
                }

                action(script);
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
            finally
            {
                try { File.Delete(script); } catch { }
            }
        }

        public static void Project()
        {
            TestScript(script =>
            {
                Output.WriteLine("Generating project: ");

                Project project = CSScriptHelper.GenerateProjectFor(script);
                project.Files.ToList().ForEach(x => Output.WriteLine("    file: " + x));
                project.Refs.ToList().ForEach(x => Output.WriteLine("    ref: " + x));
                project.SearchDirs.ToList().ForEach(x => Output.WriteLine("    searchDir: " + x));

                Output.WriteLine("OK - " + project.Files.Concat(project.Refs).Concat(project.SearchDirs).Count() + " project item(s)");
            });
        }

        public static void Completion()
        {
            TestScript(script =>
            {
                Console.Write("Autocompletion: ");

                string code = SyntaxProvider.testCode7b;

                File.WriteAllText(script, code);

                var caret = code.IndexOf("info.ver") + "info.ver".Length;
                string word = code.WordAt(caret);

                var completions = Services.GetCompletion(script, caret);

                Output.WriteLine("OK - " + completions.Count() + " completion item(s)...");
                Output.WriteLine("    '" + completions.GetLines().FirstOrDefault(x => x.StartsWith(word)) + "'");
            });
        }

        public static void CSSCompletion()
        {
            TestScript(script =>
            {
                Console.Write("CS-Script Autocompletion: ");

                File.WriteAllText(script, "  //css_inc  test.cs");

                var caret = 5;
                var completions = SyntaxProvider.GetCompletion(script, caret);

                Output.WriteLine("OK");

                caret = 12;

                completions = SyntaxProvider.GetCompletion(script, caret);

                File.WriteAllText(script, "  //css_inc  cmd.cs");
                caret = 12;

                completions = SyntaxProvider.GetCompletion(script, caret);

                caret = 15;

                completions = SyntaxProvider.GetCompletion(script, caret);

                // Console.WriteLine("    '" + completions.Split('\n').FirstOrDefault(x => x.StartsWith(word)) + "'");
            }, local: true);
        }

        // static void TestCSSCompletion2()
        // {
        //     TestScript(script =>
        //     {
        //         Console.Write("CS-Script 'Include' Autocompletion: ");

        //         string code = "  //css_inc  test.cs";
        //         File.WriteAllText(script, code);

        //         var caret = 12;
        //         var completions = SyntaxProvider.GetCompletion(script, caret);

        //         Console.WriteLine("OK");

        //         caret = 12;
        //         var word = code.WordAt(caret, true);
        //         var line = code.LineAt(caret);

        //         completions = SyntaxProvider.GetCompletion(script, caret);

        //         // Console.WriteLine("    '" + completions.Split('\n').FirstOrDefault(x => x.StartsWith(word)) + "'");
        //     });
        // }

        public static void CSSResolving()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol: ");
                string code = "  //css_ref test.dll;";

                File.WriteAllText(script, code);

                var caret = 5;
                var info = Services.Resolve(script, caret);

                Output.WriteLine("OK");
            });
        }

        public static void CSSResolving2()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol: ");
                string code = "//css_inc cmd.cs;";
                // string code = "//css_ref cmd.dll;";

                File.WriteAllText(script, code);

                var caret = 13;
                var info = Services.Resolve(script, caret);
                Output.WriteLine(info);
                Output.WriteLine("OK");
            });
        }

        public static void CSSTooltipResolving()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol to tooltip: ");
                // string code = "  //css_ref test.dll;";
                string code = "  //css_inc cmd.cs;";

                File.WriteAllText(script, code);

                var caret = 13;
                // var caret = 5;

                string info = Services.GetTooltip(script, caret, null, true);
                Output.WriteLine(info);
                Output.WriteLine("OK");
            });
        }

        public static void Resolving()
        {
            TestScript(script =>
            {
                Output.Write("Resolve symbol: ");
                string code = SyntaxProvider.testCode7b;

                File.WriteAllText(script, code);

                var pattern = "Console.Write";
                // pattern = "info.ver";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = Services.Resolve(script, caret);

                string info = "";

                Output.WriteLine("OK - " + info.Count() + " symbol info item(s)...");
                Output.WriteLine("    '" + info.GetLines().FirstOrDefault() + "'");
            });
        }
    }
}