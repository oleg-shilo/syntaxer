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

//using csscript;

// Shockingly there is no one trully transparent IPC solution for Win, Linux, Mac
// - Named-pipes are not implemented on Linux
// - Sockets seems to be a good portable approach but they claim port (not a bigi though)
// but on Windows will require granting special permissions. "Frightening" confirmation dialog
// is not a good UX.
// - Unix domain socket plays the same role as named-pipes but with Socket interface: not portable
// on Win and create file anyway. BTW the file that may be left on the system:
// http://mono.1490590.n4.nabble.com/Unix-domain-sockets-on-mono-td1490601.html
// - Unix-pipes then closest Win named-pipes equivalent are still OS specific and create a file as well.
// ---------------------
// Bottom line: the only a compromise solution, which is simple and portable is to use a plain socket.
//  - ultimately portable
//  - fast enough (particularly with request rate we need to meet - less than 1 request per 3 sec)
//  - requires no special permissions on Linux (even if it does on Win)
//  - full control of cleanup (as there is none)

namespace Syntaxer
{
    class Server
    {
        static void Main(string[] args)
        {
            //Preload();
            Deploy();
            Run(args);
            if (asms.Any())
                asms.Clear();
        }

        static void Run(string[] args)
        {
            var input = new Args(args);

            if (input.test)
            {
                TestFormat();
                TestCompletion();
            }
            else
            {
                if (input.cscs_path != null)
                    csscript.cscs_path = input.cscs_path;

                if (input.listen)
                {
                    SocketServer.Listen(input);
                }
                else
                    Console.WriteLine(SyntaxProvider.ProcessRequest(input));
            }
        }

        static List<object> asms = new List<object>();

        static void Deploy()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            Action<string, byte[]> copy = (name, bytes) =>
            {
                try { File.WriteAllBytes(Path.Combine(dir, name), bytes); } catch { }
            };

