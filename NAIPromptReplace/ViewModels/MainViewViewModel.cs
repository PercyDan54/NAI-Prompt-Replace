using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using NAIPromptReplace.Models;
using NAIPromptReplace.Views;
using ReactiveUI;
using SkiaSharp;

namespace NAIPromptReplace.ViewModels;

public class MainViewViewModel : ReactiveObject
{
    protected const string HELP_URL = "https://docs.qq.com/doc/DVkhyZk5tUmNhZVd1";

    private string showTokenButtonText = "Show";
    private string logText = string.Empty;
    private bool showToken;
    private string runButtonText = "Run";

#if DEBUG
    private readonly Random random = new Random(1337);
#else
    private readonly Random random = new Random();
#endif
    private Dictionary<string, string> replacements { get; set; } = [];
    private readonly NovelAIApi api = new NovelAIApi();

    private CancellationTokenSource? cancellationTokenSource;
    private readonly IStorageProvider? storageProvider;
    private SubscriptionInfo? subscriptionInfo;
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
    public Config Config { get; set; } = new Config();

    public ObservableCollection<TabItem> TabItems { get; set; } = new ObservableCollection<TabItem>();
    public int SelectedTabIndex { get; set; }

    public bool ShowToken
    {
        get => showToken;
        set => this.RaiseAndSetIfChanged(ref showToken, value);
    }

    public string ShowTokenButtonText
    {
        get => showTokenButtonText;
        set => this.RaiseAndSetIfChanged(ref showTokenButtonText, value);
    }

    public string RunButtonText
    {
        get => runButtonText;
        set => this.RaiseAndSetIfChanged(ref runButtonText, value);
    }

    public SubscriptionInfo? SubscriptionInfo
    {
        get => subscriptionInfo;
        set => this.RaiseAndSetIfChanged(ref subscriptionInfo, value);
    }

    public string LogText
    {
        get => logText;
        set => this.RaiseAndSetIfChanged(ref logText, value);
    }

    public ICommand ToggleShowTokenCommand { get; set; }
    public ICommand UpdateTokenCommand { get; set; }
    public ICommand OpenHelpCommand { get; set; }
    public ICommand NewTabCommand { get; set; }
    public ICommand CloseTabCommand { get; set; }
    public ICommand SaveAllCommand { get; set; }
    public ICommand OpenFileCommand { get; set; }
    public ICommand RunTasksCommand { get; set; }

    public MainViewViewModel() : this(null)
    {
    }

    public MainViewViewModel(IStorageProvider? storageProvider)
    {
        this.storageProvider = storageProvider;

        ToggleShowTokenCommand = ReactiveCommand.Create(() =>
        {
            ShowToken = !ShowToken;
            ShowTokenButtonText = ShowToken ? "Hide" : "Show";
        });

        OpenHelpCommand = ReactiveCommand.Create(OpenHelp);
        UpdateTokenCommand = ReactiveCommand.Create(async () => await updateAccountInfo(true));
        UpdateTokenCommand.Execute(null);
        NewTabCommand = ReactiveCommand.Create(() => AddTab("New config", new GenerationConfig()));
        CloseTabCommand = ReactiveCommand.Create(closeTab);
        OpenFileCommand = ReactiveCommand.Create(OpenFile);
    }

    protected virtual void OpenHelp() => PresentUri(HELP_URL);

    private async void OpenFile()
    {
        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(filePickerOptions);

        foreach (var file in files)
        {
            await OpenFile(file);
        }
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
                        //ReplacementDataGrid.ItemsSource = records;
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

    private void writeLogLine(object content)
    {
        writeLog(content + Environment.NewLine);
    }

    private void writeLog(object content)
    {
        logText += content.ToString();
    }

    private IEnumerable<GenerationParameterControl> getGenerationParameterControls()
    {
        foreach (var item in TabItems)
        {
            if (item.Content is GenerationParameterControl parameterControl)
            {
                yield return parameterControl;
            }
        }
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
        TabItems.Add(new TabItem { Header = header, Content = control });
        return control;
    }

    private void closeTab()
    {
        if (TabItems.Count <= 0)
            return;

        /*if (TabControl.Items[TabControl.SelectedIndex] is GenerationParameterControl control)
            {
                control.AnlasChanged -= updateTotalAnlas;
            }*/

        TabItems.RemoveAt(SelectedTabIndex);
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
        //Dispatcher.UIThread.Invoke(() => ProgressBar.Maximum = tasks.Count);

        while (i < tasks.Count)
        {
            var task = tasks[i];
            token.ThrowIfCancellationRequested();
            //writeLog($"Running task {i + 1} / {tasks.Count}: ");
            HttpResponseMessage? resp = null;

            try
            {
                resp = await api.Generate(task, task.GenerationParameter.ImageData == null ? "generate" : "img2img").WaitAsync(TimeSpan.FromMinutes(2));
            }
            catch (Exception e)
            {
                writeLogLine(e.Message);
            }

            //Dispatcher.UIThread.Invoke(() => updateAccountInfo());
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
            //Dispatcher.UIThread.Invoke(() => ProgressBar.Value = i);
        }

        runButtonText = "Run";
        cancellationTokenSource = null;
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

        return storageProvider?.TryGetFileFromPathAsync(fileName).Result;
    }

    protected static string ReplacePlaceHolders(string text, Dictionary<string,string> placeholders)
    {
        return Regex.Replace(
            text,
            @"\{(?<name>.*?)\}",
            match => placeholders.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Groups["name"].Value);
    }

    private async Task updateAccountInfo(bool tokenChanged = false)
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            SubscriptionInfo = tokenChanged ? await api.UpdateToken(Config.AccessToken): await api.GetSubscription();
        }
        catch
        {
        }
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
}