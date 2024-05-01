using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using SkiaSharp;

namespace NAIPromptReplace;

public static class PngMetadataReader
{
    private static readonly byte[] png_header = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly string[] stealth_pnginfo_signature_alpha = ["stealth_pngcomp", "stealth_pnginfo"];
    private static readonly string[] stealth_pnginfo_signature_rgb = ["stealth_rgbcomp", "stealth_rgbinfo"];

    public static GenerationConfig ReadJson(string json)
    {
        var jsonDocument = JsonDocument.Parse(json);

        var parameters = jsonDocument.Deserialize<GenerationParameter>(NovelAIApi.ApiSerializerOptions);

        if (parameters == null)
            throw new JsonException("Failed to parse json");
        
        var generationConfig = new GenerationConfig
        {
            GenerationParameter = parameters
        };

        // ReferenceStrength will be 0 if no reference image is set, set to default instead
        if (generationConfig.GenerationParameter.ReferenceStrength < 0.01)
            generationConfig.GenerationParameter.ReferenceStrength = 1;

        bool legacy = true;
        
        foreach (var property in jsonDocument.RootElement.EnumerateObject())
        {
            if (property.Name == "legacy_v3_extend")
                legacy = property.Value.GetBoolean();

            if (property.Value.ValueKind != JsonValueKind.String)
                continue;
            
            string value = property.Value.GetString() ?? string.Empty;

            switch (property.Name)
            {
                case "prompt":
                    generationConfig.Prompt = value;
                    break;
                case "uc":
                    generationConfig.GenerationParameter.NegativePrompt = value;
                    break;
            }
        }

        generationConfig.GenerationParameter.LegacyV3Extend = legacy;

        return generationConfig;
    }

    public static GenerationConfig ReadFile(IStorageFile file)
    {
        const string comment_key = "Comment";
        const string source_key = "Source";

        using var stream = file.OpenReadAsync().Result;
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        byte[] headerBytes = new byte[8];
        memoryStream.Read(headerBytes, 0, 8);

        if (!headerBytes.SequenceEqual(png_header))
        {
            throw new Exception($"File {file.Name} is am invalid PNG file");
        }

        var headers = ReadTextHeaders(memoryStream);
        headers.TryGetValue(source_key, out string? source);

        if (!headers.TryGetValue(comment_key, out string? comment))
        {
            memoryStream.Position = 0;
            comment = ReadStealthPng(memoryStream.ToArray());

            if (!string.IsNullOrEmpty(comment))
            {
                var jsonDocument = JsonDocument.Parse(comment);

                foreach (var property in jsonDocument.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        continue;

                    string? value = property.Value.GetString();

                    if (property.Name == comment_key)
                        comment = value;

                    else if (property.Name == source_key)
                        source = value;
                }
            }
        }

        if (string.IsNullOrEmpty(comment))
            throw new Exception($"Image {file.Name} does not contain generation info");

        var config = ReadJson(comment);

        if (!string.IsNullOrEmpty(source))
            config.Model = GenerationModelInfo.FromHash(source);
        
        return config;
    }

