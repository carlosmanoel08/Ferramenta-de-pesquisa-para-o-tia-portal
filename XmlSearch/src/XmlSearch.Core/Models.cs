namespace XmlSearch.Core;

public static class ElementKinds
{
    public const string Block = "Bloco";
    public const string Tag = "Tag";
    public const string Variable = "Variável";
    public const string DbMember = "Membro DB";
    public static readonly string[] All = [Block, Tag, Variable, DbMember];
}

public sealed record SearchEntry(long Id, string FilePath, string RelativePath, string Project,
    string Cpu, string Kind, string Name, string? DataType, string? Section, string? Comment,
    string? Network, string? Content)
{
    public string NetworkDisplay => string.IsNullOrWhiteSpace(Network) ? "Não aplicável" : Network;
}

public sealed record SearchFilters(string Query, IReadOnlyCollection<string> Projects,
    IReadOnlyCollection<string> Cpus, IReadOnlyCollection<string> Kinds, int Limit = 1000);

public sealed record IndexProgress(int ProcessedFiles, int TotalFiles, int IndexedEntries, string CurrentFile);
public sealed record IndexIssue(string FilePath, string Message);
public sealed record IndexResult(int FilesScanned, int FilesChanged, int EntriesIndexed, IReadOnlyList<IndexIssue> Issues);
