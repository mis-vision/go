using System;
using System.IO;
using static Bullseye.Targets;
using static Build.Buildary.Runner;
using static Build.Buildary.Directory;
using static Build.Buildary.Path;
using static Build.Buildary.Shell;
using static Build.Buildary.Log;

namespace Build
{
    static class Program
    {
        static void Main(string[] args)
        {
            var options = ParseOptions<RunnerOptions>(args);
            var tmpRepo = ExpandPath("./tmp-repo");
            var output = ExpandPath("./output");
            var sha = ReadShell("git rev-parse --short HEAD");
            var commitAuthorEmail = ReadShell($"git show -s --format='%ae' {sha}");

            Target("build-clean", () =>
            {
                if (DirectoryExists(output))
                {
                    DeleteDirectory(output);
                }
            });
            
            Target("build", DependsOn("build-clean"), () =>
            {
                RunShell($"vangen -out {output}");
                RunShell($"echo 'go.mis.vision' > {System.IO.Path.Join(output, "CNAME")}");
                foreach (var indexFile in System.IO.Directory.GetFiles(output, "*.html", SearchOption.AllDirectories))
                {
                    var newDestination = System.IO.Path.GetDirectoryName(indexFile) + ".html";
                    File.Move(indexFile, newDestination);
                }
            });
            
            Target("deploy-clean", () =>
            {
                if (DirectoryExists(tmpRepo))
                {
                    DeleteDirectory(tmpRepo);
                }
            });
            
            Target("deploy", DependsOn("deploy-clean"), () =>
            {
                if (string.IsNullOrEmpty(commitAuthorEmail))
                {
                    Failure("No COMMIT_AUTHOR_EMAIL, skipping deploy...");
                    Environment.Exit(1);
                }
                
                RunShell($"git clone git@github.com:mis-vision/go.git {tmpRepo}");
                
                using (ChangeDirectory(tmpRepo))
                {
                    RunShell("git checkout pages || git checkout --orphan pages");

                    RunShell("git rm -r .");
                    RunShell($"cp -r {output}/. {tmpRepo}");
                    RunShell("git add .");

                    if (string.IsNullOrEmpty(ReadShell("git status --porcelain")))
                    {
                        Info("No changes, skipping deploy...");
                        return;
                    }
                    
                    RunShell("git config user.name \"Actions CI\"");
                    RunShell($"git config user.email \"{commitAuthorEmail}\"");
                    RunShell($"git commit -m \"Deploy to GitHub Pages: {sha}\"");
                    RunShell($"git push origin pages");
                }
            });
            
            Target("default", DependsOn("build"));
            Target("ci", DependsOn("build", "deploy"));
            
            Execute(options);
        }
    }
}