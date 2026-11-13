using XmlSearch.Core;
using XmlSearch.Indexer;

var root = Path.Combine(Path.GetTempPath(), "XmlSearchTests", Guid.NewGuid().ToString("N"));
var directory = Path.Combine(root, "P1.ap17", "CPU-1", "Program blocks");
Directory.CreateDirectory(directory);
var file = Path.Combine(directory, "Sample.xml");
await File.WriteAllTextAsync(file, """<Document><SW.Blocks.FC><AttributeList><Name>FC_Test</Name><Interface><Sections><Section Name="Input"><Member Name="Enable" Datatype="Bool" /></Section><Section Name="Temp"><Member Name="Counter" Datatype="Int" /></Section></Sections></Interface><Network Number="3">Counter := Counter + 1;</Network></AttributeList></SW.Blocks.FC><SW.Blocks.DB><AttributeList><Name>DB_Test</Name><Member Name="State" Datatype="Int"><Member Name="Nested" Datatype="Bool"/></Member></AttributeList></SW.Blocks.DB><SW.Tags.PlcTag Name="Start" Datatype="Bool" /></Document>""");
var info = XmlDiscovery.Discover(root).Single();
var entries = new SiemensXmlParser().Parse(info);
Assert(entries.Any(x => x.Kind == ElementKinds.Block && x.Name == "FC_Test"), "bloco FC");
Assert(entries.Any(x => x.Kind == ElementKinds.Variable && x.Name == "FC_Test.Enable" && x.Section == "Input"), "variável de interface");
Assert(entries.Any(x => x.Kind == ElementKinds.DbMember && x.Name == "State.Nested"), "membro DB aninhado");
Assert(entries.Any(x => x.Kind == ElementKinds.Tag && x.Name == "Start"), "tag PLC");
Assert(entries.Any(x => x.Network == "3" && x.Content?.Contains("Counter") == true), "network");
Console.WriteLine("Todos os testes do indexador passaram.");
Directory.Delete(root, recursive: true);

static void Assert(bool condition, string subject) { if (!condition) throw new InvalidOperationException($"Falhou: {subject}"); }
