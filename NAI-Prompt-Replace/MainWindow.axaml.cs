using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(config_file));
        }

        ModelComboBox.ItemsSource = models;
        ModelComboBox.SelectedIndex = models.IndexOf(config.Model);
        SamplerComboBox.SelectedIndex = samplers.IndexOf(config.GenerationConfig.Sampler);
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
            config.GenerationConfig.Sampler = samplers[SamplerComboBox.SelectedIndex];
            File.WriteAllText(config_file, JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception e)
        {
        }
    }

    private async Task generate(GenerationConfig generationConfig, string prompt)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, novelai_api + "ai/generate-image");
        req.Headers.Add("Authorization", "Bearer " + config.AccessToken);
        var data = new Dictionary<string, object>
        {
            { "input", prompt },
            { "model", models[ModelComboBox.SelectedIndex] },
            { "action", "generate" },
            { "parameters", generationConfig }
        };
        req.Content = new StringContent(JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        try
        {
            var resp = await httpClient.SendAsync(req);
            writeLogLine(resp.ReasonPhrase);

            if (resp.IsSuccessStatusCode)
            {
                using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
                string fileName = generationConfig.Seed + " - ";
                await using var file = File.OpenWrite(fileName + prompt[..(255 - fileName.Length - Environment.CurrentDirectory.Length)] + ".png");
                foreach (var entry in zip.Entries)
                {
                    using var s = entry.Open();
                    s.CopyTo(file);
                    file.Close();
                }
            }
        }
        catch (Exception exception)
        {
            writeLogLine(exception.Message);
        }
    }

    private async void RunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        RunButton.IsEnabled = false;
        clearLog();

        config.GenerationConfig.Sampler = samplers[SamplerComboBox.SelectedIndex];
        var g = config.GenerationConfig.Clone();
        g.Seed ??= random.Next();
        await generate(g, config.Prompt);
        
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
}
