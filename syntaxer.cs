using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

//using csscript;

// Shockingly there is no one truly transparent IPC solution for Win, Linux, Mac
// - Named-pipes are not implemented on Linux
// - Sockets seems to be a good portable approach but they claim port (no biggie though)
// but on Windows opening socket server will require granting special permissions. Meaning a
// "frightening" confirmation dialog that is not a good for UX.
//  - Unix domain socket plays the same role as named-pipes but with Socket interface: not portable
// on Win and create file anyway. BTW the file that may be left on the system:
//  http://mono.1490590.n4.nabble.com/Unix-domain-sockets-on-mono-td1490601.html
// - Unix-pipes then closest Win named-pipes equivalent are still OS specific and create a file as well.
// ---------------------
// Bottom line: the only a compromise solution, which is simple and portable is to use a plain socket.
//  - ultimately portable
//  - fast enough (particularly with request rate we need to meet - less than 1 request per 3 sec)
//  - requires no special permissions on Linux (even if it does on Win)
//  - full control of cleanup (as there is none)

namespace Syntaxer
{
    // There is NodeLabelEditEventArgs warranty that `Console.WriteLine` is always a safe call.
    // Particularly because the code is to be run on various OS and runtimes.
    // Thus accessing Console.Encoding on Windows from WinForm app raises the exception
    static class Output
    {
        public static void WriteLine(object value)
        {
            try { Console.WriteLine(value); } catch { }
        }

        public static void Write(object value)
        {
            try { Console.Write(value); } catch { }
        }
    }

    class Server
    {
        static void Main(string[] args)
        {
            DeployCSScriptIntegration();

            var input = new Args(args);

            if (input.dr)
                DeployRoslyn();

            // -listen -timeout:60000 -cscs_path:./cscs.exe
            if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
            {
                DeployRoslyn();
            }
            else
            {
                // LoadCSScriptIntegration();
                LoadRoslyn();
            }
            mono_root = Path.GetDirectoryName(typeof(string).Assembly.Location);
            Output.WriteLine(mono_root);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Run(input);
        }

        static string mono_root;

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Probe(mono_root, args.Name) ??
                   Probe(mono_root.PathJoin("Fasades"), args.Name);

            // return Probe("/usr/lib/mono/4.5", args.Name) ??
            //        Probe("/usr/lib/mono/4.5/Fasades", args.Name) ??
            //        Probe("/home/user/.vscode/extensions/ms-vscode.csharp-1.12.1/.omnisharp/omnisharp", args.Name);
        }

        static void Run(Args input)
        {
            if (input.test)
            {
                // TestResolving();
                // TestFormat();
                // TestProject();
                // TestCompletion();

                // TestCSSCompletion();
                // TestCSSResolving();
                TestCSSResolving2();
                // TestCSSTooltipResolving();
            }
            else
            {
                if (input.cscs_path != null)
                    csscript.cscs_path = input.cscs_path;

                if (input.listen)
                    SocketServer.Listen(input);
                else
                    Output.WriteLine(SyntaxProvider.ProcessRequest(input));
            }
        }

