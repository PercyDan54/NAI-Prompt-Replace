using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using SkiaSharp;

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
    private static readonly CsvConfiguration csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, TrimOptions = TrimOptions.Trim };
    private static readonly FilePickerOpenOptions filePickerOptions = new FilePickerOpenOptions
    {
        Title = "Open File",
        FileTypeFilter =
        [
            new FilePickerFileType("JSON, PNG or CSV") { Patterns = ["*.json", "*.csv", "*.png"] }
        ],
        AllowMultiple = true
    };

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
            updateAccountInfo(true).ConfigureAwait(false);
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
            config.AccessToken = api.AccessToken;
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
        var control = new GenerationParameterControl(config, api) { Name = header };
        TabControl.Items.Add(new TabItem { Header = header, Content = control });
        control.AnlasChanged += updateTotalAnlas;
        updateTotalAnlas(null, null);
    }

    private void updateTotalAnlas(object? sender, EventArgs? e)
    {
        Dispatcher.UIThread.Invoke(() => TotalAnlas.Value = getGenerationParameterControls().Sum(c => c.AnlasDisplay.Value));
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
            Task.Factory.StartNew(_ => createAndRunTasks(configs, cancellationTokenSource.Token).ContinueWith(task =>
            {
                if (task.Exception?.InnerException != null)
                {
                    writeLogLine($"Tasks Failed with an exception, please report this: {task.Exception.InnerException}");
                }
            }), null, TaskCreationOptions.LongRunning);
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

    private string replaceText(string text, Dictionary<string, string> replaces)
    {
        string[] lines = text.Split(Environment.NewLine);
        List<string> newLines = [];

        foreach (string line in lines)
        {
            string[] words = line.Split(',', StringSplitOptions.TrimEntries);

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string bracketStart = string.Empty;
                string bracketEnd = string.Empty;

                foreach (char c in word)
                {
                    if (c is '{' or '[')
                        bracketStart += c;
                    else if (c is '}' or ']')
                        bracketEnd += c;
                }

                string wordsNoBracket = words[i].TrimStart('{', '[').TrimEnd('}', ']');

                if (replaces.TryGetValue(word, out string? replacement))
                {
                    words[i] = replacement;
                }
                else if (replaces.TryGetValue(wordsNoBracket, out replacement))
                {
                    words[i] = $"{bracketStart}{replacement}{bracketEnd}";
                }
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
        var files = await StorageProvider.OpenFilePickerAsync(filePickerOptions);

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

            // Trim spaces between words
            string[] tags = g.Prompt.Split(',', StringSplitOptions.TrimEntries);
            string prompt = g.Prompt = string.Join(',', tags);
            g.Replace = string.Join(',', g.Replace.Split(',', StringSplitOptions.TrimEntries));
            List<string[]> replaceLines = [];

            using var reader = new StringReader(g.Replace);
            using (var csv = new CsvParser(reader, csvConfiguration))
            {
                while (csv.Read())
                {
                    string[] records = csv.Record;
                    string toReplace = records[0];
                    int index = g.Prompt.IndexOf(toReplace, StringComparison.Ordinal);
                    int end = index + toReplace.Length;

                    // Ensure the matched tag is a full word split by comma
                    if (index >= 0 && (index == 0 || end == prompt.Length || Regex.IsMatch(prompt, $@",(?:\{{|\[)*{Regex.Escape(toReplace)}(?:\}}|\])*,")))
                    {
                        replaceLines.Add(records);
                    }
                }
            }

            if (replaceLines.Count > 0 && replaceLines[0].Length > 1)
            {
                var combos = Util.GetAllPossibleCombos(replaceLines);
                var toReplaces = replaceLines.Select(l => l[0]).ToList();

                for (int j = 0; j < g.BatchSize; j++)
                {
                    foreach (var combo in combos)
                    {
                        var clone = g.Clone();
                        clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + j;

                        for (int k = 0; k < combo.Count; k++)
                        {
                            clone.Prompt = clone.Prompt.Replace(toReplaces[k], combo[k]);
                        }

                        clone.Prompt = replaceText(clone.Prompt, replacements);
                        clone.CurrentReplace = replaceText(string.Join(',', combo), replacements);
                        tasks.Add(clone);
                    }
                }
            }
            else
            {
                for (int j = 0; j < g.BatchSize; j++)
                {
                    var clone = g.Clone();
                    clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + j;
                    clone.Prompt = replaceText(prompt, replacements);
                    clone.CurrentReplace = clone.Prompt;
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
            HttpResponseMessage? resp = null;

            try
            {
                resp = await api.Generate(task).WaitAsync(TimeSpan.FromMinutes(2));
            }
            catch (Exception e)
            {
                writeLogLine(e.Message);
            }

            Dispatcher.UIThread.Invoke(() => updateAccountInfo());
            bool success = resp?.IsSuccessStatusCode ?? false;

            if (success)
            {
                writeLogLine($"{(int)resp.StatusCode} {resp.ReasonPhrase}");
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                string path = string.IsNullOrWhiteSpace(task.OutputPath) ? Environment.CurrentDirectory : task.OutputPath;

                string[] split = path.Split(Path.DirectorySeparatorChar);
                var placeholders = new Dictionary<string, string>(StringComparer.Ordinal) 
                {
                    { "date", date.ToShortDateString() },
                    { "time", date.ToShortTimeString() },
                    { "seed", task.GenerationParameter.Seed.ToString() ?? string.Empty },
                    { "prompt", task.Prompt },
                    { "replace", task.CurrentReplace },
                };

                string replacePlaceHolders(string text)
                {
                    return Regex.Replace(
                        text,
                        @"\{(?<name>.*?)\}",
                        match => placeholders.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Groups["name"].Value);
                }

                for (int index = 0; index < split.Length; index++)
                {
                    string dir = split[index];

                    if (Path.IsPathRooted(dir))
                        continue;

                    split[index] = Util.GetValidDirectoryName(replacePlaceHolders(dir));
                }

                path = Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, split));
                Directory.CreateDirectory(path);

                string fileName = Util.ReplaceInvalidFileNameChars(replacePlaceHolders(task.OutputFilename).TrimEnd());

                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = replacePlaceHolders(GenerationConfig.DEFAULT_OUTPUT_FILE_NAME);

                fileName = Util.GetValidFileName(Path.Combine(path, fileName[..Math.Min(fileName.Length, 128)] + ".png"));

                await using var file = File.OpenWrite(fileName);

                foreach (var entry in zip.Entries)
                {
                    await using var s = entry.Open();
                    await s.CopyToAsync(file);
                    file.Close();
                }

                if (task.SaveJpeg)
                {
                    using var image = SKImage.FromEncodedData(fileName);
                    using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
                    using var outputStream = File.OpenWrite(Path.ChangeExtension(fileName, "jpg"));
                    data.SaveTo(outputStream);
                }
            }
            if (resp?.Content.Headers.ContentType?.MediaType == "application/json")
            {
                string content = await resp.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<NovelAIGenerationResponse>(content, NovelAIApi.CamelCaseJsonSerializerOptions);
                writeLogLine($"Error: {response?.StatusCode} {response?.Message}");
            }

            token.ThrowIfCancellationRequested();

            if (!success && retry < maxRetries && (Util.CalculateCost(task, api.SubscriptionInfo) == 0 || task.RetryAll))
            {
                writeLogLine($"Failed, Retrying {++retry} / {maxRetries}");
                Thread.Sleep(retry >= 3 || resp?.StatusCode == HttpStatusCode.TooManyRequests ? 5000 : 3000);
                continue;
            }

            retry = 0;
            Dispatcher.UIThread.Invoke(() => ProgressBar.Value = (i + 1.0) / tasks.Count * 100);
            i++;
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
            GenerationConfig? generationConfig = null;

            switch (Path.GetExtension(file.Name))
            {
                case ".json":
                    generationConfig = JsonSerializer.Deserialize<GenerationConfig>(await File.ReadAllTextAsync(path));
                    break;

                case ".png":
                    generationConfig = PngMetadataReader.FromFile(path);
                    break;

                case ".csv":
                    using (var reader = File.OpenText(path))
                    using (var csv = new CsvReader(reader, csvConfiguration))
                    {
                        var records = csv.GetRecords<TextReplacement>().ToList();
                        ReplacementDataGrid.ItemsSource = records;
                        replacements = records.ToDictionary(r => r.Text, r => r.Replace);
                    }
                    break;
            }

            if (generationConfig != null)
            {
                addTab(file.Name[..Math.Min(32, file.Name.Length)].Trim(), generationConfig);
            }
        }
        catch (Exception exception)
        {
            writeLogLine(exception.Message);
        }
    }

    private async void SaveAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TabControl.Items.Count == 0)
            return;

        var file = await GetTopLevel(this)?.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (file.Count == 0)
            return;

        foreach (var control in getGenerationParameterControls())
        {
            control.SaveConfig(Util.GetValidFileName(Path.Combine(file[0].Path.LocalPath, (Path.ChangeExtension(control.Name, string.Empty) ?? "Untitled") + ".json")));
        }
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs? e)
    {
        await updateAccountInfo(true);
    }

    private async Task updateAccountInfo(bool tokenChanged = false)
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            var subscriptionInfo = tokenChanged ? await api.UpdateToken(config.AccessToken): await api.GetSubscription();

            if (subscriptionInfo != null)
            {
                AccountInfo.Text = subscriptionInfo.ToString();
                AccountInfo.Foreground = new SolidColorBrush(subscriptionInfo.Active ? Colors.Black : Colors.Red);
                AccountAnlasDisplay.Value = subscriptionInfo.TotalTrainingStepsLeft;
            }
        }
        catch
        {
        }
    }

    private void HelpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://docs.qq.com/doc/DVkhyZk5tUmNhZVd1",
            UseShellExecute = true
        });
    }
}
