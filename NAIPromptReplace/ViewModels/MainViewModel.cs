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
    public ObservableCollection<WildcardViewModel> Wildcards { get; set; } = [];
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
    public ICommand AddWildcardCommand { get; }
    public ICommand RemoveWildcardCommand { get; }
    public ICommand SaveWildcardCommand { get; }

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
        RemoveWildcardCommand = ReactiveCommand.Create<WildcardViewModel>(v => Wildcards.Remove(v));
        AddWildcardCommand = ReactiveCommand.Create(addWildcard);
        SaveWildcardCommand = ReactiveCommand.Create(saveWildcard);
        this.WhenAnyValue(vm => vm.SelectedTabIndex).Subscribe(_ => updateTab());
    }

    private async Task saveWildcard()
    {
        if (App.StorageProvider == null)
            return;

        var file = await App.StorageProvider.SaveFilePickerAsync(jsonFilePickerOptions);

        if (file != null)
        {
            var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, Wildcards.Select(p => p.Wildcard), GenerationConfig.SerializerOptions);
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
                        var wildcards = jsonDocument.Deserialize<Wildcard[]>(GenerationConfig.SerializerOptions);;

                        if (wildcards != null)
                        {
                            foreach (var wildcard in wildcards)
                            {
                                var vm = addWildcard();
                                vm.Wildcard = wildcard;
                            }
                        }
                    }
                    break;

                case ".png":
                    generationConfig = PngMetadataReader.ReadFile(stream);
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
                    var image = new Bitmap(stream);
                    vm.GenerationLogs.Add(new GenerationLogViewModel
                    {
                        GenerationLog = new GenerationLog { File = file, Image = image, Thumbnail = image, Text = generationConfig.Prompt }
                    });
                }
            }
        }
        catch (Exception e)
        {
            writeWarning($"Error opening file {file.Name}: {e.Message}");
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
        SelectedTabIndex = TabItems.Count - 1;

        return vm;
    }

    private WildcardViewModel addWildcard()
    {
        var vm = new WildcardViewModel
        {
            RemoveCommand = RemoveWildcardCommand
        };
        Wildcards.Add(vm);
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
            GC.Collect();
            Task.Factory.StartNew(_ => createAndRunTasks(generationControlViewModels, cancellationTokenSource.Token).ContinueWith(task =>
            {
                if (task.Exception?.InnerException != null)
                {
                    writeError($"Tasks Failed with an exception, please report this: {task.Exception.InnerException}");
                }

                GC.Collect();
            }), null, TaskCreationOptions.LongRunning);
        }
    }

    private async Task createAndRunTasks(List<GenerationParameterControlViewModel> viewModels, CancellationToken token)
    {
        var tasks = new List<GenerationTask>();
        var wildcards = Wildcards.Select(s => s.Wildcard).ToArray();

        foreach (var vm in viewModels)
        {
            var g = vm.GenerationConfig.Clone(true);
            g.GenerationParameter.Seed ??= random.Next();
            long seed = g.GenerationParameter.Seed.Value;

            bool smea = g.GenerationParameter.Smea ?? false;
            g.GenerationParameter.Sampler ??= g.Model.Samplers[0];
            smea &= g.GenerationParameter.Sampler.AllowSmea;
            g.GenerationParameter.Smea = smea;
            g.GenerationParameter.Dyn &= smea;

            if (g.Model.Group == ModelGroup.V4)
            {
                g.GenerationParameter.Smea = g.GenerationParameter.Dyn = null;
            }

            bool preferBrownianFlag = g.GenerationParameter.Sampler == SamplerInfo.EulerAncestral && g.GenerationParameter.NoiseSchedule != "native";
            g.GenerationParameter.DeliberateEulerAncestralBug ??= !preferBrownianFlag;
            g.GenerationParameter.PreferBrownian ??= preferBrownianFlag;

            if (g.VarietyAdd && !g.GenerationParameter.SkipCfgAboveSigma.HasValue)
            {
                int c1 = (int)MathF.Floor(g.GenerationParameter.Width / 8f);
                int c2 = (int)MathF.Floor(g.GenerationParameter.Height / 8f);
                float v = MathF.Pow((4 * c1 * c2) / 63232f, 0.5f);
                g.GenerationParameter.SkipCfgAboveSigma = 19 * v;
            }

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
            string[] tags = g.Prompt.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string prompt = g.Prompt = string.Join(',', tags);
            string promptTrimmedFirstBrackets = prompt.TrimStart('{').TrimStart('[');
            g.Replace = string.Join(',', g.Replace.Split(',', StringSplitOptions.TrimEntries));
            List<string[]> replaceLines = [];

            bool containsTag(string tag)
            {
                int index = promptTrimmedFirstBrackets.IndexOf(tag, StringComparison.Ordinal);
                int end = index + tag.Length;

                // Ensure the matched tag is a full word split by comma
                return index >= 0 && (index == 0 || end == promptTrimmedFirstBrackets.Length ||
                       Regex.IsMatch(prompt, $@",(?:\{{|\[)*{Regex.Escape(tag)}(?:\}}|\])*,"));
            }

            try
            {
                using var reader = new StringReader(g.Replace);
                using (var csv = new CsvParser(reader, csvConfiguration))
                {
                    while (await csv.ReadAsync())
                    {
                        string[] records = csv.Record;
                        string toReplace = records[0];

                        // Ensure the matched tag is a full word split by comma
                        if (containsTag(toReplace))
                        {
                            replaceLines.Add(records);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                writeError($"Failed to process replace: {e}");
                // Cancel the tasks
                runTasks();
                return;
            }

            var usedWildcards = wildcards.Where(p => containsTag(p.Keyword)).ToArray();

            if (replaceLines.Count > 0 && replaceLines[0].Length > 1)
            {
                var combos = Util.GetAllPossibleCombos(replaceLines);
                var toReplaces = replaceLines.Select(l => l[0]).ToList();

                for (int j = 0; j < g.BatchSize; j++)
                {
                    long batchSeed = g.AllRandom && j > 0 ? random.Next() : seed + (g.FixedSeed ? 0 : j);
                    var wildcardsDict = getWildcards(usedWildcards);

                    foreach (var combo in combos)
                    {
                        var clone = g.Clone();
                        clone.GenerationParameter.Seed = batchSeed;

                        for (int k = 0; k < combo.Count; k++)
                        {
                            string toReplace = toReplaces[k];
                            int index = clone.Prompt.IndexOf(toReplace, StringComparison.Ordinal);

                            if (index < 0)
                                continue;

                            int length = toReplace.Length;
                            clone.Prompt = clone.Prompt[..index] + combo[k] + clone.Prompt[(index + length)..];
                        }

                        clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, wildcardsDict);
                        clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements).Replace(",", ", ");
                        clone.CurrentReplace = string.Join(',', combo);

                        if (g.Model.Group == ModelGroup.V4)
                        {
                            clone.GenerationParameter.CharacterPrompts = [];
                            clone.GenerationParameter.V4Prompt = new V4Prompt
                            {
                                Caption = new V4Caption
                                {
                                    BaseCaption = clone.Prompt,
                                    UseOrder = true,
                                    UseCoords = false,
                                }
                            };
                            clone.GenerationParameter.V4NegativePrompt = new V4Prompt
                            {
                                Caption =
                                {
                                    BaseCaption = clone.GenerationParameter.NegativePrompt
                                }
                            };
                        }

                        tasks.Add(new GenerationTask(clone, vm) { Wildcards = wildcardsDict });
                    }
                }
            }
            else
            {
                for (int j = 0; j < g.BatchSize; j++)
                {
                    var clone = g.Clone();
                    var wildcardsDict = getWildcards(usedWildcards);

                    clone.GenerationParameter.Seed = g.AllRandom && j > 0 ? random.Next() : seed + (g.FixedSeed ? 0 : j);
                    clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, wildcardsDict);
                    clone.Prompt = GenerationConfig.GetReplacedPrompt(clone.Prompt, clone.Replacements).Replace(",", ", ");
                    clone.CurrentReplace = clone.Prompt;

                    if (g.Model.Group == ModelGroup.V4)
                    {
                        clone.GenerationParameter.CharacterPrompts = [];
                        clone.GenerationParameter.V4Prompt = new V4Prompt
                        {
                            Caption = new V4Caption
                            {
                                BaseCaption = clone.Prompt,
                                UseOrder = true,
                                UseCoords = false,
                            }
                        };
                        clone.GenerationParameter.V4NegativePrompt = new V4Prompt
                        {
                            Caption =
                            {
                                BaseCaption = clone.GenerationParameter.NegativePrompt
                            }
                        };
                    }

                    tasks.Add(new GenerationTask(clone, vm) { Wildcards = wildcardsDict });
                }
            }

            vm.GenerationLogs.Clear();
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 10;
        var date = DateTime.Now;
        TotalTasks = tasks.Count;

        while (i < tasks.Count)
        {
            var task = tasks[i];
            var generationConfig = task.GenerationConfig;
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

                foreach (var wildcard in task.Wildcards)
                {
                    placeholders.TryAdd(wildcard.Key, wildcard.Value);
                }

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

                    string fileName = storageFile.Name;
                    var logImage = new Bitmap(memoryStream);
                    var thumbnail = Util.ResizeBitmap(logImage, maxHeight: 250);
                    task.ViewModel.GenerationLogs.Add(new GenerationLogViewModel
                    {
                        DeleteImageCommand = ReactiveCommand.CreateFromTask<GenerationLogViewModel>(async log =>
                        {
                            if (log.GenerationLog.File != null)
                            {
                                try
                                {
                                    await log.GenerationLog.File.DeleteAsync();
                                }
                                catch
                                {
                                }
                            }

                            task.ViewModel.GenerationLogs.Remove(log);
                        }),
                        GenerationLog = new GenerationLog
                        {
                            File = storageFile,
                            Image = logImage,
                            Thumbnail = thumbnail,
                            Text = $@"{fileName.Replace(",", ", ")}

Wildcards:
{string.Join(Environment.NewLine, task.Wildcards.Select(kvp => $"  - {kvp.Key}: {kvp.Value}"))}

Prompt: {task.GenerationConfig.Prompt}"
                        }
                    });

                    if (generationConfig.SaveJpeg)
                    {
                        memoryStream.Position = 0;
                        using var image = Util.RemoveImageAlpha(memoryStream);
                        var folder = generationConfig.StorageFolder ?? await storageFile.GetParentAsync();

                        if (folder != null)
                        {
                            string name = Path.GetFileNameWithoutExtension(fileName) + "_copy";
                            string extension = Path.GetExtension(fileName);
                            using var copyFile = await folder.CreateFileAsync(Util.GetValidFileName(Path.ChangeExtension(name, extension)));

                            if (copyFile != null)
                            {
                                await using var outputStream = await copyFile.OpenWriteAsync();
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
                await Task.Delay(retry >= 3 || resp?.StatusCode == HttpStatusCode.TooManyRequests ? 5000 : 3000);
                continue;
            }

            if (i % 15 == 0 && i > 0)
            {
                writeLog("Sleeping for 5 seconds to avoid rate limit...");
                await Task.Delay(5000, token);
            }

            retry = 0;
            i++;
            CurrentTask = i;
        }

        RunButtonText = "Run";
        cancellationTokenSource = null;
    }

    private static Dictionary<string, string> getWildcards(Wildcard[] wildcards)
    {
        var dict = new Dictionary<string, string>();

        foreach (var wildcard in wildcards)
        {
            List<string> csvParsed = [];
            using var stringReader = new StringReader(wildcard.Text);
            using (var csv = new CsvParser(stringReader, csvConfiguration))
            {
                while (csv.Read())
                {
                    csvParsed.AddRange(csv.Record);
                }
            }

            if (csvParsed.Count == 0)
                continue;

            string[] tags = csvParsed.ToArray();

            if (wildcard.Shuffled)
                random.Shuffle(tags);

            string[] chosen = [];

            switch (wildcard.SelectionMethod)
            {
                case SelectionMethod.All:
                    chosen = tags;
                    break;
                case SelectionMethod.SingleSequential:
                    chosen = [tags[wildcard.SingleSequentialNum]];
                    wildcard.SingleSequentialNum = (wildcard.SingleSequentialNum + 1) % tags.Length;
                    break;
                case SelectionMethod.MultipleNum:
                    chosen = random.GetItems(tags, wildcard.MultipleNum);
                    break;
                case SelectionMethod.MultipleProb:
                    chosen = tags.Where(_ => random.NextDouble() <= wildcard.MultipleProb).ToArray();
                    break;
            }

            int randomBrackets = wildcard.RandomBrackets;
            int randomBracketsMax = Math.Max(randomBrackets, wildcard.RandomBracketsMax);

            for (int k = 0; k < chosen.Length; k++)
            {
                int bracketCount = randomBrackets == randomBracketsMax ? randomBrackets : random.Next(randomBrackets, randomBracketsMax + 1);

                if (bracketCount == 0)
                    continue;

                bool addWeight = bracketCount > 0;
                char bracketStartChar = addWeight ? '{' : '[';
                char bracketEndChar = addWeight ? '}' : ']';
                bracketCount = Math.Abs(bracketCount);
                string bracketStart = new string(bracketStartChar, bracketCount);
                string bracketEnd = new string(bracketEndChar, bracketCount);
                chosen[k] = $"{bracketStart}{chosen[k]}{bracketEnd}";
            }

            dict.TryAdd(wildcard.Keyword, string.Join(',', chosen));
        }

        return dict;
    }

    protected virtual IStorageFile? GetOutputFileForTask(GenerationConfig task, Dictionary<string,string> placeholders)
    {
        string pathString = string.IsNullOrWhiteSpace(task.OutputPath) ? Environment.CurrentDirectory : task.OutputPath;

        string[] split = pathString.Split(Path.DirectorySeparatorChar);

        for (int i = 0; i < split.Length; i++)
        {
            string dir = split[i];

            if (Path.IsPathRooted(dir))
                continue;

            split[i] = Util.GetValidDirectoryName(ReplaceWildcards(dir, placeholders));
        }

        pathString = Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, split));
        Directory.CreateDirectory(pathString);

        string fileName = Util.ReplaceInvalidFileNameChars(ReplaceWildcards(task.OutputFilename, placeholders).TrimEnd());

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = ReplaceWildcards(GenerationConfig.DEFAULT_OUTPUT_FILE_NAME, placeholders);

        fileName = Util.GetValidFileName(Path.Combine(pathString, fileName[..Math.Min(fileName.Length, 128)] + ".png"));
        using var file1 = File.Create(fileName);

        return App.StorageProvider?.TryGetFileFromPathAsync(fileName).Result;
    }
    
    protected static string ReplaceWildcards(string text, Dictionary<string,string> wildcards)
    {
        return Regex.Replace(
            text,
            @"\{(?<name>.*?)\}",
            match => wildcards.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Groups["name"].Value);
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
