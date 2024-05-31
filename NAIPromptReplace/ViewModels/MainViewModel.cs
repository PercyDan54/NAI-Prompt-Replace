using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CsvHelper;
using CsvHelper.Configuration;
using DynamicData;
using NAIPromptReplace.Models;
using NAIPromptReplace.Views;
using ReactiveUI;
using SkiaSharp;

namespace NAIPromptReplace.ViewModels;

public class MainViewModel : ReactiveObject
{
    protected const string HELP_URL = "https://docs.qq.com/doc/DVkhyZk5tUmNhZVd1";

    private string showTokenButtonText = "Show";
    private bool showToken;
    private string runButtonText = "Run";

#if DEBUG
    private static readonly Random random = new Random(1337);
#else
    private static readonly Random random = new Random();
#endif
    private Dictionary<string, string> replacements { get; set; } = [];
    private readonly NovelAIApi api = new NovelAIApi();

    private CancellationTokenSource? cancellationTokenSource;
    private SubscriptionInfo? subscriptionInfo;
    private int currentTask;
    private int totalTasks = 1;
    private Config config = new Config();
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
    private static readonly FilePickerSaveOptions jsonFilePickerOptions = new FilePickerSaveOptions
    {
        Title = "Open File",
        FileTypeChoices =
        [
            new FilePickerFileType("JSON") { Patterns = ["*.json"] }
        ]
    };

    public Config Config
    {
        get => config;
        set
        {
            this.RaiseAndSetIfChanged(ref config, value);
            updateAccountInfo(true);
        }
    }

    public ObservableCollection<TabViewModel> TabItems { get; set; } = [];
    public ObservableCollection<PlaceholderGroupViewModel> Placeholders { get; set; } = [];
    public ObservableCollection<TextReplacement> Replacements { get; set; } = [];

