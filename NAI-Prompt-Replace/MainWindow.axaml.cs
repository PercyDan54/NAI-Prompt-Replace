using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NAI_Prompt_Replace;

public partial class MainWindow : Window
{
    private const string config_file = "config.json";
    private const string novelai_api = "https://api.novelai.net/";
    private readonly HttpClient httpClient = new HttpClient();
    private readonly Random random = new Random(7);

    private readonly JsonSerializerOptions apiSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private Config config { get; set; } = new Config();

    public MainWindow()
    {
        InitializeComponent();
        loadConfig();
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
            writeLogLine($"{(int)resp.StatusCode} {resp.ReasonPhrase}");

            if (resp.IsSuccessStatusCode)
            {
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                string fileName = generationConfig.GenerationParameter.Seed + " - ";
                fileName = getValidFileName(fileName + generationConfig.Prompt[..(128 - fileName.Length)] + ".png");

                await using var file = File.OpenWrite(fileName);

                foreach (var entry in zip.Entries)
                {
                    using var s = entry.Open();
                    s.CopyTo(file);
                    file.Close();
                }

                return true;
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

    private async void RunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        var tasks = new List<GenerationConfig>();

        foreach (var item in TabControl.Items)
        {
            if (((TabItem)item).Content is GenerationParameterControl parameterControl)
            {
                var generationConfig = parameterControl.Config;
                string prompt = generationConfig.Prompt;

                var g = generationConfig.Clone();
                g.GenerationParameter.Seed ??= random.Next();
                tasks.Add(g);

                string[] replaces = generationConfig.Replace.Split(',', StringSplitOptions.TrimEntries);

                if (replaces.Length > 1)
                {
                    string toReplace = replaces[0];
                    string[] replaces1 = replaces[1..];

                    foreach (var r in replaces1)
                    {
                        var clone = g.Clone();
                        clone.Prompt = prompt.Replace(toReplace, r);
                        tasks.Add(clone);
                    }
                }
            }
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 5;

        RunButton.IsEnabled = false;
        ProgressBar.Value = 0;
        clearLog();

        while (i < tasks.Count)
        {
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
                ProgressBar.Value = ((i + 1.0) / tasks.Count) * 100;
                i++;
            }
        }

        RunButton.IsEnabled = true;
    }

    private void writeLogLine(object obj)
    {
        writeLog(obj + Environment.NewLine);
    }

    private void writeLog(object obj)
    {
        LogTextBox.Text += obj;
    }

    private void clearLog()
    {
        LogTextBox.Text = string.Empty;
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            var config = JsonSerializer.Deserialize<GenerationConfig>(await File.ReadAllTextAsync(file.Path.AbsolutePath));

            if (config != null)
            {
                TabControl.Items.Add(new TabItem { Header = file.Name, Content = new GenerationParameterControl(config) });
            }
        }
    }

    private void TabControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        TabControl.InvalidateMeasure();
    }
}
