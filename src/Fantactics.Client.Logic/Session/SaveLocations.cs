using System.Globalization;

namespace Fantactics.Client.Logic.Session;

/// <summary>
/// Where match files go. In development, the repo's <c>playtests/</c> folder (where the Sim CLI and the LLM skill
/// look); in exported builds, the user data folder. <c>--saves</c> overrides both.
/// </summary>
/// <param name="Folder">The saves folder.</param>
public sealed record SaveLocations(string Folder)
{
    /// <summary>The autosave file.</summary>
    public string Autosave => Path.Combine(Folder, "autosave.json");

    /// <summary>The quicksave file.</summary>
    public string Quicksave => Path.Combine(Folder, "quick.json");

    /// <summary>Picks the saves folder.</summary>
    /// <param name="overrideFolder">The <c>--saves</c> option, if given.</param>
    /// <param name="projectFolder">The Godot project folder (<c>res://</c>, globalized).</param>
    /// <param name="userFolder">The user data folder (<c>user://saves</c>, globalized).</param>
    /// <param name="exported">Whether this is an exported build.</param>
    public static SaveLocations Choose(string? overrideFolder, string projectFolder, string userFolder, bool exported)
    {
        if (overrideFolder is not null)
        {
            return new SaveLocations(Path.GetFullPath(overrideFolder));
        }

        DirectoryInfo? repo = exported ? null : new DirectoryInfo(projectFolder);
        while (repo is not null && !Directory.Exists(Path.Combine(repo.FullName, ".git")))
        {
            repo = repo.Parent;
        }

        return new SaveLocations(repo is null ? userFolder : Path.Combine(repo.FullName, "playtests"));
    }

    /// <summary>A new match file name, e.g. <c>match-20260928-1412.json</c>.</summary>
    public string NewMatchFile(DateTime now) =>
        Path.Combine(Folder, $"match-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.json");

    /// <summary>The file a branch of <paramref name="file"/> from command <paramref name="seq"/> goes to.</summary>
    public static string BranchFile(string file, int seq) =>
        Path.Combine(Path.GetDirectoryName(file) ?? "", $"{Path.GetFileNameWithoutExtension(file)}.b{seq}.json");
}
