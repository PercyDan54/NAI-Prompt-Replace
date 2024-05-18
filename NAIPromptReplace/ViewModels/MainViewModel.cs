using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
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

    public Config Config
    {
        get => config;
        set
        {
            this.RaiseAndSetIfChanged(ref config, value);
            updateAccountInfo(true);
        }
    }

    public ObservableCollection<TabItem> TabItems { get; set; } = [];
    private List<GenerationParameterControlViewModel> generationControlViewModels = [];
    private List<IDisposable> subscriptions = [];
    private int totalCost;

    public int SelectedTabIndex { get; set; }

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

    public ObservableCollection<TextReplacement> Replacements { get; set; } = [];

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

    public ICommand ToggleShowTokenCommand { get; }
    public ICommand UpdateTokenCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand? SaveAllCommand { get; protected set; }
    public ICommand OpenFileCommand { get; }
    public ICommand RunTasksCommand { get; }

    public MainViewModel()
    {
        ToggleShowTokenCommand = ReactiveCommand.Create(() =>
        {
            ShowToken = !ShowToken;
            ShowTokenButtonText = ShowToken ? "Hide" : "Show";
        });

        OpenHelpCommand = ReactiveCommand.Create(OpenHelp);
        UpdateTokenCommand = ReactiveCommand.Create(async () => await updateAccountInfo(true));
        NewTabCommand = ReactiveCommand.Create(() => addTab("New config", new GenerationConfig()));
        CloseTabCommand = ReactiveCommand.Create(closeTab);
        OpenFileCommand = ReactiveCommand.Create(openFile);
        SaveAllCommand = ReactiveCommand.CreateFromTask(saveAll);
        RunTasksCommand = ReactiveCommand.Create(runTasks);
    }

    private async Task saveAll()
    {
        if (App.StorageProvider == null)
            return;

        var folder = await App.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder.Count == 0)
            return;

        string? path = folder[0].TryGetLocalPath();

        if (path == null)
            return;

        foreach (var vm in generationControlViewModels)
        {
            string fileName = Util.GetValidFileName(Path.Combine(path, Path.ChangeExtension(vm.Name, ".json")) ?? "Untitled");
            using var file = await folder[0].CreateFileAsync(Path.GetFileName(fileName));
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

            switch (Path.GetExtension(file.Name))
            {
                case ".json":
                    generationConfig = JsonSerializer.Deserialize<GenerationConfig>(await reader.ReadToEndAsync(), GenerationConfig.SerializerOptions);
                    break;

                case ".png":
                    generationConfig = PngMetadataReader.ReadFile(file);
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
                addTab(file.Name[..Math.Min(32, file.Name.Length)].Trim(), generationConfig);
            }
        }
        catch (Exception exception)
        {
            writeLogLine(exception.Message);
        }
    }

    private void clearLog() => LogText = string.Empty;
    
    private void writeLogLine(object content)
    {
        writeLog(content + Environment.NewLine);
    }

    private void writeLog(object content)
    {
        LogText += content.ToString();
    }

    private void addTab(string header, GenerationConfig generationConfig)
    {
        var vm = CreateGenerationParameterControlViewModel(header, generationConfig);

        var control = new GenerationParameterControl
        {
            Name = header,
            DataContext = vm,
        };

        TabItems.Add(new TabItem { Header = header, Content = control });
        generationControlViewModels.Add(vm);
        var subscription = vm.WhenAny(v => v.AnlasCost, (i) => i).Subscribe(i => updateTotalCost());
        subscriptions.Add(subscription);
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
        if (TabItems.Count == 0)
            return;

        int index = SelectedTabIndex;
        generationControlViewModels.RemoveAt(index);
        subscriptions[index].Dispose();
        subscriptions.RemoveAt(index);
        TabItems.RemoveAt(index);
        updateTotalCost();
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
            var configs = generationControlViewModels.Select(vm => vm.GenerationConfig).ToList();
            CurrentTask = 0;
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

    private async Task createAndRunTasks(List<GenerationConfig> configs, CancellationToken token)
    {
        var tasks = new List<GenerationConfig>();

        foreach (var generationConfig in configs)
        {
            var g = generationConfig.Clone();
            g.GenerationParameter.Seed ??= random.Next();
            long seed = g.GenerationParameter.Seed.Value;

            bool smea = g.GenerationParameter.Smea;
            g.GenerationParameter.Sampler ??= g.Model.Samplers[0];
            smea &= g.GenerationParameter.Sampler.AllowSmea;
            g.GenerationParameter.Smea = smea;
            g.GenerationParameter.Dyn &= smea;

            for (int j = 0; j < g.GenerationParameter.ReferenceImageData.Length; j++)
            {
                var referenceImageData = g.GenerationParameter.ReferenceImageData[j];

                g.GenerationParameter.ReferenceImageMultiple[j] = Convert.ToBase64String(referenceImageData);
            }

            var imageData = g.GenerationParameter.ImageData;

            if (imageData != null)
            {
                using var im = SKBitmap.Decode(imageData);
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
        TotalTasks = tasks.Count;

        while (i < tasks.Count)
        {
            var task = tasks[i];
            token.ThrowIfCancellationRequested();
            writeLog($"Running task {i + 1} / {tasks.Count}: ");
            HttpResponseMessage? resp = null;

            try
            {
                resp = await api.Generate(task, task.GenerationParameter.ImageData == null ? "generate" : "img2img").WaitAsync(TimeSpan.FromMinutes(2));
                updateAccountInfo();
            }
            catch (Exception e)
            {
                writeLogLine($"Error: {e.Message}");
            }

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
                        var folder = task.StorageFolder ?? await storageFile.GetParentAsync();

                        if (folder != null)
                        {
                            using var jpegFile = await folder.CreateFileAsync(Path.ChangeExtension(storageFile.Name, "jpg"));
                            using var outputStream = await jpegFile.OpenWriteAsync();
                            data.SaveTo(outputStream);
                        }
                    }
                }
            }
            else if (resp != null)
            {
                if (resp.Content.Headers.ContentType?.MediaType is "application/json" or "text/plain")
                {
                    string message = await resp.Content.ReadAsStringAsync();
                    int statusCode = (int)resp.StatusCode;

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
                    writeLogLine($"Error: {statusCode} {message}");
                }
                else
                {
                    writeLogLine($"Error: {(int)resp.StatusCode} {resp.StatusCode}");
                }
            }

            token.ThrowIfCancellationRequested();

            if (!success && retry < maxRetries && (AnlasCostCalculator.Calculate(task, api.SubscriptionInfo) == 0 || task.RetryAll))
            {
                writeLogLine($"Failed, Retrying {++retry} / {maxRetries}");
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