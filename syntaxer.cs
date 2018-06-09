using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RoslynIntellisense;
using Microsoft.CodeAnalysis;
using Intellisense.Common;

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
    // Ports:
    // 18000 - Sublime Text 3
    // 18001 - Notepad++
    // 18002 - VSCode.CodeMap
    // 18003 - VSCode.CS-Script
    class Server
    {
        // -port:18003 -listen -timeout:60000 cscs_path:C:\Users\<user>\AppData\Roaming\Code\User\cs-script.user\syntaxer\1.2.2.0\cscs.exe

        static void Main(string[] args)
        {
            // Debug.Assert(false);
            DeployCSScriptIntegration();

            var input = new Args(args);

            if (input.dr)
                DeployRoslyn();

            // -listen -timeout:60000 -cscs_path:./cscs.exe
            if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
            {
                if (!input.dr) // not already deployed
                    DeployRoslyn();
            }
            else
            {
                LoadRoslyn();
            }

            mono_root = Path.GetDirectoryName(typeof(string).Assembly.Location);
            Output.WriteLine(mono_root);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Run(input);
        }

        static string mono_root;

        static string local_dir;

        static string Local_dir
        {
            get
            {
                // must be assigned here as if it is assigned in the field declaration it triggers premature assembly loading.
                return local_dir = local_dir ?? Assembly.GetExecutingAssembly().Location.GetDirName();
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Probe(mono_root, args.Name) ??
                   Probe(mono_root.PathJoin("Fasades"), args.Name) ??
                   Probe(Local_dir, args.Name) ??
                   ProbeAlreadyLoaded(args.ShortName());
        }

        static void Run(Args input)
        {
            if (input.cscs_path != null)
            {
                csscript.cscs_path = Path.GetFullPath(input.cscs_path);
            }

            if (csscript.cscs_path == null || !Directory.Exists(csscript.cscs_path))
            {
                Console.WriteLine("Probing cscs.exe ...");
                if (File.Exists(csscript.default_cscs_path))
                {
                    csscript.cscs_path = csscript.default_cscs_path;
                }
                else if (File.Exists(csscript.default_cscs_path2))
                {
                    csscript.cscs_path = csscript.default_cscs_path2;
                }
                else
                    Console.WriteLine("Probing cscs.exe failed...");
            }
            else
                Console.WriteLine("cscs.exe: " + csscript.cscs_path);

            if (input.test)
            {
                if (csscript.cscs_path == null)
                    csscript.cscs_path = csscript.default_cscs_path;

                Test.All();
            }
            else
            {
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
            {
                if (!File.Exists(Path.Combine(dir, name)))
                    File.WriteAllBytes(Path.Combine(dir, name), bytes);
            });
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
            {
                Assembly.Load(bytes);
            });
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
            _action("csi.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.csi_exe_config));
            _action("Esent.Interop.dll", syntaxer.Properties.Resources.Esent_Interop);
            _action("Microsoft.Build.dll", syntaxer.Properties.Resources.Microsoft_Build);
            _action("Microsoft.Build.Framework.dll", syntaxer.Properties.Resources.Microsoft_Build_Framework);
            _action("Microsoft.Build.Tasks.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_CodeAnalysis);
            _action("Microsoft.Build.Tasks.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Tasks_Core);
            _action("Microsoft.Build.Utilities.Core.dll", syntaxer.Properties.Resources.Microsoft_Build_Utilities_Core);
            _action("Microsoft.CodeAnalysis.CSharp.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp);
            _action("Microsoft.CodeAnalysis.CSharp.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Scripting);
            _action("Microsoft.CodeAnalysis.CSharp.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_CSharp_Workspaces);
            _action("Microsoft.CodeAnalysis.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis);
            _action("Microsoft.CodeAnalysis.Elfie.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Elfie);
            _action("Microsoft.CodeAnalysis.Scripting.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Scripting);
            _action("Microsoft.CodeAnalysis.VisualBasic.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic);
            _action("Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_VisualBasic_Workspaces);
            _action("Microsoft.CodeAnalysis.Workspaces.Desktop.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces_Desktop);
            _action("Microsoft.CodeAnalysis.Workspaces.dll", syntaxer.Properties.Resources.Microsoft_CodeAnalysis_Workspaces);
            // Microsoft.CodeDom.Providers.DotNetCompilerPlatform included in CS-Script RoslynProvider
            _action("Microsoft.CSharp.Core.targets", syntaxer.Properties.Resources.Microsoft_CSharp_Core);
            _action("Microsoft.DiaSymReader.Native.amd64.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_amd64);
            _action("Microsoft.DiaSymReader.Native.x86.dll", syntaxer.Properties.Resources.Microsoft_DiaSymReader_Native_x86);
            _action("Microsoft.VisualBasic.Core.targets", syntaxer.Properties.Resources.Microsoft_VisualBasic_Core);
            _action("Microsoft.VisualStudio.RemoteControl.dll", syntaxer.Properties.Resources.Microsoft_VisualStudio_RemoteControl);
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
            _action("System.Runtime.InteropServices.RuntimeInformation.dll", syntaxer.Properties.Resources.System_Runtime_InteropServices_RuntimeInformation);
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
            _action("vbc.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.vbc_exe));
            _action("vbc.rsp", syntaxer.Properties.Resources.vbc1);
            _action("VBCSCompiler.exe", syntaxer.Properties.Resources.VBCSCompiler);
            _action("VBCSCompiler.exe.config", Encoding.UTF8.GetBytes(syntaxer.Properties.Resources.VBCSCompiler_exe));
        }

        static Assembly Probe(string dir, string asmName)
        {
            var file = Path.Combine(dir, asmName.Split(',')[0] + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
            else
                return null;
        }

        static Assembly ProbeAlreadyLoaded(string asmName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == asmName).FirstOrDefault();
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

                Output.WriteLine($" >> Server (v{Assembly.GetExecutingAssembly().GetName().Version}) Started (port={processArgs.port})");
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
                                {
                                    connections[args.client] = true;
                                    // Output.WriteLine("Monitor client: " + args.client);
                                }
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

    public static class TestServices
    {
        // "references" - request
        public static string FindRefreneces(string script, int offset, string context = null) => SyntaxProvider.FindRefreneces(script, offset, context);

        // "suggest_usings" - request
        public static string FindUsings(string script, string word) => SyntaxProvider.FindUsings(script, word, false);

        // "resolve" - request
        public static string Resolve(string script, int offset)
            => SyntaxProvider.Resolve(script, offset, false);

        // public static DomRegion Resolve(string script, int offset) => SyntaxProvider.ResolveRaw(script, offset);

        // "completion" - request
        public static string GetCompletion(string script, int offset)
            => SyntaxProvider.GetCompletion(script, offset);

        // public static IEnumerable<ICompletionData> GetCompletion(string script, int offset) => SyntaxProvider.GetCompletionRaw(script, offset);

        // "tooltip" - request
        public static string GetTooltip(string script, int offset, string hint, bool shortHintedTooltips)
            => SyntaxProvider.GetTooltip(script, offset, hint, shortHintedTooltips);

        // "signaturehelp" - request
        public static string GetSignatureHelp(string script, int offset)
            => SyntaxProvider.GetSignatureHelp(script, offset);

        // "project" - request
        public static Project GenerateProjectFor(string script)
            => CSScriptHelper.GenerateProjectFor(new SourceInfo(script));

        // "codemap" - request
        public static string GetCodeMap(string script)
            => SyntaxProvider.CodeMap(script, false, false);

        // "format" - request
        public static string FormatCode(string script, ref int caretPos)
            => SyntaxProvider.FormatCode(script, ref caretPos);
    }
}