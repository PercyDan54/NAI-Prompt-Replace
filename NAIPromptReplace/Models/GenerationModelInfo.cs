namespace NAIPromptReplace.Models;

public class GenerationModelInfo
{
    #region Models
    public static readonly GenerationModelInfo NaiDiffusion3 = new GenerationModelInfo
    {
        Name = "NAI Diffusion Anime V3",
        Id = "nai-diffusion-3",
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DdimV3,
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusionFurry3 = new GenerationModelInfo
    {
        Name = "NAI Diffusion Furry V3",
        Id = "nai-diffusion-furry-3",
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DdimV3,
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusion2 = new GenerationModelInfo
    {
        Name = "NovelAI Diffusion V2",
        Id = "nai-diffusion-2",
        Samplers =
        [
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.Ddim,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.Dpm2,
            SamplerInfo.DpmFast,
            SamplerInfo.Euler
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusion = new GenerationModelInfo
    {
        Name = "NovelAI Diffusion Anime V1 (Full)",
        Id = "nai-diffusion",
        Samplers =
        [
            SamplerInfo.DpmPp2M,
            SamplerInfo.EulerAncestral,
            SamplerInfo.Euler,
            SamplerInfo.Dpm2,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DpmFast,
            SamplerInfo.Ddim
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusionFurry = new GenerationModelInfo
    {
        Name = "NovelAI Diffusion Furry",
        Id = "nai-diffusion-furry",
        Samplers =
        [
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.Ddim,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.Dpm2,
            SamplerInfo.DpmFast,
            SamplerInfo.Euler,
        ]
    };
    public static readonly GenerationModelInfo SafeDiffusion = new GenerationModelInfo
    {
        Name = "NovelAI Diffusion Anime V1 (Curated)",
        Id = "safe-diffusion",
        Samplers =
        [
            SamplerInfo.DpmPp2M,
            SamplerInfo.EulerAncestral,
            SamplerInfo.Euler,
            SamplerInfo.Dpm2,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DpmFast,
            SamplerInfo.Ddim
        ]
    };

    public static readonly GenerationModelInfo[] Models = [NaiDiffusion3, NaiDiffusionFurry3, NaiDiffusion2, NaiDiffusion, NaiDiffusionFurry, SafeDiffusion];
    #endregion

    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public SamplerInfo[] Samplers { get; init; } = [];
}
