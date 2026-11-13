using XmlSearch.Core;

namespace XmlSearch.Indexer;

public sealed record XmlFileInfo(string FullPath, string RelativePath, string Project, string Cpu, long Length, long LastWriteTicks);

public static class XmlDiscovery
{
    public static IReadOnlyList<XmlFileInfo> Discover(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Pasta de XMLs não encontrada: {root}");
        return Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
            .Select(path => ToInfo(root, path)).ToList();
    }

    private static XmlFileInfo ToInfo(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var projectAt = Array.FindIndex(parts, p => p.EndsWith(".ap17", StringComparison.OrdinalIgnoreCase));
        var project = projectAt >= 0 ? parts[projectAt] : "Desconhecido";
        var cpu = projectAt >= 0 && projectAt + 1 < parts.Length ? parts[projectAt + 1] : "Desconhecida";
        var file = new FileInfo(path);
        return new(path, relative, project, cpu, file.Length, file.LastWriteTimeUtc.Ticks);
    }
}
