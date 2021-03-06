using System;
using System.Linq;
using System.IO;
using System.Reflection;
using RoslynIntellisense;
using Microsoft.CodeAnalysis;

namespace Syntaxer
{
    public class Test
    {
        public static void All()
        {
            var trigegr_loadig_var = csscript.Cscs_asm;
            // Test.SuggestUsings(); return;
            // Test.SignatureHelp(); return;
            // Test.Resolving();
            // Test.AssignmentCompletion(); return;
            // Test.Renaming();
            Test.CodeMapVSCode();
            // Test.Format();
            // Test.Project();
            // Test.Tooltip();

            // Test.CSSCompletion();
            // Test.CSSResolving();
            // Test.CSSResolving2();
            // Test.CSSTooltipResolving();
        }

        public static void Format()
        {
            Output.WriteLine("---");
            Output.Write("Formatting: ");

            // $safeprojectname$

            try
            {
                // var dummyWorkspace = MSBuildWorkspace.Create();
                // SyntaxTree tree = CSharpSyntaxTree.ParseText(SyntaxProvider.testCode.Trim());
                // SyntaxNode root = Microsoft.CodeAnalysis.Formatting.Formatter.Format(tree.GetRoot(), dummyWorkspace);
                RoslynIntellisense.Formatter.FormatHybrid(SyntaxProvider.testFreestyleCode, "code.cs");
                Output.WriteLine("OK");
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
        }

        public static void CodeMapVSCode()
        {
            var script = Path.GetTempFileName();

            Output.WriteLine("---");
            Output.Write("CodeMap-VSCode: ");

            try
            {
                var code = @"//css_autoclass 
using System;

void main()
{
    void ttt()
    {
    }
}

//css_ac_end

static class Extensions
{
    static public void Convert(this string text)
    {
    }
}";
                File.WriteAllText(script, code);
                var map = SyntaxProvider.CodeMap(script, false, true);

                Output.WriteLine("OK");
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
                Output.WriteLine("---");
                Output.WriteLine("Generating project: ");

                Project project = CSScriptHelper.GenerateProjectFor(new SourceInfo(script));
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
                Output.WriteLine("---");
                Console.Write("Autocompletion: ");

                string code = SyntaxProvider.testCode7b;

                File.WriteAllText(script, code);

                var caret = code.IndexOf("info.ver") + "info.ver".Length;
                string word = code.WordAt(caret);

                var completions = TestServices.GetCompletion(script, caret);

                Output.WriteLine("OK - " + completions.Count() + " completion item(s)...");
                Output.WriteLine("    '" + completions.GetLines().FirstOrDefault(x => x.StartsWith(word)) + "'");
            });
        }

        public static void AssignmentCompletion()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Console.Write("AssignmentCompletion: ");

                string code = SyntaxProvider.testCode7b;

                // System.IO.StreamReader file =

                File.WriteAllText(script, code);

                var pattern = "System.IO.StreamReader file =";
                pattern = "f.DialogResult =";
                pattern = "Form form =";
                pattern = "Form form = new";
                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var completions = TestServices.GetCompletion(script, caret);

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
                var info = TestServices.Resolve(script, caret);

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
                var info = TestServices.Resolve(script, caret);
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

                string info = TestServices.GetTooltip(script, caret, null, true);
                Output.WriteLine(info);
                Output.WriteLine("OK");
            });
        }

        public static void Resolving()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("Resolve symbol: ");
                string code = SyntaxProvider.testCode7b;

                File.WriteAllText(script, code);

                var pattern = "Console.Write";
                // pattern = "info.ver";
                pattern = "System.IO.StreamReader fi";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = TestServices.Resolve(script, caret);

                Output.WriteLine("OK - " + 1 + " symbol info item(s)...");
                Output.WriteLine("    '" + region.GetLines().FirstOrDefault() + "'");
            });
        }

        public static void Renaming()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("Generate renaming info: ");
                string code = SyntaxProvider.testCodeClass;

                File.WriteAllText(script, code);

                var pattern = "class Scr";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = TestServices.FindRefreneces(script, caret, "all");

                Output.WriteLine("OK - " + 1 + " symbol info item(s)...");
                Output.WriteLine("    '" + region.GetLines().FirstOrDefault() + "'");
            });
        }

        public static void SignatureHelp()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("Generate signature help: ");
                string code = @"using System;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(22
        // Console.WriteLine(5.ToString(), 33,
    }
}";
                // Console.WriteLine(22,

                File.WriteAllText(script, code);

                // var pattern = "WriteLine(";
                var pattern = "22";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = TestServices.GetSignatureHelp(script, caret);

                Output.WriteLine("OK - " + 1 + " symbol info item(s)...");
                Output.WriteLine("    '" + region.GetLines().FirstOrDefault() + "'");
            });
        }

        public static void SuggestUsings()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("SuggestUsings: ");
                string code = @"using System;
class Program
{
    static void Main(string[] args)
    {
        File
    }
}";

                File.WriteAllText(script, code);

                var pattern = "File";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = TestServices.FindUsings(script, "File");

                Output.WriteLine("OK - " + 1 + " symbol info item(s)...");
                Output.WriteLine("    '" + region.GetLines().FirstOrDefault() + "'");
            });
        }

        public static void Tooltip()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("Get tooltip: ");
                string code = SyntaxProvider.testCode7b;

                File.WriteAllText(script, code);

                var pattern = "Console.Write";
                // pattern = "info.ver";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var tooltip = TestServices.GetTooltip(script, caret, null, true);

                Output.WriteLine("OK");
                Output.WriteLine("    '" + tooltip.GetLines().FirstOrDefault() + "'");
            });
        }
    }
}