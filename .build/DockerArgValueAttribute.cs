using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.ValueInjection;

public class DockerArgValueAttribute : ValueInjectionAttributeBase
{
    [CanBeNull] public string ArgName { get; set; }

    public ArgNameCase Case { get; set; } = ArgNameCase.UpperSnakeCase;

    [CanBeNull] public RelativePath DockerFile { get; set; }

    public override object GetValue(MemberInfo member, object instance)
    {
        var argName = ArgName ?? GetArgName(member.Name);

        var dockerFile = DockerFile is not null
            ? Build.RootDirectory / DockerFile
            : instance is IHazDockerFile hazDockerFile
                ? hazDockerFile.DockerFile
                : Build.RootDirectory / "Dockerfile";
        var resultValue = dockerFile.ReadAllLines()
            .Select(x => x.Trim())
            .Where(x => x.StartsWith("ARG "))
            .Select(x => x.Substring("ARG ".Length).Trim())
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0].Trim(), x => x[1].Trim())
            .TryGetValue(argName, out var value)
            ? value
            : throw new Exception($"Could not find ARG '{argName}' in {dockerFile}");

        return ReflectionUtility.Convert(resultValue, member.GetMemberType());
    }

    public string GetArgName(string memberName) => GetArgName(memberName, Case);

    public static string GetArgName(string memberName, ArgNameCase argNameCase) =>
        argNameCase switch
        {
            ArgNameCase.PascalCase => char.ToUpperInvariant(memberName[0]) + memberName[1..],
            ArgNameCase.CamelCase => char.ToLowerInvariant(memberName[0]) + memberName[1..],
            ArgNameCase.SnakeCase => string.Concat(memberName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())),
            ArgNameCase.UpperSnakeCase => string.Concat(memberName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + char.ToUpperInvariant(x) : char.ToUpperInvariant(x).ToString())),
            ArgNameCase.LowerSnakeCase => string.Concat(memberName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + char.ToLowerInvariant(x) : char.ToLowerInvariant(x).ToString())).ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException()
        };
}

public enum ArgNameCase
{
    PascalCase,
    CamelCase,
    SnakeCase,
    UpperSnakeCase,
    LowerSnakeCase,
}
