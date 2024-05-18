using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Converters;

namespace NAIPromptReplace.Models;

public class GenerationConfig : INotifyPropertyChanged
{
    private int batchSize = 1;
    private string replace = string.Empty;
    private string prompt = "best quality, amazing quality, very aesthetic, absurdres";
    private Dictionary<string, string> replacements = [];
    private GenerationModelInfo model = GenerationModelInfo.NaiDiffusion3;
    private string outputPath = string.Empty;
    public const string DEFAULT_OUTPUT_FILE_NAME = "{seed}-{prompt}";
    
    public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        Converters = { new JsonModelInfoConverter(), new JsonSamplerInfoConverter() },
        WriteIndented = true
    };

    public string Prompt
    {
        get => prompt;
        set
        {
            if (value == prompt)
                return;

            prompt = value;
            NotifyPropertyChanged();
        }
    }

    public string Replace
    {
        get => replace;
        set
        {
            if (value == replace)
                return;

            replace = value;
            NotifyPropertyChanged();
        }
    }

    public GenerationModelInfo Model
    {
        get => model;
        set
        {
            if (value == model)
                return;

            model = value;
            NotifyPropertyChanged();
        }
    }

    public string OutputPath
    {
        get => outputPath;
        set
        {
            if (value == outputPath)
                return;

            outputPath = value;
            NotifyPropertyChanged();
        }
    }

    [JsonIgnore]
    public IStorageFolder? StorageFolder { get; set; }

    public string OutputFilename { get; set; } = DEFAULT_OUTPUT_FILE_NAME;

    [JsonIgnore]
    public string CurrentReplace { get; set; } = string.Empty;

    [JsonIgnore]
    public Dictionary<string, string> Replacements
    {
        get => replacements;
        set
        {
            if (value == replacements)
                return;

            replacements = value;
            NotifyPropertyChanged();
        }
    }

    public int BatchSize
    {
        get => batchSize;
        set
        {
            if (value == batchSize)
                return;

            batchSize = value;
            NotifyPropertyChanged();
        }
    }

    public bool AllRandom { get; set; }

    public bool RetryAll { get; set; }

    public bool SaveJpeg { get; set; }

    public GenerationParameter GenerationParameter { get; set; } = new GenerationParameter();

    public GenerationConfig Clone()
    {
        var clone = (GenerationConfig) MemberwiseClone();
        clone.PropertyChanged = null;
        clone.GenerationParameter = GenerationParameter.Clone();
        return clone;
    }

    public async Task SaveAsync(IStorageFile file)
    {
        try
        {
            using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch
        {
        }
    }

    public static string GetReplacedPrompt(string prompt, Dictionary<string, string> replacements)
    {
        string[] lines = prompt.Split(Environment.NewLine);
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

                if (replacements.TryGetValue(word, out string? replacement))
                {
                    words[i] = replacement;
                }
                else if (replacements.TryGetValue(wordsNoBracket, out replacement))
                {
                    words[i] = $"{bracketStart}{replacement}{bracketEnd}";
                }
            }

            newLines.Add(string.Join(',', words));
        }

        return string.Join(Environment.NewLine, newLines);
    }

    public override string ToString()
    {
        return $"{GenerationParameter.Seed} - {Prompt}";
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class GenerationParameter : INotifyPropertyChanged
{
    private bool smea;
    private bool dyn;
    private byte steps = 28;
    private double uncondScale = 1;
    private double cfgRescale;
    private short width = 832;
    private short height = 1216;
    private string negativePrompt = "lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing";
    private SamplerInfo? sampler = SamplerInfo.Euler;

    public string NegativePrompt
    {
        get => negativePrompt;
        set
        {
            if (value == negativePrompt)
                return;

            negativePrompt = value;
            NotifyPropertyChanged();
        }
    }

    public SamplerInfo? Sampler
    {
        get => sampler;
        set
        {
            if (value == sampler)
                return;

            sampler = value;
            NotifyPropertyChanged();
        }
    }

    public long? Seed { get; set; }

    public bool LegacyV3Extend { get; set; }

    public string NoiseSchedule { get; set; } = "native";

    public double Scale { get; set; } = 5;

    [JsonPropertyName("ucPreset")]
    public byte UcPreset { get; private init; } = 3;

    public bool AddOriginalImage { get; private init; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceImage { get; set; }

    [JsonIgnore]
    public byte[][] ReferenceImageData { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Image { get; set; }

    [JsonIgnore]
    public byte[]? ImageData { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Strength { get; set; } = 0.7;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Noise { get; set; } = 0;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExtraNoiseSeed { get; set; }

    public string[] ReferenceImageMultiple { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ReferenceInformationExtracted { get; set; }

    public double[] ReferenceInformationExtractedMultiple { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ReferenceStrength { get; set; }

    public double[] ReferenceStrengthMultiple { get; set; } = [];

    [JsonPropertyName("sm")]
    public bool Smea
    {
        get => smea;
        set
        {
            smea = value;
            NotifyPropertyChanged();
        }
    }

    [JsonPropertyName("sm_dyn")]
    public bool Dyn
    {
        get => dyn;
        set
        {
            dyn = value;
            NotifyPropertyChanged();
        }
    }

    public byte Steps
    {
        get => steps;
        set
        {
            steps = value;
            NotifyPropertyChanged();
        }
    }

    public double UncondScale
    {
        get => uncondScale;
        set
        {
            uncondScale = value;
            NotifyPropertyChanged();
        }
    }

    public double CfgRescale
    {
        get => cfgRescale;
        set
        {
            cfgRescale = value;
            NotifyPropertyChanged();
        }
    }

    public short Width
    {
        get => width;
        set
        {
            width = value;
            NotifyPropertyChanged();
        }
    }

    public short Height
    {
        get => height;
        set
        {
            height = value;
            NotifyPropertyChanged();
        }
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public GenerationParameter Clone()
    {
        var clone = (GenerationParameter)MemberwiseClone();
        clone.PropertyChanged = null;
        clone.ReferenceImageMultiple = ReferenceImageMultiple.ToArray();
        clone.ReferenceStrengthMultiple = ReferenceStrengthMultiple.ToArray();
        clone.ReferenceInformationExtractedMultiple = ReferenceInformationExtractedMultiple.ToArray();
        return clone;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
