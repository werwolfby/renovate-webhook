using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    Target LatestRenovateVersion => _ => _
        .Executes(async () =>
        {
            var registry = "renovate";
            var image = "renovate";

            var latestRenovateVersion = await ListTagsFromDockerHub($"{registry}/{image}")
                .Select(version =>
                {
                    if (version.StartsWith("sha256"))
                        return null;

                    if (version.StartsWith("v"))
                        version = version.Substring(1);

                    SemanticVersion.TryParse(version, out var semanticVersion);
                    return semanticVersion;
                })
                .Where(x => x != null && string.IsNullOrEmpty(x.Release))
                .TakeWhile(v => v > RenovateVersion)
                .Take(100)
                .MaxAsync();

            ReportSummary(c => c
                .AddPair("Latest Renovate Version", latestRenovateVersion?.ToString() ?? "N/A")
                .AddPair("Current Renovate Version", RenovateVersion.ToString()));
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

    static async IAsyncEnumerable<string> ListTagsFromDockerHub(string repo)
    {
        var s = repo.Split('/', 2);
        var ns = s[0];
        var name = s[1];
        var url = $"https://hub.docker.com/v2/namespaces/{Uri.EscapeDataString(ns)}/repositories/{Uri.EscapeDataString(name)}/tags?page_size=100&ordering=last_updated";
        while (!string.IsNullOrEmpty(url))
        {
            Log.Logger.Information("Loading Docker tags from {Url}", url);
            var content = await HttpTasks.HttpDownloadStringAsync(url);
            var doc = JsonDocument.Parse(content);
            foreach (var x in doc.RootElement.GetProperty("results").EnumerateArray())
                yield return x.GetProperty("name").GetString();
            url = doc.RootElement.TryGetProperty("next", out var next) ? next.GetString() : null;
            Log.Logger.Information("Next page: {Url}", url);
        }
    }

}
