using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Syntaxer
{
    public static class Utils
    {
        public static string NormalizeLineEnding(this string text)
        {
            //sublime operates with '\n' when counts caret position even on windows
            return text.Replace("\r\n", "\n");
        }

        public static void PreserveCurrentDir(Action action)
        {
            string currDir = Environment.CurrentDirectory;
            try
            {
                action();
            }
            finally
            {
                Environment.CurrentDirectory = currDir;
            }
        }

        public static bool IsProcessRunning(int id)
        {
            return Process.GetProcesses().Any(x => x.Id == id);
        }

        public static int FindIndex<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");

            int retVal = 0;
            foreach (var item in items)
            {
                if (predicate(item)) return retVal;
                retVal++;
            }
            return -1;
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        static char[] delimiters = "\\\t\n\r .,:;'\"=[]{}()+-/!?@$%^&*��><#|~`".ToCharArray();

        public static string WordAt(this string text, int index)
        {
            int start = text.Substring(0, index).LastIndexOfAny(delimiters) + 1;
            int end = text.Substring(index).IndexOfAny(delimiters);

            if (end != -1)
                end = (index + end);
            else
                end = text.Length;

            return text.Substring(start, end - start);
        }

        public static string LineAt(this string text, int index)
        {
            var delimiters = "\r\n".ToCharArray();

            int start = text.Substring(0, index).LastIndexOfAny(delimiters) + 1;
            int end = text.Substring(index).IndexOfAny(delimiters);

            if (end != -1)
                end = text.Length;
            else
                end = (index + end);

            return text.Substring(start, end - start).Trim();
        }

        public static string DeleteDirFiles(this string dir, string pattern)
        {
            foreach (string oldFile in Directory.GetFiles(dir, pattern))
                try { File.Delete(oldFile); } catch { }
            return dir;
        }

        public static string EnsureDir(this string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string Run(string exe, string args)
        {
            var output = new StringBuilder();

            var proc = new Process();
            proc.StartInfo.FileName = exe;
            proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(exe);
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();

            string line = null;
            while (null != (line = proc.StandardOutput.ReadLine()))
            {
                output.AppendLine(line);
            }
            proc.WaitForExit();
            return output.ToString();
        }
    }

    public static class SocketExtensions
    {
        public static byte[] GetBytes(this string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        public static string GetString(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public static byte[] ReadAllBytes(this TcpClient client)
        {
            var bytes = new byte[client.ReceiveBufferSize];
            var len = client.GetStream()
                            .Read(bytes, 0, bytes.Length);
            var result = new byte[len];
            Array.Copy(bytes, result, len);
            return result;
        }

        public static string ReadAllText(this TcpClient client)
        {
            return client.ReadAllBytes().GetString();
        }

        public static void WriteAllBytes(this TcpClient client, byte[] data)
        {
            var stream = client.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        public static void WriteAllText(this TcpClient client, string data)
        {
            client.WriteAllBytes(data.GetBytes());
        }
    }

    public static class ProcessExtensions
    {
        private static string FindIndexedProcessName(int pid)
        {
            var processName = Process.GetProcessById(pid).ProcessName;
            var processesByName = Process.GetProcessesByName(processName);
            string processIndexdName = null;

            for (var index = 0; index < processesByName.Length; index++)
            {
                processIndexdName = index == 0 ? processName : processName + "#" + index;
                var processId = new PerformanceCounter("Process", "ID Process", processIndexdName);
                if ((int)processId.NextValue() == pid)
                {
                    return processIndexdName;
                }
            }

            return processIndexdName;
        }

        static Process FindPidFromIndexedProcessName(string indexedProcessName)
        {
            var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
            return Process.GetProcessById((int)parentId.NextValue());
        }

        public static Process Parent(this Process process)
        {
            try
            {
                return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id));
            }
            catch { }
            return null;
        }

        public static void KillGroup(this Process process, Func<Process, bool> childrenHint = null)
        {
            foreach (var item in process.Children(childrenHint).Concat(new[] { process }))
                try { item.Kill(); }
                catch { }
        }

        public static IEnumerable<Process> Children(this Process process, Func<Process, bool> hint = null)
        {
            var items = Process.GetProcesses()
                               .Where(p => hint == null || hint(p))
                               .Select(p => new { Process = p, Parent = p.Parent() })
                               .Where(x => x.Parent != null && x.Parent.Id == process.Id)
                               .Select(x => x.Process)
                               .ToArray();
            return items;
        }

        public static int GetParentProcessIdOnLinux(int processId)
        {
            string line;
            using (StreamReader reader = new StreamReader("/proc/" + processId + "/stat"))
                line = reader.ReadLine();

            int endOfName = line.LastIndexOf(')');
            string[] parts = line.Substring(endOfName).Split(new char[] { ' ' }, 4);

            if (parts.Length >= 3)
            {
                int ppid = Int32.Parse(parts[2]);
                return ppid;
            }

            return -1;
        }
    }

    static class FileExtensions
    {
        public static int ToInt(this string data)
        {
            int result;
            int.TryParse(data, out result);
            return result;
        }

        public static bool IsEmpty(this string data)
        {
            return string.IsNullOrEmpty(data);
        }

        public static bool IsSameAs(this string text1, string text2, bool ignoreCase = false)
        {
            return string.Compare(text1, text2, ignoreCase) == 0;
        }

        public static string[] GetLines(this string data, string lineBreak = "\n")
        {
            return data.Split(new string[] { lineBreak }, StringSplitOptions.None);
        }

        public static T InitFromArgs<T>(this T obj, string[] args)
        {
            var argList = new List<string>(args);

            foreach (FieldInfo field in obj.GetType().GetFields())
            {
                string expected = "-" + field.Name + ":";
                var matches = args.Where(a => a.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
                                  .Select(a => new { Value = a.Substring(expected.Length), Arg = a });

                if (matches.Any())
                {
                    var argsToRemove = matches.Select(x => x.Arg);
                    argList.RemoveAll(x => argsToRemove.Contains(x));

                    if (field.FieldType.IsArray)
                    {
                        var values = matches.Select(x => Convert.ChangeType(x.Value, field.FieldType.GetElementType()))
                                            .ToArray();

                        var array = Array.CreateInstance(field.FieldType.GetElementType(), values.Count());
                        for (int i = 0; i < array.Length; i++)
                            array.SetValue(values[i], i);

                        field.SetValue(obj, array);
                    }
                    else
                    {
                        object value = Convert.ChangeType(matches.Last().Value, field.FieldType);
                        field.SetValue(obj, value);
                    }
                }
                else if (field.FieldType == typeof(bool))
                {
                    expected = "-" + field.Name;
                    string argValue = argList.LastOrDefault(a => a.Equals(expected, StringComparison.OrdinalIgnoreCase));
                    if (argValue != null)
                    {
                        field.SetValue(obj, true);
                        argList.Remove(argValue);
                    }
                }
            }

            return obj;
        }
    }

    static class SyntaxMapper
    {
        public static int MapAbsPosition(string textA, int positionA, string textB)
        {
            //position is a caret position that is a string pos+1 for the case when the caret is
            //after the char at pos
            if (positionA == textA.Length)
                return textB.Length;

            int rightOffset = textA.OffsetToNextToken(positionA); //move to the next token if currently at white space

            int syntaxLength = textA.PosToSyntaxLength(positionA + rightOffset);
            int positionB = textB.SyntaxLengthToPos(syntaxLength);

            return positionB;
        }

        internal static int OffsetToNextToken(this string text, int pos)
        {
            int offset = 0;
            for (int i = pos; i < text.Length; i++)
            {
                if (IsMeaningfull(text[i], true))
                    break;
                offset++;
            }
            return offset;
        }

        public static int GetArgumentCount(string memberInfo)
        {
            //Method: void Class.Method(string text, Dictionary<int, string> map)
            //Method: void Console.WriteLine(bool value)

            int start = memberInfo.IndexOf('(');
            int end = memberInfo.LastIndexOf(')');
            if (start != -1 && end != -1 && (end - start) != 1)
            {
                char[] array = memberInfo.Substring(start, end - start).ToCharArray();

                int count = 1;
                int level = 0;
                foreach (char c in array)
                {
                    //exclude all comas in the angle brackets
                    if (c == '<')
                        level++;
                    else if (c == '>')
                        level--;
                    if (level == 0 && c == ',')
                        count++;
                }
                return count;
            }
            else
            {
                return 0;
            }
        }

        static bool IsMeaningfull(char c, bool countLineBreaks = false)
        {
            if (countLineBreaks)
                return (c == '\r' || c == '\n' || !char.IsWhiteSpace(c));
            else
                return !char.IsWhiteSpace(c);
        }

        internal static int PosToSyntaxLength(this string text, int pos)
        {
            int syntaxLength = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (IsMeaningfull(text[i]))
                {
                    syntaxLength++;
                }

                if (i == pos)
                    break;
            }

            return syntaxLength;
        }

        internal static int SyntaxLengthToPos(this string text, int syntaxLength)
        {
            var textBuf = new StringBuilder();

            int absolutePos = -1;
            int currentSyntaxLength = 0;

            for (int i = 0; i < text.Length; i++)
            {
                absolutePos++;

                if (IsMeaningfull(text[i]))
                {
                    currentSyntaxLength++;
                    if (currentSyntaxLength == syntaxLength)
                        break;
                }
            }

            return absolutePos;
        }
    }
}