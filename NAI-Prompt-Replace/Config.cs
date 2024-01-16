using System;
using System.Text.Json.Serialization;

namespace NAI_Prompt_Replace;

public class Config
{
    public string AccessToken { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Replace { get; set; } = string.Empty;
    public string Model { get; set; } = "nai-diffusion-3";
    public GenerationConfig GenerationConfig { get; set; } = new GenerationConfig();
}

public class GenerationConfig
{
    public string NegativePrompt { get; set; } = string.Empty;
    public string Sampler { get; set; } = "k_euler";
    
    [JsonPropertyName("sm")]
    public bool Smea { get; set; } = true;

    [JsonPropertyName("sm_dyn")]
    public bool Dyn { get; set; }

    public byte Steps { get; set; } = 28;
    public double Scale { get; set; } = 5;
    public long? Seed { get; set; } = null;

    [JsonPropertyName("ucPreset")]
    public int Uc { get; set; } = 3;

    public short Width { get; set; } = 832;
    public short Height { get; set; } = 1216;

    public GenerationConfig Clone() => (GenerationConfig) MemberwiseClone();
}