    public static string ReadStealthPng(byte[] data)
    {
        using var bitmap = SKBitmap.Decode(data);
        bool hasAlpha = bitmap.AlphaType != SKAlphaType.Opaque;
        byte stage = 0;
        bool alphaMode = false;
        bool compressed = false;
        string bufferRgb = string.Empty, bufferAlpha = string.Empty, binaryData = string.Empty, content = string.Empty;
        int indexRgb = 0, indexAlpha = 0;
        int paramLen = -1;

        for (int i = 0; i < bitmap.Width; i++)
        {
            for (int j = 0; j < bitmap.Height; j++)
            {
                var px = bitmap.GetPixel(i, j);

                if (hasAlpha)
                {
                    bufferAlpha += px.Alpha & 1;
                    indexAlpha++;
                }

                bufferRgb += px.Red & 1;
                bufferRgb += px.Green & 1;
                bufferRgb += px.Blue & 1;
                indexRgb += 3;

                if (stage == 0)
                {
                    if (indexAlpha == stealth_pnginfo_signature_alpha[0].Length * 8)
                    {
                        string decodedSig = binaryStringToString(bufferAlpha);

                        for (int k = 0; k < stealth_pnginfo_signature_alpha.Length; k++)
                        {
                            if (stealth_pnginfo_signature_alpha[k] == decodedSig)
                            {
                                stage++;
                                alphaMode = true;
                                compressed = k == 0;
                                bufferAlpha = string.Empty;
                                indexAlpha = 0;
                                break;
                            }
                        }

                        if (stage != 1)
                            stage = 3;
                    }
                    else if (indexRgb == stealth_pnginfo_signature_rgb[0].Length * 8)
                    {
                        string decodedSig = binaryStringToString(bufferRgb);

                        for (int k = 0; k < stealth_pnginfo_signature_rgb.Length; k++)
                        {
                            if (stealth_pnginfo_signature_rgb[k] == decodedSig)
                            {
                                stage++;
                                compressed = k == 0;
                                bufferRgb = string.Empty;
                                indexRgb = 0;
                                break;
                            }
                        }
                    }
                }
                else if (stage == 1)
                {
                    if (alphaMode && indexAlpha == 32)
                    {
                        paramLen = Convert.ToInt32(bufferAlpha, 2);
                        stage++;
                        bufferAlpha = string.Empty;
                        indexAlpha = 0;
                    }
                    else if (indexRgb == 33)
                    {
                        char pop = bufferRgb[^1];
                        bufferRgb.Remove(bufferRgb.Length - 1);
                        paramLen = Convert.ToInt32(bufferRgb, 2);
                        stage++;
                        bufferRgb += pop;
                        indexRgb = 1;
                    }
                }
                else if (stage == 2)
                {
                    if (alphaMode)
                    {
                        if (indexAlpha == paramLen)
                        {
                            binaryData = bufferAlpha;
                            stage++;
                            break;
                        }
                    }
                    else if (indexRgb >= paramLen)
                    {
                        int diff = paramLen - indexRgb;

                        if (diff < 0)
                            bufferRgb = bufferRgb[..diff];

                        binaryData = bufferRgb;
                        stage++;
                        break;
                    }
                }
                else
                {
                    stage = 3;
                }
            }

            if (stage == 3)
                break;
        }

        if (!string.IsNullOrEmpty(binaryData))
        {
            byte[] byteData = binaryData.Chunk(8)
                .Select(chars => Convert.ToByte(new string(chars), 2))
                .ToArray();

            if (compressed)
            {
                using var stream = new MemoryStream(byteData);
                using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                content = reader.ReadToEnd();
            }
            else
            {
                content = Encoding.UTF8.GetString(byteData);
            }
        }

        return content;
    }

    private static string binaryStringToString(string binaryString)
    {
        byte[] byteArray = binaryString.Chunk(8)
            .Select(chars => Convert.ToByte(new string(chars), 2))
            .ToArray();

        return Encoding.UTF8.GetString(byteArray);
    }

    public static Dictionary<string, string> ReadTextHeaders(Stream stream)
    {
        using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8, true))
        {
            var dict = new Dictionary<string, string>();

            while (stream.Position < stream.Length)
            {
                int chunkLength = BinaryPrimitives.ReadInt32BigEndian(binaryReader.ReadBytes(4));
                
                byte[] chunkTypeBytes = binaryReader.ReadBytes(4);
                string chunkType = Encoding.UTF8.GetString(chunkTypeBytes);

                ReadOnlySpan<byte> chunkData = binaryReader.ReadBytes(chunkLength);

                if (chunkType == "tEXt")
                {
                    int zeroIndex = chunkData.IndexOf((byte)0);
                
                    if (zeroIndex is < 1 or > 79)
                    {
                        continue;
                    }

                    dict.Add(Encoding.UTF8.GetString(chunkData[..zeroIndex]), Encoding.UTF8.GetString(chunkData[(zeroIndex + 1)..]));
                }
                
                // Ignore image data chunks
                if (chunkType == "IDAT")
                    break;

                // CRC Hash
                binaryReader.ReadInt32();
            }

            return dict;
        }
    }
}
