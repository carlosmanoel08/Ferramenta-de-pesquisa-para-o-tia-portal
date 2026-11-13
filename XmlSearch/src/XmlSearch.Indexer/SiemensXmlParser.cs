using System.Text.RegularExpressions;
using System.Xml.Linq;
using XmlSearch.Core;

namespace XmlSearch.Indexer;

public sealed class SiemensXmlParser
{
    public IReadOnlyList<SearchEntry> Parse(XmlFileInfo file)
    {
        var document = XDocument.Load(file.FullPath, LoadOptions.PreserveWhitespace);
        var entries = new List<SearchEntry>();
        var blockNodes = document.Descendants().Where(x => x.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal));
        foreach (var block in blockNodes)
        {
            var blockName = Value(block, "Name") ?? block.Descendants().FirstOrDefault(x => x.Name.LocalName == "Name")?.Value ?? Path.GetFileNameWithoutExtension(file.FullPath);
            var blockType = block.Name.LocalName["SW.Blocks.".Length..];
            entries.Add(Entry(file, ElementKinds.Block, blockName, blockType, null, Comment(block), null, Compact(block.Value)));
            AddInterfaceVariables(file, block, blockName, entries);
            if (blockType.Contains("DB", StringComparison.OrdinalIgnoreCase)) AddDbMembers(file, block, entries);
            AddNetworkEntries(file, block, blockName, blockType, entries);
        }

        foreach (var tag in document.Descendants().Where(IsTagDefinition))
        {
            var name = Value(tag, "Name");
            if (!string.IsNullOrWhiteSpace(name))
                entries.Add(Entry(file, ElementKinds.Tag, name, Value(tag, "Datatype") ?? Value(tag, "DataType"), null, Comment(tag), null, Compact(tag.Value)));
        }
        return entries;
    }

    private static bool IsTagDefinition(XElement element) =>
        (element.Name.LocalName.Contains("Tag", StringComparison.OrdinalIgnoreCase) || element.Parent?.Name.LocalName.Contains("Tags", StringComparison.OrdinalIgnoreCase) == true)
        && Value(element, "Name") is not null && !element.Name.LocalName.Contains("Table", StringComparison.OrdinalIgnoreCase);

    private static void AddInterfaceVariables(XmlFileInfo file, XElement block, string blockName, List<SearchEntry> entries)
    {
        foreach (var section in block.Descendants().Where(x => x.Name.LocalName == "Section"))
        {
            var sectionName = Value(section, "Name");
            if (sectionName is not ("Input" or "Output" or "InOut" or "Temp" or "Constant" or "Return")) continue;
            foreach (var member in section.Descendants().Where(x => x.Name.LocalName == "Member"))
            {
                var name = Value(member, "Name");
                if (!string.IsNullOrWhiteSpace(name))
                    entries.Add(Entry(file, ElementKinds.Variable, $"{blockName}.{MemberPath(member)}", Value(member, "Datatype"), sectionName, Comment(member), null, Compact(member.Value)));
            }
        }
    }

    private static void AddDbMembers(XmlFileInfo file, XElement block, List<SearchEntry> entries)
    {
        foreach (var member in block.Descendants().Where(x => x.Name.LocalName == "Member"))
        {
            var name = Value(member, "Name");
            if (!string.IsNullOrWhiteSpace(name))
                entries.Add(Entry(file, ElementKinds.DbMember, MemberPath(member), Value(member, "Datatype"), null, Comment(member), null, Compact(member.Value)));
        }
    }

    private static void AddNetworkEntries(XmlFileInfo file, XElement block, string blockName, string blockType, List<SearchEntry> entries)
    {
        foreach (var network in block.Descendants().Where(x => x.Name.LocalName.Contains("Network", StringComparison.OrdinalIgnoreCase)))
        {
            var number = Value(network, "Number") ?? Value(network, "ID") ?? "Network";
            var text = Compact(network.Value);
            if (!string.IsNullOrWhiteSpace(text)) entries.Add(Entry(file, ElementKinds.Block, blockName, blockType, null, null, number, text));
        }
    }

    private static SearchEntry Entry(XmlFileInfo file, string kind, string name, string? datatype, string? section, string? comment, string? network, string? content) =>
        new(0, file.FullPath, file.RelativePath, file.Project, file.Cpu, kind, name, datatype, section, comment, network, content);
    private static string? Value(XElement node, string attribute) => node.Attribute(attribute)?.Value;
    private static string MemberPath(XElement member) => string.Join('.', member.AncestorsAndSelf().Where(x => x.Name.LocalName == "Member").Reverse().Select(x => Value(x, "Name")).Where(x => !string.IsNullOrWhiteSpace(x))!);
    private static string? Comment(XElement node) => node.Descendants().FirstOrDefault(x => x.Name.LocalName.Contains("Comment", StringComparison.OrdinalIgnoreCase))?.Value;
    private static string Compact(string value)
    {
        var text = Regex.Replace(value, @"\s+", " ").Trim();
        return text.Length > 12000 ? text[..12000] : text;
    }
}
