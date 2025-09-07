using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using Nuke.Components;

class Build : NukeBuild, IRestore, ICompile
{
    public static int Main () => Execute<Build>();

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

}
