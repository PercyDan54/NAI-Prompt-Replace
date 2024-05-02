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
        Name = "NAI Diffusion V2",
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
        Name = "NAI Diffusion Anime V1 (Full)",
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
        Name = "NAI Diffusion Furry",
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
        Name = "NAI Diffusion Anime V1 (Curated)",
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

    public static GenerationModelInfo FromHash(string hash)
    {
        switch (hash)
        {
            case "Stable Diffusion 1D44365E":
            case "Stable Diffusion F4D50568":
                return SafeDiffusion;
            case "Stable Diffusion 81274D13":
            case "Stable Diffusion 3B3287AF":
                return NaiDiffusion;
            case "Stable Diffusion 4CC42576":
            case "Stable Diffusion 1D09C008":
            case "Stable Diffusion 1D09D794":
            case "Stable Diffusion F64BA557":
                return NaiDiffusionFurry;
            case "Stable Diffusion 49BFAF6A":
            case "Stable Diffusion F1022D28":
                return NaiDiffusion2;
            case "Stable Diffusion XL 4BE8C60C":
            case "Stable Diffusion XL C8704949":
            case "Stable Diffusion XL 9CC2F394":
                return NaiDiffusionFurry3;
            case "Stable Diffusion XL B0BDF6C1":
            case "Stable Diffusion XL C1E1DE52":
            case "Stable Diffusion XL 8BA2AF87":
            default:
                return NaiDiffusion3;
        }
    }
}
