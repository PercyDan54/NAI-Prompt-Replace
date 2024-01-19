using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;

namespace NAI_Prompt_Replace;

public partial class MainWindow : Window
{
    private const string config_file = "config.json";
    private const string novelai_api = "https://api.novelai.net/";
    private readonly HttpClient httpClient = new HttpClient();
#if DEBUG
    private readonly Random random = new Random(1337);
    #else
    private readonly Random random = new Random();
#endif
    private List<TextReplacement> replacements { get; set; } = new();

    private readonly JsonSerializerOptions apiSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private Config config { get; set; } = new Config();
    private CancellationTokenSource? cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
        loadConfig();
        DataContext = config;
    }

    private void loadConfig()
    {
        if (File.Exists(config_file))
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(config_file)) ?? config;
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

    private async Task<bool> generate(GenerationConfig generationConfig)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, novelai_api + "ai/generate-image");
        req.Headers.Add("Authorization", "Bearer " + config.AccessToken);

        var data = new Dictionary<string, object>
        {
            { "input", generationConfig.Prompt },
            { "model", generationConfig.Model },
            { "action", "generate" },
            { "parameters", generationConfig.GenerationParameter }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(data, apiSerializerOptions));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            var resp = await httpClient.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                writeLogLine($"{(int)resp.StatusCode} {resp.ReasonPhrase}");
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                string fileName = generationConfig.GenerationParameter.Seed + " - ";
                fileName = getValidFileName(fileName + generationConfig.Prompt[..128] + ".png");

                await using var file = File.OpenWrite(fileName);

                foreach (var entry in zip.Entries)
                {
                    await using var s = entry.Open();
                    await s.CopyToAsync(file);
                    file.Close();
                }

                return true;
            }
            else if (resp.Content.Headers.ContentType?.MediaType == "application/json")
            {
                string content = await resp.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<NovelAIGenerationResponse>(content);
                writeLogLine($"{response.StatusCode} {response.Message}");
            }
        }
        catch (Exception exception)
        {
            writeLogLine(exception.Message);
        }

        return false;
    }

    private string getValidFileName(string originalFileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            originalFileName = originalFileName.Replace(invalid, '_');
        }

        string fileName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);

        int i = 0;

        while (File.Exists(fileName + extension))
        {
            fileName = originalFileName + " (" + ++i + ")";
        }

        return fileName + extension;
    }

    private void RunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        if (cancellationTokenSource != null)
        {
            RunButton.Content = "Run";
            cancellationTokenSource.Cancel();
            cancellationTokenSource = null;
        }
        else
        {
            RunButton.Content = "Cancel";
            cancellationTokenSource = new CancellationTokenSource();
            var configs = getGenerationParameterControls().Select(p => p.Config).ToList();
            Task.Factory.StartNew(_ => runGeneration(configs, cancellationTokenSource.Token), null, TaskCreationOptions.LongRunning);
        }
    }

    private IEnumerable<GenerationParameterControl> getGenerationParameterControls()
    {
        foreach (var item in TabControl.Items)
        {
            if (((TabItem)item).Content is GenerationParameterControl parameterControl)
            {
                yield return parameterControl;
            }
        }
    }

    private async Task runGeneration(IEnumerable<GenerationConfig> configs, CancellationToken token)
    {
        var tasks = new List<GenerationConfig>();

        foreach (var generationConfig in configs)
        {
            var g = generationConfig.Clone();
            g.GenerationParameter.Seed ??= random.Next();
            long seed = g.GenerationParameter.Seed.Value;

            foreach (var replacement in replacements)
            {
                g.Prompt = g.Prompt.Replace(replacement.Target, replacement.Replace);
                g.Replace = generationConfig.Replace.Replace(replacement.Target, replacement.Replace);
            }

            string prompt = g.Prompt;
            for (int j = 0; j < g.BatchSize; j++)
            {
                var clone = g.Clone();
                clone.GenerationParameter.Seed = seed + j;
                tasks.Add(g);
            }

            string[] replaces = g.Replace.Split(',', StringSplitOptions.TrimEntries);

            if (replaces.Length > 1)
            {
                string toReplace = replaces[0];

                if (prompt.Contains(toReplace))
                {
                    string[] replaces1 = replaces[1..];

                    foreach (var r in replaces1)
                    {
                        for (int j = 0; j < g.BatchSize; j++)
                        {
                            var clone = g.Clone();
                            clone.GenerationParameter.Seed = seed + j;
                            clone.Prompt = prompt.Replace(toReplace, r);
                            tasks.Add(clone);
                        }
                    }
                }
            }
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 5;

        Dispatcher.UIThread.Invoke(() => ProgressBar.Value = 0);

        clearLog();

        while (i < tasks.Count)
        {
            token.ThrowIfCancellationRequested();
            writeLog($"Running task {i + 1} / {tasks.Count}: ");
            bool success = await generate(tasks[i]);

            if (!success && ++retry < maxRetries)
            {
                writeLogLine($"Failed, Retrying {retry} / {maxRetries}");
                Thread.Sleep(3000);
            }
            else
            {
                retry = 0;
                Dispatcher.UIThread.Invoke(() => ProgressBar.Value = ((i + 1.0) / tasks.Count) * 100);
                i++;
            }
        }

        Dispatcher.UIThread.Invoke(() =>
        {
            RunButton.Content = "Run";
            cancellationTokenSource = null;
        });
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

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            FileTypeFilter = 
            [
                new FilePickerFileType("JSON or CSV") { Patterns = ["*.json", "*.csv"] },
            ],
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            try
            {
                switch (Path.GetExtension(file.Name))
                {
                    case ".json":
                        var config = JsonSerializer.Deserialize<GenerationConfig>(await File.ReadAllTextAsync(file.Path.LocalPath), new JsonSerializerOptions
                        {
                            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                        });

                        if (config != null)
                        {
                            TabControl.Items.Add(new TabItem { Header = file.Name, Content = new GenerationParameterControl(config) });
                        }

                        break;
                    
                    case ".csv":
                        using (var reader = File.OpenText(file.Path.LocalPath))
                        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false }))
                        {
                            replacements = csv.GetRecords<TextReplacement>().ToList();
                            ReplacementDataGrid.ItemsSource = replacements;
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                writeLogLine(exception.Message);
            }
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

    private class NovelAIGenerationResponse
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