        static void DeployRoslyn()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            ForEachRoslynAssembly((name, bytes) =>
                File.WriteAllBytes(Path.Combine(dir, name), bytes));
        }

        static void DeployCSScriptIntegration()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            ForEachCSScriptAssembly((name, bytes) =>
                File.WriteAllBytes(Path.Combine(dir, name), bytes));
        }

        static void LoadCSScriptIntegration()
        {
            ForEachCSScriptAssembly((name, bytes) =>
                Assembly.Load(bytes));
        }

        static void LoadRoslyn()
        {
            ForEachRoslynAssembly((name, bytes) =>
                Assembly.Load(bytes));
        }

        static void ForEachCSScriptAssembly(Action<string, byte[]> action)
        {
            Action<string, byte[]> _action = (name, bytes) =>
            {
                try { action(name, bytes); } catch { }
            };
            // _action("CSSRoslynProvider.dll", syntaxer.Properties.Resources.CSSRoslynProvider);
            _action("Intellisense.Common.dll", syntaxer.Properties.Resources.Intellisense_Common);
            _action("RoslynIntellisense.exe", syntaxer.Properties.Resources.RoslynIntellisense);
        }

        static void ForEachRoslynAssembly(Action<string, byte[]> action)
        {
            Action<string, byte[]> _action = (name, bytes) =>
            {
                try { action(name, bytes); } catch { }
            };

            _action("csc.exe", syntaxer.Properties.Resources.csc_exe);
            _action("csc.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.csc_exe_config));
            _action("csc.rsp", syntaxer.Properties.Resources.csc_rsp);
            _action("csi.exe", syntaxer.Properties.Resources.csi_exe);
            _action("csi.rsp", syntaxer.Properties.Resources.csi_rsp);
            _action("Esent.Interop.dll", syntaxer.Properties.Resources.Esent_Interop);
            _action("Microsoft.Build.dll", syntaxer.Properties.Resources.Microsoft_Build);
            _action("Microsoft.Build.Framework.dll", syntaxer.Properties.Resources.Microsoft_Build_Framework);
            _action("Microsoft.Build.Tasks.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_CodeAnalysis);
            _action("Microsoft.Build.Tasks.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core);
            _action("Microsoft.Build.Utilities.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Utilities_Core);
            _action("Microsoft.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis);
            _action("Microsoft.CodeAnalysis.CSharp.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp);
            _action("Microsoft.CodeAnalysis.CSharp.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Scripting);
            _action("Microsoft.CodeAnalysis.CSharp.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Workspaces);
            _action("Microsoft.CodeAnalysis.Elfie.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Elfie);
            _action("Microsoft.CodeAnalysis.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Scripting);
            _action("Microsoft.CodeAnalysis.VisualBasic.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic);
            _action("Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic_Workspaces);
            _action("Microsoft.CodeAnalysis.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces);
            _action("Microsoft.CodeAnalysis.Workspaces.Desktop.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces_Desktop);
            _action("Microsoft.CSharp.Core.targets", syntaxer.Properties.Resources.Microsoft_CSharp_Core);
            _action("Microsoft.DiaSymReader.Native.amd64.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_amd64);
            _action("Microsoft.DiaSymReader.Native.x86.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_x86);
            _action("Microsoft.VisualBasic.Core.targets", syntaxer.Properties.Resources.Microsoft_VisualBasic_Core);
            _action("Microsoft.Win32.Primitives.dll", syntaxer.Properties.Resources.Microsoft_Win32_Primitives);
            _action("System.AppContext.dll", syntaxer.Properties.Resources.System_AppContext);
            _action("System.Collections.Immutable.dll", syntaxer.Properties.Resources.System_Collections_Immutable);
            _action("System.Composition.AttributedModel.dll", syntaxer.Properties.Resources.System_Composition_AttributedModel);
            _action("System.Composition.Convention.dll", syntaxer.Properties.Resources.System_Composition_Convention);
            _action("System.Composition.Hosting.dll", syntaxer.Properties.Resources.System_Composition_Hosting);
            _action("System.Composition.Runtime.dll", syntaxer.Properties.Resources.System_Composition_Runtime);
            _action("System.Composition.TypedParts.dll", syntaxer.Properties.Resources.System_Composition_TypedParts);
            _action("System.Console.dll", syntaxer.Properties.Resources.System_Console);
            _action("System.Diagnostics.FileVersionInfo.dll", syntaxer.Properties.Resources.System_Diagnostics_FileVersionInfo);
            _action("System.Diagnostics.Process.dll", syntaxer.Properties.Resources.System_Diagnostics_Process);
            _action("System.Diagnostics.StackTrace.dll", syntaxer.Properties.Resources.System_Diagnostics_StackTrace);
            _action("System.IO.Compression.dll", syntaxer.Properties.Resources.System_IO_Compression);
            _action("System.IO.FileSystem.dll", syntaxer.Properties.Resources.System_IO_FileSystem);
            _action("System.IO.FileSystem.DriveInfo.dll", syntaxer.Properties.Resources.System_IO_FileSystem_DriveInfo);
            _action("System.IO.FileSystem.Primitives.dll", syntaxer.Properties.Resources.System_IO_FileSystem_Primitives);
            _action("System.IO.Pipes.dll", syntaxer.Properties.Resources.System_IO_Pipes);
            _action("System.Reflection.Metadata.dll", syntaxer.Properties.Resources.System_Reflection_Metadata);
            _action("System.Security.AccessControl.dll", syntaxer.Properties.Resources.System_Security_AccessControl);
            _action("System.Security.Claims.dll", syntaxer.Properties.Resources.System_Security_Claims);
            _action("System.Security.Cryptography.Algorithms.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Algorithms);
            _action("System.Security.Cryptography.Encoding.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Encoding);
            _action("System.Security.Cryptography.Primitives.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Primitives);
            _action("System.Security.Cryptography.X509Certificates.dll", syntaxer.Properties.Resources.System_Security_Cryptography_X509Certificates);
            _action("System.Security.Principal.Windows.dll", syntaxer.Properties.Resources.System_Security_Principal_Windows);
            _action("System.Text.Encoding.CodePages.dll", syntaxer.Properties.Resources.System_Text_Encoding_CodePages);
            _action("System.Threading.Tasks.Dataflow.dll", syntaxer.Properties.Resources.System_Threading_Tasks_Dataflow);
            _action("System.Threading.Thread.dll", syntaxer.Properties.Resources.System_Threading_Thread);
            _action("System.ValueTuple.dll", syntaxer.Properties.Resources.System_ValueTuple);
            _action("System.Xml.ReaderWriter.dll", syntaxer.Properties.Resources.System_Xml_ReaderWriter);
            _action("System.Xml.XmlDocument.dll", syntaxer.Properties.Resources.System_Xml_XmlDocument);
            _action("System.Xml.XPath.dll", syntaxer.Properties.Resources.System_Xml_XPath);
            _action("System.Xml.XPath.XDocument.dll", syntaxer.Properties.Resources.System_Xml_XPath_XDocument);
            _action("vbc.exe", syntaxer.Properties.Resources.vbc);
            _action("vbc.rsp", syntaxer.Properties.Resources.vbc1);
            _action("VBCSCompiler.exe", syntaxer.Properties.Resources.VBCSCompiler);
            _action("VBCSCompiler.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.VBCSCompiler_exe));
            _action("vbc.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.vbc_exe));
            _action("Microsoft.VisualStudio.RemoteControl.dll", syntaxer.Properties.Resources.Microsoft_VisualStudio_RemoteControl);
        }

        static void TestFormat()
        {
            Console.Write("Formatting: ");

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

        static void TestProject()
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

        static void TestCompletion()
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

        static void TestCSSCompletion()
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

        static void TestCSSResolving()
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

        static void TestCSSResolving2()
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

        static void TestCSSTooltipResolving()
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

                Output.WriteLine("OK");
            });
        }

        static void TestResolving()
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

        static Assembly Probe(string dir, string asmName)
        {
            var file = Path.Combine(dir, asmName.Split(',')[0] + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
            else
                return null;
        }
    }

    class SocketServer
    {
        static Dictionary<int, object> connections = new Dictionary<int, object>();

        static void MonitorConnections(int connectionTimeout, Action requestShutdown)
        {
            do
            {
                Thread.Sleep(connectionTimeout);
                lock (connections)
                {
                    foreach (int id in connections.Keys.ToArray())
                        if (!Utils.IsProcessRunning(id))
                            connections.Remove(id);
                }
            }
            while (connections.Any());
            requestShutdown();
        }

        public static void Listen(Args processArgs)
        {
            try
            {
                var serverSocket = new TcpListener(IPAddress.Loopback, processArgs.port);
                serverSocket.Start();

                if (processArgs.client != 0)
                {
                    connections[processArgs.client] = true;
                    Output.WriteLine("Monitor client: " + processArgs.client);
                }

                Task.Run(() => MonitorConnections(processArgs.timeout, requestShutdown: serverSocket.Stop));

                Output.WriteLine($" >> Server Started (port={processArgs.port})");
                new Engine().Preload();
                Output.WriteLine($" >> Syntax engine loaded");

                while (true)
                {
                    Output.WriteLine(" >> Waiting for client request...");
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();
                    Output.WriteLine(" >> Accepted client...");

                    lock (connections)
                    {
                        try
                        {
                            Output.WriteLine(" >> Reading request...");
                            string request = clientSocket.ReadAllText();

                            var args = new Args(request.GetLines());

                            if (args.exit)
                            {
                                clientSocket.WriteAllText("Bye");
                                break;
                            }
                            else
                            {
                                if (args.client != 0)
                                    connections[args.client] = true;
                            }

                            Output.WriteLine(" >> Processing client request");

                            string response = SyntaxProvider.ProcessRequest(args);
                            if (response != null)
                                clientSocket.WriteAllText(response);
                        }
                        catch (Exception e)
                        {
                            Output.WriteLine(e.Message);
                        }
                    }
                }

                serverSocket.Stop();
                Output.WriteLine(" >> exit");
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10048)
                    Output.WriteLine(">" + e.Message);
                else
                    Output.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Output.WriteLine(e);
            }
        }
    }

    class SyntaxProvider
    {
        static public string ProcessRequest(Args args)
        {
            try
            {
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
                    return $"<error>File '{args.script}' doesn't exist";

                string result = "";

                if (args.op == "references")
                    result = FindRefreneces(args.script, args.pos);
                else if (args.op.StartsWith("suggest_usings:"))
                    result = FindUsings(args.script, args.op.Split(':').Last());
                else if (args.op == "resolve")
                    result = Resolve(args.script, args.pos);
                else if (args.op == "completion")
                    result = GetCompletion(args.script, args.pos);
                else if (args.op.StartsWith("tooltip:"))
                    result = GetMemberInfo(args.script, args.pos, args.op.Split(':').Last(), args.short_hinted_tooltips == 1);
                else if (args.op == "project")
                    result = GenerateProjectFor(args.script);
                else if (args.op == "codemap")
                    result = CodeMap(args.script);
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
        }

        internal static string Resolve(string script, int offset)
        {
            Output.WriteLine("Resolve");

            DomRegion region = ResolveRaw(script, offset);

            var result = new StringBuilder();
            result.AppendLine("file:" + region.FileName);
            result.AppendLine("line:" + region.BeginLine);

            return result.ToString().Trim();
        }

        static string GetAssociatedFileExtensions(string directive)
        {
            return directive.FirstMatch(("//css_inc", ".cs"),
                                        ("//css_include", ".cs"),
                                        ("//css_ref", ".dll|.exe"),
                                        ("//css_reference", ".dll|.exe"));
        }

        internal static bool ParseAsCssDirective(string script, string code, int offset, Action<string> onDirective, Action<string, string> onDirectiveArg)
        {
            string word = code.WordAt(offset, true);
            string line = code.LineAt(offset);
            if (word.StartsWith("//css_") || line.TrimStart().StartsWith("//css_"))
            {
                var directive = line.TrimStart().WordAt(2, true);
                string extensions = GetAssociatedFileExtensions(directive);

                if (word == "")
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
                        onDirective(directive);
                    else
                        onDirectiveArg(directive, word);
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

        internal static DomRegion ResolveRaw(string script, int offset)
        {
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            var region = DomRegion.Empty;

            bool wasProcessed = ParseAsCssDirective(script, code, offset,
                directive =>
                {
                    region = CssSyntax.Resolve(directive);
                },
                (directive, arg) =>
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
                });

            if (!wasProcessed)
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

        internal static DomRegion ResolveRawOld(string script, int offset)
        {
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            var region = DomRegion.Empty;

            string word = code.WordAt(offset, true);
            string line = code.LineAt(offset);
            if (word.StartsWith("//css_") || line.TrimStart().StartsWith("//css_"))
            {
                var directive = line.TrimStart().WordAt(2, true);
                string extensions = GetAssociatedFileExtensions(directive);

                if (word == "")
                {
                    region = CssSyntax.Resolve(directive);
                }
                else if (word.StartsWith("//css_"))
                {
                    region = CssSyntax.Resolve(word);
                }
                else
                {
                    if (extensions == null) // directive that does not include file
                        region = CssSyntax.Resolve(directive);
                    else
                        region = LookopDirectivePathOld(script, offset, directive, word, extensions);
                }
            }
            else
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

        internal static string FindUsings(string script, string word)
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

            return regions.Select(x => x.Namespace).JoinBy("\n");
        }

        internal static string FormatCode(string script, ref int caret)
        {
            Output.WriteLine("FormatCode-------------------------------------------");
            //formattedCode = formattedCode.NormalizeLineEnding();
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            string formattedCode = RoslynIntellisense.Formatter.FormatHybrid(code, "code.cs");

            caret = SyntaxMapper.MapAbsPosition(code, caret, formattedCode);

            return formattedCode;
        }

        internal static string CodeMap(string script)
        {
            csscript.Log("CodeMap");
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            var members = CSScriptHelper.GetMapOf(code, true).OrderBy(x => x.ParentDisplayName).ToArray();

            var result = new StringBuilder();

            var lines = members.Select(x =>
                                {
                                    return new { Type = (x.ParentDisplayType + " " + x.ParentDisplayName).Trim(), Content = "    " + x.DisplayName, Line = x.Line };
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

            string word = code.WordAt(caret, true);
            string line = code.LineAt(caret);
            if (word.StartsWith("//css_") || line.TrimStart().StartsWith("//css_"))
            {
                completions = CssCompletionData.AllDirectives;
                if (word == "")
                {
                    var directive = line.TrimStart().WordAt(2, true);
                    string extensions = directive.FirstMatch(("//css_inc", ".cs"),
                                                             ("//css_include", ".cs"),
                                                             ("//css_ref", ".dll|.exe"),
                                                             ("//css_reference", ".dll|.exe"));

                    if (extensions != null)
                    {
                        completions = CSScriptHelper.GenerateProjectFor(script)
                                                    .SearchDirs
                                                    .SelectMany(dir => extensions.Split('|').Select(x => new { ProbingDir = dir, FileExtension = x }))
                                                    .SelectMany(x =>
                                                    {
                                                        var files = Directory.GetFiles(x.ProbingDir, "*" + x.FileExtension);

                                                        return files.Select(file => new CssCompletionData
                                                        {
                                                            CompletionText = Path.GetFileName(file),
                                                            DisplayText = Path.GetFileName(file),
                                                            Description = "File: " + file,
                                                            CompletionType = CompletionType.file
                                                        });
                                                    });
                    }
                }
            }
            else
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

        internal static string GetMemberInfo(string script, int caret, string hint, bool shortHintedTooltips)
        {
            Output.WriteLine("GetMemberInfo");
            //Console.WriteLine("hint: " + hint);

            string result = null;
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            string word = code.WordAt(caret, true);
            string line = code.LineAt(caret);
            if (word.StartsWith("//css_") || line.TrimStart().StartsWith("//css_"))
            {
                var css_directive = CssCompletionData.AllDirectives.FirstOrDefault(x => x.DisplayText == word);
                if (css_directive != null)
                {
                    result = $"Directive: {css_directive.DisplayText}\n{css_directive.Description}";
                    return result.NormalizeLineEnding().Replace("\r\n\r\n", "\r\n").TrimEnd();
                }
            }

            if (!script.EndsWith(".g.cs"))
                CSScriptHelper.DecorateIfRequired(ref code, ref caret);

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                 .ToArray();

            int methodStartPosTemp;
            var items = Autocompleter.GetMemberInfo(code, caret, out methodStartPosTemp, project.Refs, sources, hint.HasAny());
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

    public static class Services
    {
        // "references" - request
        public static string FindRefreneces(string script, int offset) => SyntaxProvider.FindRefreneces(script, offset);

        // "suggest_usings" - request
        public static string FindUsings(string script, string word) => SyntaxProvider.FindUsings(script, word);

        // "resolve" - request
        public static string Resolve(string script, int offset)
            => SyntaxProvider.Resolve(script, offset);

        // public static DomRegion Resolve(string script, int offset) => SyntaxProvider.ResolveRaw(script, offset);

        // "completion" - request
        public static string GetCompletion(string script, int offset)
            => SyntaxProvider.GetCompletion(script, offset);

        // public static IEnumerable<ICompletionData> GetCompletion(string script, int offset) => SyntaxProvider.GetCompletionRaw(script, offset);

        // "tooltip" - request
        public static string GetTooltip(string script, int offset, string hint, bool shortHintedTooltips)
            => SyntaxProvider.GetMemberInfo(script, offset, hint, shortHintedTooltips);

        // "project" - request
        public static Project GenerateProjectFor(string script)
            => CSScriptHelper.GenerateProjectFor(script);

        // "codemap" - request
        public static string GetCodeMap(string script)
            => SyntaxProvider.CodeMap(script);

        // "format" - request
        public static string FormatCode(string script, ref int caretPos)
            => SyntaxProvider.FormatCode(script, ref caretPos);
    }
}