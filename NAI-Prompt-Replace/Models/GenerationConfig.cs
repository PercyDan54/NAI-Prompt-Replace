using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NAI_Prompt_Replace;

public class GenerationConfig : INotifyPropertyChanged
{
    private int batchSize = 1;
    private string replace = string.Empty;
    public const string DEFAULT_OUTPUT_FILE_NAME = "{seed}-{prompt}";

    public string Prompt { get; set; } = "best quality, amazing quality, very aesthetic, absurdres";

    public string Replace
    {
        get => replace;
        set
        {
            replace = value;
            NotifyPropertyChanged();
        }
    }

    public string Model { get; set; } = "nai-diffusion-3";

    public string OutputPath { get; set; } = string.Empty;

    public string OutputFilename { get; set; } = DEFAULT_OUTPUT_FILE_NAME;

    [JsonIgnore]
    public string CurrentReplace { get; set; } = string.Empty;

    public int BatchSize
    {
        get => batchSize;
        set
        {
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
    private bool smea = true;
    private bool dyn;
    private byte steps = 28;
    private double uncondScale = 1;
    private double cfgRescale;
    private short width = 832;
    private short height = 1216;

    public string NegativePrompt { get; set; } = "lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing";

    public string Sampler { get; set; } = "k_euler";

    public long? Seed { get; set; } = null;

    public bool LegacyV3Extend { get; set; } = false;

    public string NoiseSchedule { get; set; } = "native";

    public double Scale { get; set; } = 5;

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
        return clone;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
