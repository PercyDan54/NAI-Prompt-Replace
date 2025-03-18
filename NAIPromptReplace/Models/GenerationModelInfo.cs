namespace NAIPromptReplace.Models;

public class GenerationModelInfo
{
    #region Models
    public static readonly GenerationModelInfo NaiDiffusion4Full = new GenerationModelInfo
    {
        Name = "NAI Diffusion V4 Full",
        Id = "nai-diffusion-4-full",
        Group = ModelGroup.V4,
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2MSde,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusion4CuratedPreview = new GenerationModelInfo
    {
        Name = "NAI Diffusion V4 Curated",
        Id = "nai-diffusion-4-curated-preview",
        Group = ModelGroup.V4,
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2MSde,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusion3 = new GenerationModelInfo
    {
        Name = "NAI Diffusion Anime V3",
        Id = "nai-diffusion-3",
        Group = ModelGroup.StableDiffusionXL,
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2MSde,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DdimV3,
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusionFurry3 = new GenerationModelInfo
    {
        Name = "NAI Diffusion Furry V3",
        Id = "nai-diffusion-furry-3",
        Group = ModelGroup.StableDiffusionXLFurry,
        Samplers =
        [
            SamplerInfo.Euler,
            SamplerInfo.EulerAncestral,
            SamplerInfo.DpmPp2SAncestral,
            SamplerInfo.DpmPp2MSde,
            SamplerInfo.DpmPp2M,
            SamplerInfo.DpmPpSde,
            SamplerInfo.DdimV3,
        ]
    };
    public static readonly GenerationModelInfo NaiDiffusion2 = new GenerationModelInfo
    {
        Name = "NAI Diffusion Anime V2",
        Id = "nai-diffusion-2",
        Group = ModelGroup.StableDiffusionGroup2,
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

    public static readonly GenerationModelInfo[] Models = [NaiDiffusion4Full, NaiDiffusion4CuratedPreview, NaiDiffusion3, NaiDiffusionFurry3, NaiDiffusion2, NaiDiffusion, NaiDiffusionFurry, SafeDiffusion];
    public static readonly string[] Schedulers = ["native", "karras", "exponential", "polyexponential"];

    #endregion

    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public ModelGroup Group { get; init; } = ModelGroup.StableDiffusion;
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
            case "Stable Diffusion XL 37C2B166":
            case "Stable Diffusion XL F306816B":
            case "Stable Diffusion XL 9CC2F394":
                return NaiDiffusionFurry3;
            case "Stable Diffusion XL B0BDF6C1":
            case "Stable Diffusion XL C1E1DE52":
            case "Stable Diffusion XL 7BCCAA2C":
            case "Stable Diffusion XL 1120E6A9":
            case "Stable Diffusion XL 8BA2AF87":
                return NaiDiffusion3;
            case "NovelAI Diffusion V4 4F49EC75":
            case "NovelAI Diffusion V4 CA4B7203":
            case "NovelAI Diffusion V4 79F47848":
            case "NovelAI Diffusion V4 F6302A9D":
                return NaiDiffusion4Full;
            default:
                return hash.Contains("NovelAI Diffusion V4", StringComparison.Ordinal) ? NaiDiffusion4CuratedPreview : NaiDiffusion3;
        }
    }
}

public enum ModelGroup
{
    StableDiffusion,
    StableDiffusionGroup2,
    StableDiffusionXL,
    StableDiffusionXLFurry,
    V4,
}
