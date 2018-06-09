using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Intellisense.Common;
using RoslynIntellisense;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using Syntaxer;
using System.Xml.Linq;

namespace Syntaxer
{
    public class Project
    {
        public string[] Files;
        public string[] Refs;
        public string Script;
        public string[] SearchDirs;

        public static Project GenerateProjectFor(string script)
        {
            return csscript.ProjectBuilder.GenerateProjectFor(script);
        }
    }

    //need to use reflection so cscs.exe can be remapped dynamically
    internal static class csscript
    {
        internal static string default_cscs_path = Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("cscs.exe");
        internal static string default_cscs_path2 = Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("..", "cscs.exe");

        internal static void Log(string message)
        {
            //string file = Path.Combine(Assembly.GetExecutingAssembly().Location + ".log");

            //if (File.Exists(file))
            //    message = File.ReadAllText(file) + "\n" + message;

            //File.WriteAllText(file, message);
            Output.WriteLine(message);
        }

        internal static Assembly _cscs_asm;

        internal static Assembly Cscs_asm
        {
            get
            {
                lock (typeof(csscript))
                {
                    // csscript.Log("Cscs_asm=" + (_cscs_asm == null ? "<null>" : "<asm>"));
                    // csscript.Log("cscs_path=" + cscs_path);
                    if (_cscs_asm == null)
                    {
                        try
                        {
                            if (cscs_path.IsEmpty())
                                csscript.Log($"Error: cscs_path is empty");
                            _cscs_asm = Assembly.Load(File.ReadAllBytes(cscs_path));
                        }
                        catch (Exception e)
                        {
                            Log(e.ToString());
                            throw new Exception($"Cannot load cscs.exe assembly from {cscs_path}");
                        }
                    }
                    return _cscs_asm;
                }
            }
        }

        static string _cscs_path;

        static public string cscs_path
        {
            get { return _cscs_path; }

            set
            {
                if (value != null && value != _cscs_path)
                {
                    if (value == "./cscs.exe" || !File.Exists(value))
                        _cscs_path = csscript.default_cscs_path;
                    else
                        _cscs_path = value;

                    if (_cscs_path != null && File.Exists(_cscs_path))
                        _cscs_path = Path.GetFullPath(_cscs_path);

                    Console.WriteLine("cscs_path set to: " + _cscs_path);
                    // Console.WriteLine("Setting cscs.exe ...");
                    //CSScriptProxy.TriggerCompilerLoading();
                }
            }
        }

        public class ProjectBuilder
        {
            static public string GetCSSConfig()
            {
                //csscript.ProjectBuilder.GetCSSConfig();
                try
                {
                    return Path.Combine(Path.GetDirectoryName(csscript._cscs_path), "css_config.xml");
                    //var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "ProjectBuilder").FirstOrDefault();
                    //MethodInfo method = type.GetMethod("GetCSSConfig", BindingFlags.Public | BindingFlags.Static);
                    //return (string) method.Invoke(null, new object[0]);
                }
                catch { }
                return null;
            }

            static object cscs_GenerateProjectFor(string script)
            {
                var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "ProjectBuilder").FirstOrDefault();
                csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "ProjectBuilder").FirstOrDefault();
                MethodInfo method = type.GetMethod("GenerateProjectFor", BindingFlags.Public | BindingFlags.Static);
                return method.Invoke(null, new object[] { script });
            }

