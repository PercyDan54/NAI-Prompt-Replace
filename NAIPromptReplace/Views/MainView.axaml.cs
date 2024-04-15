using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using NAIPromptReplace.Models;
using SkiaSharp;

namespace NAIPromptReplace.Views;

public partial class MainView : LayoutTransformControl
{
    protected const string CONFIG_FILE = "config.json";
    protected const string HELP_URL = "https://docs.qq.com/doc/DVkhyZk5tUmNhZVd1";

#if DEBUG
    private readonly Random random = new Random(1337);
#else
    private readonly Random random = new Random();
#endif
    private Dictionary<string, string> replacements { get; set; } = [];
    private readonly NovelAIApi api = new NovelAIApi();

    protected Config Config { get; set; } = new Config();
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

    protected IStorageProvider? StorageProvider => TopLevel.GetTopLevel(this)?.StorageProvider;

    protected virtual string ConfigPath => CONFIG_FILE;

    public MainView()
    {
        InitializeComponent();
        loadConfig();
        DataContext = Config;
    }

    private void loadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            Config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? Config;
            updateAccountInfo(true).ConfigureAwait(false);
        }
    }

    public void SaveConfig()
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            Config.AccessToken = api.AccessToken;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
        }
    }

    private void NewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AddTab("New config", new GenerationConfig());
    }

    protected virtual GenerationParameterControl AddTab(string header, GenerationConfig config)
    {
        var control = new GenerationParameterControl(config, api) { Name = header };
        control.SetReplacements(replacements);
        control.OpenOutputButton.Click += (_, _) =>
        {
            string path = string.IsNullOrEmpty(config.OutputPath) ? Environment.CurrentDirectory : config.OutputPath;
            PresentUri(path);
        };
        TabControl.Items.Add(new TabItem { Header = header, Content = control });
        control.AnlasChanged += updateTotalAnlas;
        updateTotalAnlas(null, null);

        return control;
    }

    protected virtual void PresentUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch
        {
        }
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
            await OpenFile(file);
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

            var referenceImageData = g.GenerationParameter.ReferenceImageData;

            if (referenceImageData != null)
            {
                g.GenerationParameter.ReferenceImage = Convert.ToBase64String(referenceImageData);
            }

            referenceImageData = g.GenerationParameter.ImageData;

            if (referenceImageData != null)
            {
                using var im = SKBitmap.Decode(referenceImageData);
                using var resized = im.Resize(new SKSizeI(g.GenerationParameter.Width, g.GenerationParameter.Height), SKFilterQuality.High);
                using var data = resized.Encode(SKEncodedImageFormat.Png, 100);
                g.GenerationParameter.Image = Convert.ToBase64String(data.ToArray());
                g.GenerationParameter.ExtraNoiseSeed = seed;
            }
            else
            {
                g.GenerationParameter.Strength = g.GenerationParameter.Noise = null;
            }

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

                        clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements);
                        clone.CurrentReplace = string.Join(',', combo);
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
                    clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements);
                    clone.CurrentReplace = clone.Prompt;
                    tasks.Add(clone);
                }
            }
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 5;
        var date = DateTime.Now;
        Dispatcher.UIThread.Invoke(() => ProgressBar.Maximum = tasks.Count);

        while (i < tasks.Count)
        {
            var task = tasks[i];
            token.ThrowIfCancellationRequested();
            writeLog($"Running task {i + 1} / {tasks.Count}: ");
            HttpResponseMessage? resp = null;

            try
            {
                resp = await api.Generate(task, task.GenerationParameter.ImageData == null ? "generate" : "img2img").WaitAsync(TimeSpan.FromMinutes(2));
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
                var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "date", date.ToShortDateString() },
                    { "time", date.ToShortTimeString() },
                    { "seed", task.GenerationParameter.Seed.ToString() ?? string.Empty },
                    { "prompt", task.Prompt },
                    { "replace", task.CurrentReplace },
                };

                var storageFile = GetOutputFileForTask(task, placeholders);
                using var file = await storageFile.OpenWriteAsync();

                foreach (var entry in zip.Entries)
                {
                    await using var s = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await s.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(file);

                    if (task.SaveJpeg)
                    {
                        memoryStream.Position = 0;
                        using var image = SKImage.FromEncodedData(memoryStream);
                        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
                        var folder = await storageFile.GetParentAsync();

                        if (folder != null)
                        {
                            using var jpegFile = await folder.CreateFileAsync(Path.ChangeExtension(storageFile.Name, "jpg"));
                            using var outputStream = await jpegFile.OpenWriteAsync();
                            data.SaveTo(outputStream);
                        }
                    }
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
            i++;
            Dispatcher.UIThread.Invoke(() => ProgressBar.Value = i);
        }

        Dispatcher.UIThread.Invoke(() =>
        {
            RunButton.Content = "Run";
            cancellationTokenSource = null;
        });
    }

    protected virtual IStorageFile GetOutputFileForTask(GenerationConfig task, Dictionary<string,string> placeholders)
    {
        string pathString = string.IsNullOrWhiteSpace(task.OutputPath) ? Environment.CurrentDirectory : task.OutputPath;

        string[] split = pathString.Split(Path.DirectorySeparatorChar);

        for (int index = 0; index < split.Length; index++)
        {
            string dir = split[index];

            if (Path.IsPathRooted(dir))
                continue;

            split[index] = Util.GetValidDirectoryName(ReplacePlaceHolders(dir, placeholders));
        }

        pathString = Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, split));
        Directory.CreateDirectory(pathString);

        string fileName = Util.ReplaceInvalidFileNameChars(ReplacePlaceHolders(task.OutputFilename, placeholders).TrimEnd());

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = ReplacePlaceHolders(GenerationConfig.DEFAULT_OUTPUT_FILE_NAME, placeholders);

        fileName = Util.GetValidFileName(Path.Combine(pathString, fileName[..Math.Min(fileName.Length, 128)] + ".png"));
        using var file1 = File.Create(fileName);

        return StorageProvider?.TryGetFileFromPathAsync(fileName).Result;
    }

    protected static string ReplacePlaceHolders(string text, Dictionary<string,string> placeholders)
    {
        return Regex.Replace(
            text,
            @"\{(?<name>.*?)\}",
            match => placeholders.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Groups["name"].Value);
    }

    public async Task OpenFile(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            GenerationConfig? generationConfig = null;

            switch (Path.GetExtension(file.Name))
            {
                case ".json":
                    generationConfig = JsonSerializer.Deserialize<GenerationConfig>(await reader.ReadToEndAsync());
                    break;

                case ".png":
                    generationConfig = PngMetadataReader.ReadFile(file);
                    break;

                case ".csv":
                    using (var csv = new CsvReader(reader, csvConfiguration))
                    {
                        var records = csv.GetRecords<TextReplacement>().ToList();
                        ReplacementDataGrid.ItemsSource = records;
                        replacements = records.ToDictionary(r => r.Text, r => r.Replace);

                        foreach (var control in getGenerationParameterControls())
                        {
                            control.SetReplacements(replacements);
                        }
                    }
                    break;
            }

            if (generationConfig != null)
            {
                AddTab(file.Name[..Math.Min(32, file.Name.Length)].Trim(), generationConfig);
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

        var folder = await StorageProvider?.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder.Count == 0)
            return;

        foreach (var control in getGenerationParameterControls())
        {
            var fileName = Util.GetValidFileName(Path.Combine(folder[0].TryGetLocalPath(), (Path.ChangeExtension(control.Name, string.Empty) ?? "Untitled") + ".json"));
            folder[0].CreateFileAsync(Path.GetFileName(fileName));
            control.SaveConfig(await StorageProvider?.TryGetFileFromPathAsync(fileName));
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
            var subscriptionInfo = tokenChanged ? await api.UpdateToken(Config.AccessToken): await api.GetSubscription();

            if (subscriptionInfo != null)
            {
                AccountInfo.Text = subscriptionInfo.ToString();

                if (subscriptionInfo.Active)
                {
                    AccountInfo.TryFindResource("TextControlForeground", ActualThemeVariant, out var defaultBrush);
                    AccountInfo.Foreground = defaultBrush as IBrush ?? new SolidColorBrush(Colors.Black);
                }
                else
                    AccountInfo.Foreground = new SolidColorBrush(Colors.Red);

                AccountAnlasDisplay.Value = subscriptionInfo.TotalTrainingStepsLeft;
            }
        }
        catch
        {
        }
    }

    private void HelpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PresentUri(HELP_URL);
    }
}
