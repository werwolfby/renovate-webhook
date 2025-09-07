using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Utilities.Collections;
using Nuke.Components;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;

class Build : NukeBuild, IRestore, ICompile, IHazNerdbankGitVersioning
{
    public static int Main () => Execute<Build>();

    [DockerArgValue] public SemanticVersion RenovateVersion { get; set; }

    Target Clean => _ => _
        .Before<IRestore>()
        .Executes(() =>
        {
            RootDirectory
                .GlobDirectories("*/bin", "*/obj")
                // Exclude .build itself
                .WhereNot(e => e.ToString().StartsWith(BuildProjectDirectory!.ToString()))
                .DeleteDirectories();
        });

    Target Docker => _ => _
        .DependsOn<ICompile>()
        .Executes(() =>
        {
            var version = ((IHazNerdbankGitVersioning)this).Versioning.SimpleVersion;
            var imageName = "werwolfby/renovate-webhook";
            var dockerTag = $"{imageName}:{version}";
            var latestTag = $"{imageName}:latest";

            DockerBuildxBuild(s => s
                .SetPath(RootDirectory)
                .SetFile(RootDirectory / "Dockerfile")
                .SetTag(dockerTag, latestTag)
                .SetPull(true)
                .SetPlatform("linux/amd64,linux/arm64"));

            ReportSummary(c => c
                .AddPair("Docker Image", dockerTag));

            Log.Logger.Information("Built Docker image {DockerTag}", dockerTag);
        });

}
