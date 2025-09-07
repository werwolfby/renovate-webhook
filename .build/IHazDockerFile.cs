using Nuke.Common;
using Nuke.Common.IO;

interface IHazDockerFile : INukeBuild
{
    public AbsolutePath DockerFile => RootDirectory / "Dockerfile";
}