            internal static string cscs_GetScriptTempDir()
            {
                return (string)csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "CSExecutor").First()
                                                   .GetMethod("GetScriptTempDir", BindingFlags.Public | BindingFlags.Static)
                                                   .Invoke(null, new object[0]);
            }

            static public Project GenerateProjectFor(string script)
            {
                //csscript.ProjectBuilder.GenerateProjectFor(script);
                try
                {
                    if (csscript.Cscs_asm == null)
                        throw new Exception($"cscs.exe assembly is not loaded ({csscript.cscs_path}).");

                    var dbg_interface_file = Path.Combine(cscs_GetScriptTempDir(), "Cache", "dbg.cs");

                    object proj = cscs_GenerateProjectFor(script);
                    Type projType = proj.GetType();

                    string[] includes = (string[])projType.GetField("Files").GetValue(proj);

                    return new Project
                    {
                        Files = includes.Concat(new[] { dbg_interface_file }).ToArray(),
                        Refs = (string[])projType.GetField("Refs").GetValue(proj),
                        SearchDirs = (string[])projType.GetField("SearchDirs").GetValue(proj),
                        Script = (string)projType.GetField("Script").GetValue(proj)
                    };
                }
                catch { }
                return null;
            }
        }

        class CSScriptProxy
        {
            static public void TriggerCompilerLoading()
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        //Preload compiler
                        {
                            var script = Path.GetTempFileName();
                            try
                            {
                                //File.WriteAllText(script, "using System;");
                                //var proc = new Process();
                                //proc.StartInfo.FileName = cscs_path;
                                //proc.StartInfo.Arguments = $"-c:0 -ac:0 \"{script}\"";
                                //proc.StartInfo.UseShellExecute = false;
                                //proc.StartInfo.CreateNoWindow = true;
                                //proc.Start();
                                //proc.WaitForExit();
                            }
                            catch { }
                            finally
                            {
                                try
                                {
                                    if (File.Exists(script))
                                        File.Delete(script);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                });
            }
        }

        public class AutoclassGenerator
        {
            static public string Process(string text, ref int position)
            {
                //csscript.AutoclassGenerator.Process(text, ref position);
                try
                {
                    var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "AutoclassGenerator").FirstOrDefault();
                    MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "Process" && m.GetParameters().Length == 2);
                    object[] args = new object[] { text, position };
                    var result = (string)method.Invoke(null, args);
                    position = (int)args[1];
                    return result;
                }
                catch { }
                return null;
            }
        }
    }

    static class CSScriptHelper
    {
        static public Project GenerateProjectFor(SourceInfo script)
        {
            if (script.File == script.RawFile)
            {
                return Project.GenerateProjectFor(script.File);
            }
            else
            {
                // The input file is not the actual script file.
                // For example the script file in the editor is modified but unsaved and teh editor
                // uses temp file for intellisense. 
                var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + script.File.GetFileExtension());
                try
                {
                    var dir_instruction = "//css_dir " + script.File.GetDirName();

                    File.WriteAllText(tempFile, dir_instruction + Environment.NewLine + script.Content);
                    var project = Project.GenerateProjectFor(tempFile);

                    project.Script = script.File;

                    var files = project.Files.ToList();
                    files.Remove(tempFile);
                    files.Insert(0, script.File);
                    project.Files = files.ToArray();

                    var dirs = project.SearchDirs.ToList();
                    dirs.Remove(tempFile.GetDirName());
                    project.SearchDirs = dirs.ToArray();

                    return project;
                }
                finally
                {
                    if(File.Exists(tempFile))
                        try { File.Delete(tempFile); } catch { }
                }
            }
        }

        static Project GenerateProjectFor(string script)
        {
            return Project.GenerateProjectFor(script);
        }

        static public string GetCSSConfig()
        {
            return csscript.ProjectBuilder.GetCSSConfig();
        }

        static string help_file;

        static public string GetCSSHelp()
        {
            var file = csscript.ProjectBuilder.cscs_GetScriptTempDir().PathJoin("help.txt");
            if (help_file == null)
            {
                try
                {
                    var output = Utils.Run(csscript.cscs_path, "-help");
                    File.WriteAllText(file, output);
                }
                catch
                {
                    help_file = file;
                }
            }
            return help_file ?? file;
        }

        public static CodeMapItem[] GetMapOf(string code, bool stripInjectedClass = false)
        {
            bool injected = DecorateIfRequired(ref code);
            CodeMapItem[] map = Autocompleter.GetMapOf(code, injected);

            if (stripInjectedClass && injected)
            {
                //only remove if "main()" was decorated with global
                if (map.Any(x => x.ParentDisplayName.StartsWith("<Global")))
                    map = map.Where(x => x.ParentDisplayName != "ScriptClass").ToArray();
            }

            if (injected)
            {
                var injectedLine = int.MaxValue;

                int pos = code.IndexOf("///CS-Script auto-class generation");
                if (pos != -1)
                    injectedLine = code.Substring(0, pos).Split('\n').Count() - 1;

                map = map.Where(i => i.Line != injectedLine).ToArray();

                foreach (CodeMapItem item in map)
                {
                    if (item.Line >= injectedLine)
                        item.Line -= 1;
                }
            }
            return map;
        }

        static public bool DecorateIfRequired(ref string text)
        {
            int dummy = 0;
            return DecorateIfRequired(ref text, ref dummy);
        }

        static public bool DecorateIfRequired(ref string text, ref int currentPos)
        {
            if (NeedsAutoclassWrapper(text))
            {
                int originalPos = currentPos;
                string originaltext = text;

                text = GenerateAutoclassWrapper(text, ref currentPos);

                //if (originaltext.GetHashCode() == text.GetHashCode())
                if (!text.Contains("///CS-Script auto-class generation"))
                {
                    currentPos = originalPos;
                    return false;
                }
                else
                    return true;
            }
            else
                return false;
        }

        static public string GenerateAutoclassWrapper(string text, ref int position)
        {
            return csscript.AutoclassGenerator.Process(text, ref position);
        }

        static public bool NeedsAutoclassWrapper(string text)
        {
            // csscript.Log("NeedsAutoclassWrapper");

            bool isAutoClassSupported = false;
            try
            {
                var file = GetCSSConfig();
                if (File.Exists(file))
                {
                    var xml = XDocument.Load(file);

                    string[] defaultArgs = xml.Root.Descendants("defaultArguments")
                                                   .First()
                                                   .Value
                                                   .Split(' ');

                    if (defaultArgs.Contains("-ac") || defaultArgs.Contains("-autoclass"))
                        isAutoClassSupported = true;
                }
            }
            catch { }

            foreach (Match item in Regex.Matches(text, @"\s?//css_args\s+(/|-)(ac|ac:0|ac:1)(,|;\s+)"))
                isAutoClassSupported = !item.Value.Contains("ac:0");

            foreach (Match item in Regex.Matches(text, @"\s?//css_args\s+(/|-)(autoclass|autoclass:0|autoclass:1)(,|;|\s+)"))
                isAutoClassSupported = !item.Value.Contains("ac:0");

            return isAutoClassSupported;
        }

        static public void Undecorate(string text, ref DomRegion region)
        {
            int pos = text.IndexOf("///CS-Script auto-class generation");
            if (pos != -1)
            {
                var injectedLine = text.Substring(0, pos).Split('\n').Count() - 1;
                if (injectedLine < region.BeginLine)
                {
                    region.BeginLine--;
                    region.EndLine--;
                }
            }
        }

        static public void NormaliseFileReference(ref string file, ref int line)
        {
            try
            {
                if (file.EndsWith(".g.csx") || file.EndsWith(".g.cs") && file.Contains(Path.Combine("CSSCRIPT", "Cache")))
                {
                    //it is an auto-generated file so try to find the original source file (logical file)
                    string dir = Path.GetDirectoryName(file);
                    string infoFile = Path.Combine(dir, "css_info.txt");
                    if (File.Exists(infoFile))
                    {
                        string[] lines = File.ReadAllLines(infoFile);
                        if (lines.Length > 1 && Directory.Exists(lines[1]))
                        {
                            string logicalFile = Path.Combine(lines[1], Path.GetFileName(file).Replace(".g.csx", ".csx").Replace(".g.cs", ".cs"));
                            if (File.Exists(logicalFile))
                            {
                                string code = File.ReadAllText(file);
                                int pos = code.IndexOf("///CS-Script auto-class generation");
                                if (pos != -1)
                                {
                                    int injectedLineNumber = code.Substring(0, pos).Split('\n').Count() - 1;
                                    if (injectedLineNumber <= line)
                                        line -= 1; //a single line is always injected
                                }
                                file = logicalFile;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}