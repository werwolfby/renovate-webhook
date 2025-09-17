using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JetBrains.Annotations;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities.Collections;
using Nuke.Components;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;

class Build : NukeBuild, IRestore, ICompile, IHazNerdbankGitVersioning, IHazDockerFile
{
    public static int Main () => Execute<Build>();

    [DockerArgValue] public SemanticVersion RenovateVersion { get; set; }

    [CanBeNull] public SemanticVersion TargetRenovateVersion { get; set; }

    [Parameter] public bool Push { get; set; }

    [Parameter] public string Platforms { get; set; } = "linux/amd64,linux/arm64";

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

    Target GetLatestRenovateVersion => _ => _
        .WhenSkipped(DependencyBehavior.Execute)
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

            latestRenovateVersion ??= RenovateVersion;

            ReportSummary(c => c
                .AddPair("Latest Renovate Version", latestRenovateVersion?.ToString() ?? "N/A")
                .AddPair("Current Renovate Version", RenovateVersion.ToString()));

            TargetRenovateVersion = latestRenovateVersion;
        });

    Target UpgradeRenovateVersion => _ => _
        .DependsOn(GetLatestRenovateVersion)
        .OnlyWhenDynamic(() => TargetRenovateVersion != null)
        .Executes(() =>
        {
            if (TargetRenovateVersion == RenovateVersion)
            {
                Log.Logger.Information("Already at latest Renovate version {RenovateVersion}", RenovateVersion);
                ReportSummary(c => c
                    .AddPair("Renovate Version", RenovateVersion.ToString()));
                return;
            }

            Log.Logger.Information("Upgrading Renovate version from {OldVersion} to {NewVersion}", RenovateVersion, TargetRenovateVersion);

            var dockerFile = ((IHazDockerFile)this).DockerFile;
            var argName = this.GetType().GetMember(nameof(RenovateVersion))
                .Single()
                .GetCustomAttributes(typeof(DockerArgValueAttribute), false)
                .OfType<DockerArgValueAttribute>()
                .First()
                .GetArgName(nameof(RenovateVersion));

            bool updated = false;

            dockerFile.UpdateText(text =>
            {
                // Use \n intentionally instead of Environment.NewLine
                // to keep existing line endings, and change only the line we want to change
                var lines = text.Split("\n");
                var updatedLines = lines
                    .Select(l =>
                    {
                        if (!l.StartsWith("ARG ") || !l.Contains(argName) || !l.Contains(RenovateVersion.ToString()))
                            return l;

                        updated = true;
                        return $"ARG {argName}={TargetRenovateVersion}";
                    });

                return string.Join("\n", updatedLines);
            });

            if (!updated)
                throw new Exception($"Failed to update {argName} in {dockerFile} to {TargetRenovateVersion}");

            Log.Logger.Information("Updated version in version.json");

            ReportSummary(c => c
                .AddPair("New Renovate Version", TargetRenovateVersion!.ToString()));

            var message = $"build(renovate): upgrade renovate image to {TargetRenovateVersion}";
            var author = "github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>";

            Log.Logger.Information("Committing change {Message}", message);
            GitTasks.Git($"commit {dockerFile} -m {message} --author={author}");

            if (Push)
            {
                Log.Logger.Information("Pushing change {Message}", message);
                GitTasks.Git("push origin HEAD");
            }
        });

    Target Docker => _ => _
        .DependsOn<ICompile>()
        .Executes(() =>
        {
            var version = ((IHazNerdbankGitVersioning)this).Versioning.SimpleVersion;
            var imageName = "werwolfby/renovate-webhook";
            var tag = $"{version}-{RenovateVersion}";
            var dockerTag = $"{imageName}:{tag}";
            var latestTag = $"{imageName}:latest";

            DockerBuildxBuild(s => s
                .SetPath(RootDirectory)
                .SetFile(RootDirectory / "Dockerfile")
                .SetTag(dockerTag, latestTag)
                .SetPull(true)
                .SetPlatform(Platforms)
                .SetPush(Push)
                .SetCacheTo(GitHubActions.Instance != null
                    ? ["type=gha,mode=max"]
                    : [])
                .SetCacheFrom(GitHubActions.Instance != null
                    ? ["type=gha"]
                    : []));

            ReportSummary(c => c
                .AddPair("Docker Image", dockerTag));

            Log.Logger.Information("Built Docker image {DockerTag}", dockerTag);

            if (Push)
            {
                Log.Logger.Information("Pushed Docker image {DockerTag}", dockerTag);

                GitTasks.Git($"tag {tag}");
                GitTasks.Git("push origin HEAD --tags");

                Log.Logger.Information("Pushed Git tag {Tag}", tag);

                ReportSummary(c => c
                    .AddPair("Docker Image", dockerTag)
                    .AddPair("Git Tag", tag));
            }
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
