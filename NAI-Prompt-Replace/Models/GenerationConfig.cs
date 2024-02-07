using System.Text.Json.Serialization;

namespace NAI_Prompt_Replace;

public class GenerationConfig
{
    public string Prompt { get; set; } = "best quality, amazing quality, very aesthetic, absurdres";
    public string Replace { get; set; } = string.Empty;
    public string Model { get; set; } = "nai-diffusion-3";
    public string OutputPath { get; set; } =  string.Empty;
    public int BatchSize { get; set; } = 1;
    public bool AllRandom { get; set; }

    public GenerationParameter GenerationParameter { get; set; } = new GenerationParameter();

    public GenerationConfig Clone()
    {
        var clone = (GenerationConfig) MemberwiseClone();
        clone.GenerationParameter = GenerationParameter.Clone();
        return clone;
    }

    public override string ToString() => $"{GenerationParameter.Seed} - {Prompt}";
}

public class GenerationParameter
{
    public string NegativePrompt { get; set; } = "lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing";
    public string Sampler { get; set; } = "k_euler";
    public string NoiseSchedule { get; set; } = "native";

    [JsonPropertyName("sm")]
    public bool Smea { get; set; } = true;

    [JsonPropertyName("sm_dyn")]
    public bool Dyn { get; set; }

    public byte Steps { get; set; } = 28;
    public double Scale { get; set; } = 5;
    public double UncondScale { get; set; } = 1;
    public double CfgRescale { get; set; }
    public long? Seed { get; set; } = null;
    public bool LegacyV3Extend { get; set; } = false;
    public short Width { get; set; } = 832;
    public short Height { get; set; } = 1216;

    public GenerationParameter Clone() => (GenerationParameter) MemberwiseClone();
}