    private List<GenerationParameterControlViewModel> generationControlViewModels = [];
    private List<IDisposable> subscriptions = [];
    private int totalCost;
    private int selectedTabIndex = -1;
    private GenerationParameterControl? selectedContent;

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref selectedTabIndex, value);
    }

    public GenerationParameterControl? SelectedContent
    {
        get => selectedContent;
        set => this.RaiseAndSetIfChanged(ref selectedContent, value);
    }

    public int CurrentTask
    {
        get => currentTask;
        set => this.RaiseAndSetIfChanged(ref currentTask, value);
    }

    public int TotalTasks
    {
        get => totalTasks;
        set => this.RaiseAndSetIfChanged(ref totalTasks, value);
    }

    public int TotalCost
    {
        get => totalCost;
        set => this.RaiseAndSetIfChanged(ref totalCost, value);
    }

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

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ICommand ToggleShowTokenCommand { get; }
    public ICommand UpdateTokenCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand? SaveAllCommand { get; protected set; }
    public ICommand OpenFileCommand { get; }
    public ICommand RunTasksCommand { get; }
    public ICommand AddPlaceholderCommand { get; }
    public ICommand RemovePlaceholderCommand { get; }
    public ICommand SavePlaceholderCommand { get; }

    public MainViewModel()
    {
        ToggleShowTokenCommand = ReactiveCommand.Create(() =>
        {
            ShowToken = !ShowToken;
            ShowTokenButtonText = ShowToken ? "Hide" : "Show";
        });

        OpenHelpCommand = ReactiveCommand.Create(OpenHelp);
        UpdateTokenCommand = ReactiveCommand.Create(async () => await updateAccountInfo(true));
        NewTabCommand = ReactiveCommand.Create(() => addTab("New Config", new GenerationConfig()));
        CloseTabCommand = ReactiveCommand.Create(closeTab);
        OpenFileCommand = ReactiveCommand.Create(openFile);
        SaveAllCommand = ReactiveCommand.CreateFromTask(saveAll);
        RunTasksCommand = ReactiveCommand.Create(runTasks);
        RemovePlaceholderCommand = ReactiveCommand.Create<PlaceholderGroupViewModel>(v => Placeholders.Remove(v));
        AddPlaceholderCommand = ReactiveCommand.Create(addPlaceholder);
        SavePlaceholderCommand = ReactiveCommand.Create(savePlaceholder);
        this.WhenAnyValue(vm => vm.SelectedTabIndex).Subscribe(_ => updateTab());
    }

    private async Task savePlaceholder()
    {
        if (App.StorageProvider == null)
            return;

        var file = await App.StorageProvider.SaveFilePickerAsync(jsonFilePickerOptions);

        if (file != null)
        {
            var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, Placeholders.Select(p => p.Group), GenerationConfig.SerializerOptions);
        }
    }

    private async Task saveAll()
    {
        if (App.StorageProvider == null)
            return;

        var folder = await App.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());

        if (folder.Count == 0)
            return;

        string? path = folder[0].TryGetLocalPath();

        if (path == null)
            return;

        foreach (var vm in generationControlViewModels)
        {
            string fileName = Util.GetValidFileName(Path.Combine(path, Path.ChangeExtension(vm.Name, ".json")) ?? "Untitled");
            using var file = await folder[0].CreateFileAsync(Path.GetFileName(fileName));

            if (file == null)
                continue;

            await vm.GenerationConfig.SaveAsync(file);
        }
    }

    protected void OpenHelp() => PresentUri(HELP_URL);

    private async void openFile()
    {
        if (App.StorageProvider == null)
            return;

        var files = await App.StorageProvider.OpenFilePickerAsync(filePickerOptions);

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
            bool isImage = false;

            switch (Path.GetExtension(file.Name))
            {
                case ".json":
                    var jsonDocument = await JsonDocument.ParseAsync(stream);

                    if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
                        generationConfig = jsonDocument.Deserialize<GenerationConfig>(GenerationConfig.SerializerOptions);
                    else if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var placeholders = jsonDocument.Deserialize<PlaceholderGroup[]>(GenerationConfig.SerializerOptions);;

                        if (placeholders != null)
                        {
                            foreach (var placeholderGroup in placeholders)
                            {
                                var vm = addPlaceholder();
                                vm.Group = placeholderGroup;
                            }
                        }
                    }
                    break;

                case ".png":
                    generationConfig = PngMetadataReader.ReadFile(file);
                    isImage = true;
                    break;

                case ".csv":
                    using (var csv = new CsvReader(reader, csvConfiguration))
                    {
                        var records = csv.GetRecords<TextReplacement>().ToList();
                        Replacements.Clear();
                        Replacements.AddRange(records);
                        replacements = records.ToDictionary(r => r.Text, r => r.Replace);

                        foreach (var g in generationControlViewModels)
                        {
                            g.GenerationConfig.Replacements = replacements;
                        }
                    }
                    break;
            }

            if (generationConfig != null)
            {
                var vm = addTab(file.Name.Trim(), generationConfig);

                if (isImage)
                {
                    stream.Position = 0;
                    vm.Images.Add(new Bitmap(stream));
                }
            }
        }
        catch (Exception e)
        {
            writeWarning($"Error opening file: {e.Message}");
        }
    }

    private void clearLog() => LogEntries.Clear();
    
    private LogEntry writeLog(object content, LogEventLevel logLevel = LogEventLevel.Information)
    {
        var entry = new LogEntry
        {
            Text = content.ToString(),
            LogLevel = logLevel
        };
        LogEntries.Add(entry);

        return entry;
    }

    private LogEntry writeWarning(object content) => writeLog(content, LogEventLevel.Warning);

    private LogEntry writeError(object content) => writeLog(content, LogEventLevel.Error);

    private GenerationParameterControlViewModel addTab(string header, GenerationConfig generationConfig)
    {
        var vm = CreateGenerationParameterControlViewModel(header, generationConfig);

        var control = new GenerationParameterControl
        {
            Name = header,
            DataContext = vm,
        };

        TabItems.Add(new TabViewModel { Name = header, Control = control });
        generationControlViewModels.Add(vm);
        var subscription = vm.WhenAny(v => v.AnlasCost, (i) => i).Subscribe(i => updateTotalCost());
        subscriptions.Add(subscription);

        if (SelectedTabIndex == -1)
            SelectedTabIndex = 0;

        return vm;
    }

    private PlaceholderGroupViewModel addPlaceholder()
    {
        var vm = new PlaceholderGroupViewModel
        {
            RemoveCommand = RemovePlaceholderCommand
        };
        Placeholders.Add(vm);
        return vm;
    }

    private void updateTab()
    {
        if (SelectedTabIndex == -1 || TabItems.Count == 0)
        {
            SelectedContent = null;
            return;
        }

        SelectedContent = TabItems[SelectedTabIndex].Control;
    }

    protected virtual GenerationParameterControlViewModel CreateGenerationParameterControlViewModel(string header, GenerationConfig generationConfig)
    {
        generationConfig.Replacements = replacements;
        var vm = new GenerationParameterControlViewModel
        {
            Name = header,
            GenerationConfig = generationConfig,
            Api = api,
            OpenOutputFolderCommand = ReactiveCommand.Create(() =>
            {
                string path = string.IsNullOrEmpty(generationConfig.OutputPath) ? Environment.CurrentDirectory : generationConfig.OutputPath;
                PresentUri(path);
            })
        };

        return vm;
    }

    private void updateTotalCost()
    {
        TotalCost = generationControlViewModels.Sum(vm => vm.AnlasCost);
    }

    private void closeTab()
    {
        if (TabItems.Count == 0 || SelectedTabIndex < 0)
            return;

        int index = SelectedTabIndex;
        generationControlViewModels.RemoveAt(index);
        subscriptions[index].Dispose();
        subscriptions.RemoveAt(index);
        TabItems.RemoveAt(index);
        updateTotalCost();

        if (TabItems.Count > 0)
        {
            int newIndex = index - 1;

            if (newIndex < 0)
            {
                if (TabItems.Count >= index)
                    newIndex = index;
            }
            
            SelectedTabIndex = newIndex;
        }
    }

    private void runTasks()
    {
        if (cancellationTokenSource != null)
        {
            RunButtonText = "Run";
            cancellationTokenSource.Cancel();
            CurrentTask = 0;
            cancellationTokenSource = null;
        }
        else
        {
            RunButtonText = "Cancel";
            cancellationTokenSource = new CancellationTokenSource();
            CurrentTask = 0;
            clearLog();
            Task.Factory.StartNew(_ => createAndRunTasks(generationControlViewModels, cancellationTokenSource.Token).ContinueWith(task =>
            {
                if (task.Exception?.InnerException != null)
                {
                    writeError($"Tasks Failed with an exception, please report this: {task.Exception.InnerException}");
                }
            }), null, TaskCreationOptions.LongRunning);
        }
    }

    private async Task createAndRunTasks(List<GenerationParameterControlViewModel> viewModels, CancellationToken token)
    {
        var tasks = new List<(GenerationParameterControlViewModel, GenerationConfig)>();
        var placeholderGroups = Placeholders.Select(s => s.Group).ToArray();

        foreach (var vm in viewModels)
        {
            var g = vm.GenerationConfig.Clone(true);
            g.GenerationParameter.Seed ??= random.Next();
            long seed = g.GenerationParameter.Seed.Value;

            bool smea = g.GenerationParameter.Smea;
            g.GenerationParameter.Sampler ??= g.Model.Samplers[0];
            smea &= g.GenerationParameter.Sampler.AllowSmea;
            g.GenerationParameter.Smea = smea;
            g.GenerationParameter.Dyn &= smea;

            for (int j = 0; j < g.GenerationParameter.ReferenceImageData.Length; j++)
            {
                g.GenerationParameter.ReferenceImageMultiple[j] = Convert.ToBase64String(g.GenerationParameter.ReferenceImageData[j]);
            }

            byte[]? imageData = g.GenerationParameter.ImageData;

            if (imageData != null)
            {
                try
                {
                    using var im = SKBitmap.Decode(imageData);
                    using var resized = im.Resize(new SKSizeI(g.GenerationParameter.Width, g.GenerationParameter.Height), SKFilterQuality.High);
                    using var data = resized.Encode(SKEncodedImageFormat.Png, 100);
                    g.GenerationParameter.Image = Convert.ToBase64String(data.ToArray());
                    g.GenerationParameter.ExtraNoiseSeed = seed;
                }
                catch (Exception e)
                {
                    writeError($"Unable to load Img2Img image: {e.Message}");
                    g.GenerationParameter.Strength = g.GenerationParameter.Noise = null;
                }
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
                while (await csv.ReadAsync())
                {
                    string[] records = csv.Record;
                    string toReplace = records[0];
                    int index = prompt.IndexOf(toReplace, StringComparison.Ordinal);
                    int end = index + toReplace.Length;

                    // Ensure the matched tag is a full word split by comma
                    if (index >= 0 && (index == 0 || end == prompt.Length || Regex.IsMatch(prompt, $@",(?:\{{|\[)*{Regex.Escape(toReplace)}(?:\}}|\])*,")))
                    {
                        replaceLines.Add(records);
                    }
                }
            }

            var usedPlaceholders = placeholderGroups.Where(p => tags.Contains(p.Keyword)).ToArray();

            if (replaceLines.Count > 0 && replaceLines[0].Length > 1)
            {
                var combos = Util.GetAllPossibleCombos(replaceLines);
                var toReplaces = replaceLines.Select(l => l[0]).ToList();

                for (int j = 0; j < g.BatchSize; j++)
                {
                    long batchSeed = g.AllRandom && j > 0 ? random.Next() : seed + j;
                    var placeholders = getPlaceholders(usedPlaceholders);

                    foreach (var combo in combos)
                    {
                        var clone = g.Clone();
                        clone.GenerationParameter.Seed = batchSeed;

                        for (int k = 0; k < combo.Count; k++)
                        {
                            clone.Prompt = clone.Prompt.Replace(toReplaces[k], combo[k]);
                        }

                        clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, placeholders);
                        clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements);
                        clone.CurrentReplace = string.Join(',', combo);
                        tasks.Add((vm, clone));
                    }
                }
            }
            else
            {
                for (int j = 0; j < g.BatchSize; j++)
                {
                    var clone = g.Clone();
                    var placeholders = getPlaceholders(usedPlaceholders);

                    clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + j;
                    clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, placeholders);
                    clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements);
                    clone.CurrentReplace = clone.Prompt;
                    tasks.Add((vm, clone));
                }
            }

            vm.Images.Clear();
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 10;
        var date = DateTime.Now;
        TotalTasks = tasks.Count;

        while (i < tasks.Count)
        {
            var task = tasks[i];
            var generationConfig = task.Item2;
            token.ThrowIfCancellationRequested();
            var progressLog = writeLog($"Running task {i + 1} / {tasks.Count}: ");
            HttpResponseMessage? resp = null;

            void writeErrorResult(string content)
            {
                progressLog.Text += content;
                progressLog.LogLevel = LogEventLevel.Error;
            }

            try
            {
                resp = await api.Generate(generationConfig, generationConfig.GenerationParameter.ImageData == null ? "generate" : "img2img").WaitAsync(TimeSpan.FromMinutes(2));
                updateAccountInfo();
            }
            catch (Exception e)
            {
                writeErrorResult($"Error: {e.Message}");
            }

            bool success = resp?.IsSuccessStatusCode ?? false;

            if (success)
            {
                progressLog.Text += $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "date", date.ToShortDateString() },
                    { "time", date.ToShortTimeString() },
                    { "seed", generationConfig.GenerationParameter.Seed.ToString() ?? string.Empty },
                    { "prompt", generationConfig.Prompt },
                    { "replace", generationConfig.CurrentReplace },
                };

                var storageFile = GetOutputFileForTask(generationConfig, placeholders);
                using var file = await storageFile.OpenWriteAsync();

                foreach (var entry in zip.Entries)
                {
                    await using var s = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await s.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(file);
                    memoryStream.Position = 0;
                    task.Item1.Images.Add(new Bitmap(memoryStream));

                    if (generationConfig.SaveJpeg)
                    {
                        memoryStream.Position = 0;
                        using var image = Util.RemoveImageAlpha(memoryStream);
                        var folder = generationConfig.StorageFolder ?? await storageFile.GetParentAsync();

                        if (folder != null)
                        {
                            string name = Path.GetFileNameWithoutExtension(storageFile.Name) + "_copy";
                            string extension = Path.GetExtension(storageFile.Name);
                            using var copyFile = await folder.CreateFileAsync(Util.GetValidFileName(Path.ChangeExtension(name, extension)));

                            if (copyFile != null)
                            {
                                using var outputStream = await copyFile.OpenWriteAsync();
                                image.Encode(outputStream, SKEncodedImageFormat.Png, 100);
                            }
                        }
                    }
                }
            }
            else if (resp != null)
            {
                string message = resp.StatusCode.ToString();
                int statusCode = (int)resp.StatusCode;

                if (resp.Content.Headers.ContentType?.MediaType is "application/json" or "text/plain")
                {
                    message = await resp.Content.ReadAsStringAsync();

                    try
                    {
                        var response = JsonSerializer.Deserialize<NovelAIGenerationResponse>(message, NovelAIApi.CamelCaseJsonSerializerOptions);

                        if (response != null)
                        {
                            message = response.Message;
                            statusCode = response.StatusCode;
                        }
                    }
                    catch
                    {
                    }
                }

                writeErrorResult($"Error: {statusCode} {message}");
            }

            token.ThrowIfCancellationRequested();

            if (!success && retry < maxRetries && (AnlasCostCalculator.Calculate(generationConfig, api.SubscriptionInfo) == 0 || generationConfig.RetryAll))
            {
                writeWarning($"Failed, Retrying {++retry} / {maxRetries}");
                Thread.Sleep(retry >= 3 || resp?.StatusCode == HttpStatusCode.TooManyRequests ? 5000 : 3000);
                continue;
            }

            retry = 0;
            i++;
            CurrentTask = i;
        }

        RunButtonText = "Run";
        cancellationTokenSource = null;
    }

    private static Dictionary<string, string> getPlaceholders(PlaceholderGroup[] placeholderGroups)
    {
        var dict = new Dictionary<string, string>();

        foreach (var placeholder in placeholderGroups)
        {
            List<string> csvParsed = [];
            using var stringReader = new StringReader(placeholder.Text);
            using (var csv = new CsvParser(stringReader, csvConfiguration))
            {
                while (csv.Read())
                {
                    csvParsed.AddRange(csv.Record);
                }
            }

            if (csvParsed.Count == 0)
                continue;

            string[] placeholderTags = csvParsed.ToArray();

            if (placeholder.Shuffled)
                random.Shuffle(placeholderTags);

            string[] chosen = [];

            switch (placeholder.SelectionMethod)
            {
                case SelectionMethod.All:
                    chosen = placeholderTags;
                    break;
                case SelectionMethod.SingleSequential:
                    chosen = [placeholderTags[placeholder.SingleSequentialNum]];
                    placeholder.SingleSequentialNum = (placeholder.SingleSequentialNum + 1) % placeholderTags.Length;
                    break;
                case SelectionMethod.MultipleNum:
                    chosen = random.GetItems(placeholderTags, placeholder.MultipleNum);
                    break;
                case SelectionMethod.MultipleProb:
                    chosen = placeholderTags.Where(_ => random.NextDouble() < placeholder.MultipleProb).ToArray();
                    break;
            }

            int randomBrackets = placeholder.RandomBrackets;

            if (randomBrackets >= 0)
            {
                int randomBracketsMax = Math.Max(randomBrackets, placeholder.RandomBracketsMax);

                for (int k = 0; k < chosen.Length; k++)
                {
                    bool addWeight = random.NextDouble() < 0.5;
                    char bracketStartChar = addWeight ? '{' : '[';
                    char bracketEndChar = addWeight ? '}' : ']';
                    int bracketCount = randomBrackets == randomBracketsMax ? randomBrackets : random.Next(randomBrackets, randomBracketsMax);
                    string bracketStart = new string(bracketStartChar, bracketCount);
                    string bracketEnd = new string(bracketEndChar, bracketCount);
                    chosen[k] = $"{bracketStart}{chosen[k]}{bracketEnd}";
                }
            }

            dict.TryAdd(placeholder.Keyword, string.Join(',', chosen));
        }

        return dict;
    }

    protected virtual IStorageFile? GetOutputFileForTask(GenerationConfig task, Dictionary<string,string> placeholders)
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

        return App.StorageProvider?.TryGetFileFromPathAsync(fileName).Result;
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
            Config.AccessToken = api.AccessToken;
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
