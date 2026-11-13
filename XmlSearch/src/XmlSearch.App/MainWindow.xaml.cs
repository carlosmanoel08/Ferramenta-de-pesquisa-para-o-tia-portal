using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using XmlSearch.Core;
using XmlSearch.Indexer;

namespace XmlSearch.App;

public partial class MainWindow : Window
{
    private readonly SearchRepository _repository;
    private readonly XmlIndexService _indexer;
    private readonly ObservableCollection<SearchEntry> _results = [];
    public MainWindow()
    {
        InitializeComponent();
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XmlSearch");
        _repository = new SearchRepository(Path.Combine(appData, "search.db"));
        _indexer = new XmlIndexService(_repository, new SiemensXmlParser());
        SourceRootTextBox.Text = FindDefaultRefsRoot(); ResultsGrid.ItemsSource = _results;
        RefreshFilterChoices(); Search();
    }
    private static string FindDefaultRefsRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null) { var refs = Path.Combine(directory.FullName, "refs"); if (Directory.Exists(refs)) return refs; directory = directory.Parent; }
        return Path.Combine(Directory.GetCurrentDirectory(), "refs");
    }
    private async void Reindex_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReindexButton.IsEnabled = false; var progress = new Progress<IndexProgress>(p => StatusText.Text = $"Indexando {p.ProcessedFiles}/{p.TotalFiles}: {p.CurrentFile}");
            var result = await _indexer.ReindexAsync(SourceRootTextBox.Text.Trim(), progress);
            RefreshFilterChoices(); Search(); StatusText.Text = $"Concluído: {result.FilesScanned} XMLs, {result.FilesChanged} alterados, {result.EntriesIndexed} itens indexados." + (result.Issues.Count == 0 ? "" : $" Avisos: {result.Issues.Count}; primeiro: {result.Issues[0].FilePath} — {result.Issues[0].Message}");
        }
        catch (Exception ex) { StatusText.Text = "Falha na indexação: " + ex.Message; }
        finally { ReindexButton.IsEnabled = true; }
    }
    private void RefreshFilterChoices()
    {
        ReplaceItems(ProjectsList, _repository.Values("project")); ReplaceItems(CpusList, _repository.Values("cpu")); ReplaceItems(KindsList, ElementKinds.All);
    }
    private static void ReplaceItems(ListBox list, IEnumerable<string> values) { list.ItemsSource = values.ToList(); }
    private void Search()
    {
        var filters = new SearchFilters(QueryTextBox?.Text ?? "", Selected(ProjectsList), Selected(CpusList), Selected(KindsList));
        _results.Clear(); foreach (var entry in _repository.Search(filters)) _results.Add(entry); if (StatusText is not null) StatusText.Text = $"{_results.Count} resultado(s), máximo de 1000.";
    }
    private static IReadOnlyCollection<string> Selected(ListBox list) => list.SelectedItems.Cast<string>().ToArray();
    private void Filters_Changed(object sender, SelectionChangedEventArgs e) => Search();
    private void Query_KeyUp(object sender, KeyEventArgs e) => Search();
    private void Clear_Click(object sender, RoutedEventArgs e) { QueryTextBox.Clear(); ProjectsList.UnselectAll(); CpusList.UnselectAll(); KindsList.UnselectAll(); Search(); }
    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e) => OpenButton.IsEnabled = ResultsGrid.SelectedItem is SearchEntry;
    private void Open_Click(object sender, RoutedEventArgs e) { if (ResultsGrid.SelectedItem is SearchEntry entry && File.Exists(entry.FilePath)) Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true }); }
}
