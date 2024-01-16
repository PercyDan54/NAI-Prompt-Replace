using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NAI_Prompt_Replace;

public partial class MainWindow : Window
{
    private const string config_file = "config.json";
    private const string novelai_api = "https://api.novelai.net/";
    private readonly HttpClient httpClient = new HttpClient();
    private readonly Random random = new Random(7);
    private readonly List<string> models = ["nai-diffusion-3", "safe-diffusion", "nai-diffusion-furry", "nai-diffusion-inpainting", "nai-diffusion-3-inpainting", "safe-diffusion-inpainting", "furry-diffusion-inpainting", "kandinsky-vanilla", "nai-diffusion-2", "nai-diffusion"];
    private readonly List<string> samplers = ["k_euler", "k_euler_ancestral", "k_dpmpp_2s_ancestral", "k_dpmpp_2m", "k_dpmpp_sde", "ddim_v3"];

    private readonly JsonSerializerOptions apiSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private Config config { get; set; } = new Config();

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

        ModelComboBox.ItemsSource = models;
        ModelComboBox.SelectedIndex = models.IndexOf(config.Model);
        SamplerComboBox.SelectedIndex = samplers.IndexOf(config.GenerationParameter.Sampler);
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
            config.Model = models[ModelComboBox.SelectedIndex];
            config.GenerationParameter.Sampler = samplers[SamplerComboBox.SelectedIndex];
            File.WriteAllText(config_file, JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception e)
        {
        }
    }

    private async Task<bool> generate(GenerationParameter generationParameter, string prompt)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, novelai_api + "ai/generate-image");
        req.Headers.Add("Authorization", "Bearer " + config.AccessToken);
        var data = new Dictionary<string, object>
        {
            { "input", prompt },
            { "model", models[ModelComboBox.SelectedIndex] },
            { "action", "generate" },
            { "parameters", generationParameter }
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
                string fileName = generationParameter.Seed + " - ";
                fileName = getValidFileName(fileName + prompt[..(128 - fileName.Length - Environment.CurrentDirectory.Length)]) + ".png";

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
        string fileName = originalFileName;

        int i = 0;
        while (File.Exists(fileName))
        {
            fileName = originalFileName + " (" + ++i + ")";
        }

        return fileName;
    }

    private async void RunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        RunButton.IsEnabled = false;
        ProgressBar.Value = 0;
        clearLog();

        config.GenerationParameter.Sampler = samplers[SamplerComboBox.SelectedIndex];
        string prompt = config.Prompt;
        List<string> tasks = [prompt];
        var g = config.GenerationParameter.Clone();
        g.Seed ??= random.Next();

        string[] replaces = config.Replace.Split(',', StringSplitOptions.TrimEntries);
        if (replaces.Length > 1)
        {
            string toReplace = replaces[0];
            string[] replaces1 = replaces[1..];
            foreach (var r in replaces1)
            {
                tasks.Add(prompt.Replace(toReplace, r));
            }
        }

        int i = 0;
        int retry = 0;
        const int maxRetries = 5;

        while (i < tasks.Count)
        {
            writeLog($"Running task {i + 1} / {tasks.Count}: ");
            bool success = await generate(g, tasks[i]);
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

    private void preventNullValue(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
        {
            var numericUpDown = (NumericUpDown)sender;
            numericUpDown.Value = numericUpDown.Minimum;
        }
    }
}
