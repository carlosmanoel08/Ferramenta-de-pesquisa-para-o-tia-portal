using XmlSearch.Core;

namespace XmlSearch.Indexer;

public sealed class XmlIndexService(SearchRepository repository, SiemensXmlParser parser)
{
    public async Task<IndexResult> ReindexAsync(string root, IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default) => await Task.Run(() =>
    {
        var files = XmlDiscovery.Discover(root); var issues = new List<IndexIssue>(); var changed = 0; var entries = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var file = files[i]; seen.Add(file.FullPath);
            progress?.Report(new(i + 1, files.Count, entries, file.RelativePath));
            if (repository.IsCurrent(file)) continue;
            try { var parsed = parser.Parse(file); repository.ReplaceFile(file, parsed); changed++; entries += parsed.Count; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException) { issues.Add(new(file.RelativePath, ex.Message)); }
        }
        repository.DeleteMissing(seen); return new IndexResult(files.Count, changed, entries, issues);
    }, cancellationToken);
}