            copy("CSSCodeProvider.v4.6.dll", syntaxer.Properties.Resources.CSSCodeProvider_v4_6);
            copy("Intellisense.Common.dll", syntaxer.Properties.Resources.Intellisense_Common);
            copy("RoslynIntellisense.dll", syntaxer.Properties.Resources.RoslynIntellisense);
            copy("csc.exe", syntaxer.Properties.Resources.csc_exe);
            copy("csc.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.csc_exe_config));
            copy("csc.rsp", syntaxer.Properties.Resources.csc_rsp);
            copy("csi.exe", syntaxer.Properties.Resources.csi_exe);
            copy("csi.rsp", syntaxer.Properties.Resources.csi_rsp);
            copy("Esent.Interop.dll", syntaxer.Properties.Resources.Esent_Interop);
            copy("Microsoft.Build.dll", syntaxer.Properties.Resources.Microsoft_Build);
            copy("Microsoft.Build.Framework.dll", syntaxer.Properties.Resources.Microsoft_Build_Framework);
            copy("Microsoft.Build.Tasks.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_CodeAnalysis);
            copy("Microsoft.Build.Tasks.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core);
            copy("Microsoft.Build.Utilities.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Utilities_Core);
            copy("Microsoft.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis);
            copy("Microsoft.CodeAnalysis.CSharp.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp);
            copy("Microsoft.CodeAnalysis.CSharp.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Scripting);
            copy("Microsoft.CodeAnalysis.CSharp.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Workspaces);
            copy("Microsoft.CodeAnalysis.Elfie.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Elfie);
            copy("Microsoft.CodeAnalysis.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Scripting);
            copy("Microsoft.CodeAnalysis.VisualBasic.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic);
            copy("Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic_Workspaces);
            copy("Microsoft.CodeAnalysis.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces);
            copy("Microsoft.CodeAnalysis.Workspaces.Desktop.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces_Desktop);
            copy("Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll", syntaxer.Properties.Resources.Microsoft_CodeDom_Providers_DotNetCompilerPlatform);
            copy("Microsoft.CSharp.Core.targets", syntaxer.Properties.Resources.Microsoft_CSharp_Core);
            copy("Microsoft.DiaSymReader.Native.amd64.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_amd64);
            copy("Microsoft.DiaSymReader.Native.x86.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_x86);
            copy("Microsoft.VisualBasic.Core.targets", syntaxer.Properties.Resources.Microsoft_VisualBasic_Core);
            copy("Microsoft.Win32.Primitives.dll", syntaxer.Properties.Resources.Microsoft_Win32_Primitives);
            copy("System.AppContext.dll", syntaxer.Properties.Resources.System_AppContext);
            copy("System.Collections.Immutable.dll", syntaxer.Properties.Resources.System_Collections_Immutable);
            copy("System.Composition.AttributedModel.dll", syntaxer.Properties.Resources.System_Composition_AttributedModel);
            copy("System.Composition.Convention.dll", syntaxer.Properties.Resources.System_Composition_Convention);
            copy("System.Composition.Hosting.dll", syntaxer.Properties.Resources.System_Composition_Hosting);
            copy("System.Composition.Runtime.dll", syntaxer.Properties.Resources.System_Composition_Runtime);
            copy("System.Composition.TypedParts.dll", syntaxer.Properties.Resources.System_Composition_TypedParts);
            copy("System.Console.dll", syntaxer.Properties.Resources.System_Console);
            copy("System.Diagnostics.FileVersionInfo.dll", syntaxer.Properties.Resources.System_Diagnostics_FileVersionInfo);
            copy("System.Diagnostics.Process.dll", syntaxer.Properties.Resources.System_Diagnostics_Process);
            copy("System.Diagnostics.StackTrace.dll", syntaxer.Properties.Resources.System_Diagnostics_StackTrace);
            copy("System.IO.Compression.dll", syntaxer.Properties.Resources.System_IO_Compression);
            copy("System.IO.FileSystem.dll", syntaxer.Properties.Resources.System_IO_FileSystem);
            copy("System.IO.FileSystem.DriveInfo.dll", syntaxer.Properties.Resources.System_IO_FileSystem_DriveInfo);
            copy("System.IO.FileSystem.Primitives.dll", syntaxer.Properties.Resources.System_IO_FileSystem_Primitives);
            copy("System.IO.Pipes.dll", syntaxer.Properties.Resources.System_IO_Pipes);
            copy("System.Reflection.Metadata.dll", syntaxer.Properties.Resources.System_Reflection_Metadata);
            copy("System.Security.AccessControl.dll", syntaxer.Properties.Resources.System_Security_AccessControl);
            copy("System.Security.Claims.dll", syntaxer.Properties.Resources.System_Security_Claims);
            copy("System.Security.Cryptography.Algorithms.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Algorithms);
            copy("System.Security.Cryptography.Encoding.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Encoding);
            copy("System.Security.Cryptography.Primitives.dll", syntaxer.Properties.Resources.System_Security_Cryptography_Primitives);
            copy("System.Security.Cryptography.X509Certificates.dll", syntaxer.Properties.Resources.System_Security_Cryptography_X509Certificates);
            copy("System.Security.Principal.Windows.dll", syntaxer.Properties.Resources.System_Security_Principal_Windows);
            copy("System.Text.Encoding.CodePages.dll", syntaxer.Properties.Resources.System_Text_Encoding_CodePages);
            copy("System.Threading.Tasks.Dataflow.dll", syntaxer.Properties.Resources.System_Threading_Tasks_Dataflow);
            copy("System.Threading.Thread.dll", syntaxer.Properties.Resources.System_Threading_Thread);
            copy("System.ValueTuple.dll", syntaxer.Properties.Resources.System_ValueTuple);
            copy("System.Xml.ReaderWriter.dll", syntaxer.Properties.Resources.System_Xml_ReaderWriter);
            copy("System.Xml.XmlDocument.dll", syntaxer.Properties.Resources.System_Xml_XmlDocument);
            copy("System.Xml.XPath.dll", syntaxer.Properties.Resources.System_Xml_XPath);
            copy("System.Xml.XPath.XDocument.dll", syntaxer.Properties.Resources.System_Xml_XPath_XDocument);
            copy("vbc.exe", syntaxer.Properties.Resources.vbc);
            copy("vbc.rsp", syntaxer.Properties.Resources.vbc1);
            copy("VBCSCompiler.exe", syntaxer.Properties.Resources.VBCSCompiler);
            copy("VBCSCompiler.exe.config;", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.VBCSCompiler_exe));
            copy("vbc.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.vbc_exe));
            copy("Microsoft.VisualStudio.RemoteControl.dll", syntaxer.Properties.Resources.Microsoft_VisualStudio_RemoteControl);
        }

        static void Preload()
        {
            //if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
            //    return;

            // On Linux AssemblyResolve may not be fired for all assemblies if running under ST3 runtime.
            // So pre-load them at the startup.

            //var asm = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Microsoft.Build.Tasks.Core.dll");
            //if (!File.Exists(asm))
            //    File.WriteAllBytes(asm, syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core);

            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build_Engine));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build_Framework));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_Build_Utilities_Core));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Workspaces));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic_Workspaces));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces_Desktop));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Composition_AttributedModel));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Composition_Hosting));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Composition_Runtime));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Composition_TypedParts));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Reflection_Metadata));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.RoslynIntellisense));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.Intellisense_Common));
            //asms.Add(Assembly.Load(syntaxer.Properties.Resources.System_Collections_Immutable));
        }

        static void TestFormat()
        {
            Console.Write("Formatting: ");
            try
            {
                RoslynIntellisense.Formatter.FormatHybrid(SyntaxProvider.testCode, "code.cs");
                Console.WriteLine("OK");
            }
            catch (Exception e)
            {
                Console.WriteLine("failed");
                Console.WriteLine(e);
            }
        }

        static void TestCompletion()
        {
            Console.Write("Autocompletion: ");
            var script = Path.GetTempFileName();
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

                string code = SyntaxProvider.testCode;

                File.WriteAllText(script, code);

                IEnumerable<ICompletionData> completions = null;

                var caret = 315;
                string word = code.WordAt(caret);

                Project project = CSScriptHelper.GenerateProjectFor(script);
                var sources = project.Files
                                     .Where(f => f != script)
                                     .Select(f => new Tuple<string, string>(File.ReadAllText(f), f))
                                     .ToArray();

                completions = Autocompleter.GetAutocompletionFor(code, caret, project.Refs, sources);
                var count = completions.Count();

                Console.WriteLine("OK");
            }
            catch (Exception e)
            {
                Console.WriteLine("failed");
                Console.WriteLine(e);
            }
            finally
            {
                try { File.Delete(script); } catch { }
            }
        }

        //static string probingDir = @"E:\Galos\Projects\CS-Script.Sublime\src\syntaxer\bin\Debug\Roslyn";

        static Assembly Probe(string dir, string asmName)
        {
            var file = Path.Combine(dir, asmName.Split(',')[0] + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
            else
                return null;
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
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
                    Console.WriteLine("Monitor client: " + processArgs.client);
                }

                Task.Run(() => MonitorConnections(processArgs.timeout, requestShutdown: serverSocket.Stop));

                Console.WriteLine($" >> Server Started (port={processArgs.port})");
                //Console.WriteLine($" >> Syntax engine is about to be loaded");
                new Engine().Preload();
                Console.WriteLine($" >> Syntax engine loaded");

                while (true)
                {
                    Console.WriteLine(" >> Waiting for client request...");
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();
                    Console.WriteLine(" >> Accepted client...");

                    lock (connections)
                    {
                        try
                        {
                            Console.WriteLine(" >> Reading request...");
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

                            Console.WriteLine(" >> Processing client request");

                            string response = SyntaxProvider.ProcessRequest(args);
                            if (response != null)
                                clientSocket.WriteAllText(response);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }

                serverSocket.Stop();
                Console.WriteLine(" >> exit");
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10048)
                    Console.WriteLine(">" + e.Message);
                else
                    Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                        Console.WriteLine(" >> cscs.exe is remapped to: " + csscript.cscs_path);
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
                    Console.WriteLine("FormatCode>");
                    int caretPos = args.pos;
                    var formattedCode = FormatCode(args.script, ref caretPos);
                    Console.WriteLine("<FormatCode");
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

        static string Resolve(string script, int offset)
        {
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            var result = new StringBuilder();

            string token = code.WordAt(offset);

            DomRegion region;
            if (token.StartsWith("//css_"))
            {
                region = CssSyntax.Resolve(token);
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
            result.AppendLine("file:" + region.FileName);
            result.AppendLine("line:" + region.BeginLine);

            return result.ToString().Trim();
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

        static string FindRefreneces(string script, int offset)
        {
            Console.WriteLine("FindRefreneces");

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

        static string FindUsings(string script, string word)
        {
            Console.WriteLine("FindUsings");
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            Project project = CSScriptHelper.GenerateProjectFor(script);
            var sources = project.Files
                                 .Where(f => f != script)
                                 .Select(f => new Tuple<string, string>(File.ReadAllText(f), f));

            var regions = Autocompleter.GetNamespacesFor(code, word, project.Refs, sources).Result;

            return regions.Select(x => x.Namespace).JoinBy("\n");
        }

        static string FormatCode(string script, ref int caret)
        {
            Console.WriteLine("FormatCode-------------------------------------------");
            //formattedCode = formattedCode.NormalizeLineEnding();
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            string formattedCode = RoslynIntellisense.Formatter.FormatHybrid(code, "code.cs");

            caret = SyntaxMapper.MapAbsPosition(code, caret, formattedCode);

            return formattedCode;
        }

        static string CodeMap(string script)
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

        static string GetCompletion(string script, int caret)
        {
            Console.WriteLine("GetCompletion");
            string code = File.ReadAllText(script);

            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            IEnumerable<ICompletionData> completions = null;
            var result = new StringBuilder();

            string word = code.WordAt(caret);
            if (word.StartsWith("//css"))
            {
                completions = CssCompletionData.AllDirectives;
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

            foreach (ICompletionData item in completions)
            {
                string type = item.CompletionType.ToString().Replace("_event", "event").Replace("_namespace", "namespace");
                string completion = item.CompletionText;
                string display = item.DisplayText;
                if (item.CompletionType == CompletionType.method)
                {
                    if (item.HasOverloads)
                    {
                        display += "(...)";
                        completion += "(";
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
                            completion += "(";
                        }
                    }
                }

                result.AppendLine($"{display}\t{type}|{completion}");
            }

            //Console.WriteLine(">>>>>" + result.ToString().Trim());
            return result.ToString().Trim();
        }

        static string GetMemberInfo(string script, int caret, string hint, bool shortHintedTooltips)
        {
            Console.WriteLine("GetMemberInfo");
            //Console.WriteLine("hint: " + hint);

            string result = null;
            string code = File.ReadAllText(script);
            if (code.IsEmpty())
                throw new Exception("The file containing code is empty");

            string word = code.WordAt(caret);
            string line = code.LineAt(caret);
            if (word.StartsWith("//css_") || line.StartsWith("//css_"))
            {
                if (!word.StartsWith("//css_"))
                    word = line.WordAt(1);

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

        static public string GenerateProjectFor(string script)
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
    }
}