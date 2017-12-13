//css_inc cmd.cs;
using System.Diagnostics;
using System;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using static dbg;

class Script
{
    static public void Main(string[] args)
    {
        dbg.max_items = 50;

        var syntaxer_roslyn_distro = Environment.ExpandEnvironmentVariables(@"%GITHUB_PROJECTS%\Sublime\cs-script\syntaxer\Roslyn");

        var roslyn_packages = Environment.ExpandEnvironmentVariables(@"%GITHUB_PROJECTS%\Sublime\cs-script\syntaxer\packages");
        var roslyn_intellisense_debug_dir = Environment.ExpandEnvironmentVariables(@"%GITHUB_PROJECTS%\cs-script.npp\src\Roslyn.Intellisesne\Roslyn.Intellisense\bin\Debug");
        var roslyn_compilers_dir = Directory.GetDirectories(roslyn_packages, "Microsoft.Net.Compilers.*").OrderBy(x => x).LastOrDefault() + "\\tools";

        print("Aggregating Roslyn");
        print("  assemblies from ", roslyn_intellisense_debug_dir);
        print("  compilers from ", roslyn_compilers_dir);

        var roslyn_asms = Directory.GetFiles(roslyn_intellisense_debug_dir, "*.dll")
                            .Concat(new[] { Path.Combine(roslyn_intellisense_debug_dir, "RoslynIntellisense.exe") });

        print(roslyn_asms);

        var compiler_dlls = Directory.GetFiles(roslyn_compilers_dir, "*.dll");
        var compiler_targets = Directory.GetFiles(roslyn_compilers_dir, "*.targets");
        var compiler_configs = Directory.GetFiles(roslyn_compilers_dir, "*.config");
        var compiler_exes = Directory.GetFiles(roslyn_compilers_dir, "*.exe");
        var compiler_rsp = Directory.GetFiles(roslyn_compilers_dir, "*.rsp");

        var common_dlls = roslyn_asms.Select(x => new { roslyn_asm = x, compiler_asm = compiler_dlls.Where(y => Path.GetFileName(y) == Path.GetFileName(x)).FirstOrDefault() })
                                     .Where(x => x.compiler_asm != null)
                                     ;

        print("----------------");

        foreach (var item in common_dlls)
        {
            // ignore last (build) version part
            var version1 = string.Join(".", FileVersionInfo.GetVersionInfo(item.roslyn_asm).FileVersion.Split('.').Take(3).ToArray());
            var version2 = string.Join(".", FileVersionInfo.GetVersionInfo(item.compiler_asm).FileVersion.Split('.').Take(3).ToArray());

            if (version1 != version2)
                print(Path.GetFileName(item.roslyn_asm), $"- VERSION MISSMATCH ({version1} vs {version2})");
        }

        var all_files = compiler_dlls.Concat(compiler_targets)
                                     .Concat(compiler_configs)
                                     .Concat(compiler_exes)
                                     .Concat(compiler_rsp)
                                     .Concat(roslyn_asms);

        foreach (var item in all_files)
        {
            cmd.copy(item, Path.Combine(syntaxer_roslyn_distro, Path.GetFileName(item)));
            break;
        }
    }
}