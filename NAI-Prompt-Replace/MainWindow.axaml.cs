using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;

namespace NAI_Prompt_Replace;

public partial class MainWindow : Window
{
    private const string config_file = "config.json";
#if DEBUG
    private readonly Random random = new Random(1337);
    #else
    private readonly Random random = new Random();
#endif
    private Dictionary<string, string> replacements { get; set; } = new Dictionary<string, string>();
    private readonly NovelAIApi api = new NovelAIApi();

    private Config config { get; set; } = new Config();
    private CancellationTokenSource? cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
        loadConfig();
        DataContext = config;
        AddHandler(DragDrop.DropEvent, onDrop);
    }

    private async void onDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();

            foreach (var item in files)
            {
                if (item is IStorageFile file)
                {
                    await openFile(file);
                }
            }
        }
    }

    private void loadConfig()
    {
        if (File.Exists(config_file))
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(config_file)) ?? config;
            updateAccountInfo().ConfigureAwait(false);
        }
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        saveConfig();
    }

    private void saveConfig()
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            File.WriteAllText(config_file, JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception e)
        {
        }
    }

    private void NewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        addTab("New config", new GenerationConfig());
    }

    private void addTab(string header, GenerationConfig config)
    {
        var control = new GenerationParameterControl(config, api);
        TabControl.Items.Add(new TabItem { Header = header, Content = control });
        control.AnlasChanged += updateTotalAnlas;
        updateTotalAnlas(null, null);
    }

    private void updateTotalAnlas(object? sender, EventArgs? e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            int total = 0;

            foreach (var control in getGenerationParameterControls())
            {
                var config = control.Config;
                int cost = control.AnlasDisplay.Value;
                var replaceLines = config.Replace.Split(Environment.NewLine).Select(l => Math.Max(l.Split(',').Length, 1));

                foreach (int line in replaceLines)
                    cost *= line;

                total += cost;
            }

            TotalAnlas.Value = total;
        });
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TabControl.Items.Count > 0)
        {
            if (TabControl.Items[TabControl.SelectedIndex] is GenerationParameterControl control)
            {
                control.AnlasChanged -= updateTotalAnlas;
            }

            TabControl.Items.RemoveAt(TabControl.SelectedIndex);
            updateTotalAnlas(null, null);
        }
    }

    private void TabControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        TabControl?.InvalidateMeasure();
    }

    private void ReplacementDataGrid_OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Width = DataGridLength.Auto;
    }

    private void ShowPasswordButton_OnClick(object? sender, RoutedEventArgs e)
    {
        TokenTextBox.RevealPassword = !TokenTextBox.RevealPassword;
        ShowPasswordButton.Content = TokenTextBox.RevealPassword ? "Hide" : "Show";
    }

    private void RunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        if (cancellationTokenSource != null)
        {
            RunButton.Content = "Run";
            cancellationTokenSource.Cancel();
            ProgressBar.Value = 0;
            cancellationTokenSource = null;
        }
        else
        {
            RunButton.Content = "Cancel";
            cancellationTokenSource = new CancellationTokenSource();
            var configs = getGenerationConfigs().ToList();
            ProgressBar.Value = 0;
            clearLog();
            Task.Factory.StartNew(_ => createAndRunTasks(configs, cancellationTokenSource.Token), null, TaskCreationOptions.LongRunning);
        }
    }

    private IEnumerable<GenerationParameterControl> getGenerationParameterControls()
    {
        foreach (object? item in TabControl.Items)
        {
            if (((TabItem)item).Content is GenerationParameterControl parameterControl)
            {
                yield return parameterControl;
            }
        }
    }

    private IEnumerable<GenerationConfig> getGenerationConfigs() => getGenerationParameterControls().Select(p => p.Config);

    private string replaceText(string text)
    {
        string[] lines = text.Split(Environment.NewLine);
        List<string> newLines = [];

        foreach (string line in lines)
        {
            string[] words = line.Split(',', StringSplitOptions.TrimEntries);

            for (int i = 0; i < words.Length; i++)
            {
                if (replacements.ContainsKey(words[i]))
                    words[i] = replacements[words[i]];
            }

            newLines.Add(string.Join(',', words));
        }

        return string.Join(Environment.NewLine, newLines);
    }

    private void writeLogLine(object obj)
    {
        writeLog(obj + Environment.NewLine);
    }

    private void writeLog(object obj)
    {
        Dispatcher.UIThread.Invoke(() => LogTextBox.Text += obj);
    }

    private void clearLog()
    {
        Dispatcher.UIThread.Invoke(() => LogTextBox.Text = string.Empty);
    }

    private async void OpenButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            FileTypeFilter =
            [
                new FilePickerFileType("JSON, PNG or CSV") { Patterns = ["*.json", "*.csv", "*.png"] }
            ],
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            await openFile(file);
        }
    }

    private async Task createAndRunTasks(IEnumerable<GenerationConfig> configs, CancellationToken token)
    {
        var tasks = new List<GenerationConfig>();

        foreach (var generationConfig in configs)
        {
            var g = generationConfig.Clone();
            g.GenerationParameter.Seed ??= random.Next();
            long seed = g.GenerationParameter.Seed.Value;

            g.Prompt = replaceText(g.Prompt);
            g.Replace = replaceText(g.Replace);

            string prompt = g.Prompt;

            string[][]? replaceLines = g.Replace.Split(Environment.NewLine).Select(s => s.Split(',', StringSplitOptions.TrimEntries)).ToArray();

            // TODO: Rewrite this
            void replacePrompt(int now, string ss)
            {
                if (now == prompt.Length)
                {
                    for (int j = 0; j < g.BatchSize; j++)
                    {
                        var clone = g.Clone();
                        clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + j;
                        clone.Prompt = ss;
                        tasks.Add(clone);
                    }

                    return;
                }

                bool ok = false;

                foreach (string[] replaces in replaceLines)
                {
                    if (now + replaces[0].Length <= prompt.Length && prompt[now..(now + replaces[0].Length)] == replaces[0])
                    {
                        ok = true;

                        foreach (string t1 in replaces)
                        {
                            replacePrompt(now + replaces[0].Length, ss + t1);
                        }
                    }
                }
                if (!ok)
                {
                    replacePrompt(now + 1, ss + prompt[now]);
                }
            }

            if (replaceLines.Length > 0 && replaceLines[0].Length > 1)
            {
                replacePrompt(0, string.Empty);
            }
            else
            {
                for (int j = 0; j < g.BatchSize; j++)
                {
                    var clone = g.Clone();
                    clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + j;
                    tasks.Add(clone);
                }
            }
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 5;
        var date = DateTime.Now;

        while (i < tasks.Count)
        {
            var task = tasks[i];
            token.ThrowIfCancellationRequested();
            writeLog($"Running task {i + 1} / {tasks.Count}: ");
            var resp = await api.Generate(task);
            Dispatcher.UIThread.Invoke(updateAccountInfo);
            bool success = resp.IsSuccessStatusCode;

            if (success)
            {
                writeLogLine($"{(int)resp.StatusCode} {resp.ReasonPhrase}");
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                string fileName = task.GenerationParameter.Seed + "-";
                fileName = Util.GetValidFileName(fileName + task.Prompt[..Math.Min(128, task.Prompt.Length)] + ".png");

                string path = string.IsNullOrWhiteSpace(task.OutputPath) ? Environment.CurrentDirectory : task.OutputPath;
                string[] split = path.Split(Path.DirectorySeparatorChar);

                for (int index = 0; index < split.Length; index++)
                {
                    string dir = split[index];

                    if (Path.IsPathRooted(dir))
                        continue;

                    split[index] = Util.GetValidDirectoryName(dir.Replace("${date}", date.ToShortDateString())
                        .Replace("${time}", date.ToShortTimeString()));
                }

                path = string.Join(Path.DirectorySeparatorChar, split);
                Directory.CreateDirectory(path);
                fileName = Path.Combine(path, fileName);

                await using var file = File.OpenWrite(fileName);

                foreach (var entry in zip.Entries)
                {
                    await using var s = entry.Open();
                    await s.CopyToAsync(file);
                    file.Close();
                }
            }
            if (resp.Content.Headers.ContentType?.MediaType == "application/json")
            {
                string content = await resp.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<NovelAIGenerationResponse>(content);
                writeLogLine($"Error: {response?.StatusCode} {response?.Message}");
            }

            token.ThrowIfCancellationRequested();

            if (!success && retry < maxRetries)
            {
                if (Util.CalculateCost(task, api.SubscriptionInfo) > 0 && !task.RetryAll)
                {
                    i++;
                    continue;
                }

                writeLogLine($"Failed, Retrying {++retry} / {maxRetries}");
                Thread.Sleep(retry >= 3 ? 5000 : 3000);
            }
            else
            {
                retry = 0;
                Dispatcher.UIThread.Invoke(() => ProgressBar.Value = (i + 1.0) / tasks.Count * 100);
                i++;
            }
        }

        Dispatcher.UIThread.Invoke(() =>
        {
            RunButton.Content = "Run";
            cancellationTokenSource = null;
        });
    }

    private async Task openFile(IStorageFile file)
    {
        try
        {
            string path = file.Path.LocalPath;
            GenerationConfig? config = null;

            switch (Path.GetExtension(file.Name))
            {
                case ".json":
                    config = JsonSerializer.Deserialize<GenerationConfig>(await File.ReadAllTextAsync(path));
                    break;

                case ".png":
                    config = PngMetadataReader.FromFile(path);
                    break;

                case ".csv":
                    using (var reader = File.OpenText(path))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false }))
                    {
                        var records = csv.GetRecords<TextReplacement>().ToList();
                        ReplacementDataGrid.ItemsSource = records;
                        replacements = records.ToDictionary(r => r.Text, r => r.Replace);
                    }
                    break;
            }

            if (config != null)
            {
                addTab(file.Name[..Math.Min(32, file.Name.Length)], config);
            }
        }
        catch (Exception exception)
        {
            writeLogLine(exception.Message);
        }
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs? e)
    {
        await updateAccountInfo();
    }

    private async Task updateAccountInfo()
    {
        if (Design.IsDesignMode)
            return;

        var subscriptionInfo = await api.UpdateToken(config.AccessToken);

        if (subscriptionInfo != null)
        {
            AccountInfo.Text = subscriptionInfo.ToString();
            AccountInfo.Foreground = new SolidColorBrush(subscriptionInfo.Active ? Colors.Black : Colors.Red);
            AccountAnlasDisplay.Value = subscriptionInfo.TotalTrainingStepsLeft;
        }
    }
}
