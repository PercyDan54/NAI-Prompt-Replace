using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
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

        var parameter = jsonDocument.Deserialize<GenerationParameter>(NovelAIApi.ApiSerializerOptions);

        if (parameter == null)
            throw new JsonException("Failed to parse GenerationParameter json");

        // ReferenceStrength will be 0 if no reference image is set, set to null instead
        if (parameter.ReferenceStrength < 0.01)
            parameter.ReferenceStrength = null;

        var generationConfig = new GenerationConfig
        {
            GenerationParameter = parameter
        };

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

    public static GenerationConfig ReadFile(Stream stream)
    {
        const string comment_key = "Comment";
        const string source_key = "Source";

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        byte[] headerBytes = new byte[8];
        memoryStream.Read(headerBytes, 0, 8);

        if (!headerBytes.SequenceEqual(png_header))
        {
            throw new Exception("Invalid PNG file");
        }

        var headers = ReadTextHeaders(memoryStream);
        headers.TryGetValue(source_key, out string? source);

        if (!headers.TryGetValue(comment_key, out string? comment))
        {
            memoryStream.Position = 0;
            comment = ReadStealthPng(memoryStream);

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
            throw new Exception("Image does not contain generation info");

        var config = ReadJson(comment);

        if (!string.IsNullOrEmpty(source))
            config.Model = GenerationModelInfo.FromHash(source);
        
        return config;
    }

    public static string ReadStealthPng(Stream stream)
    {
        using var bitmap = SKBitmap.Decode(stream);
        bool hasAlpha = bitmap.AlphaType != SKAlphaType.Opaque;
        StealthPngReadingState state = StealthPngReadingState.ReadingSignature;
        bool alphaMode = false;
        bool compressed = false;
        string content = string.Empty;
        byte[] bufferAlpha = new byte[15], bufferRgb = new byte[15], byteData = [];
        byte byteAlpha = 0, bitAlpha = 0, byteRgb = 0, bitRgb = 0;
        int indexRgb = 0, indexAlpha = 0;
        int paramLen = -1;

        for (int i = 0; i < bitmap.Width; i++)
        {
            for (int j = 0; j < bitmap.Height; j++)
            {
                var px = bitmap.GetPixel(i, j);

                void addBit(byte bit, ref byte[] buffer, ref byte currentByte, ref byte currentBit, ref int index)
                {
                    currentByte <<= 1;
                    currentByte |= bit;
                    currentBit++;

                    if (currentBit == 8 && index < buffer.Length)
                    {
                        buffer[index++] = currentByte;
                        currentBit = 0;
                        currentByte = 0;
                    }
                }

                if (hasAlpha)
                {
                    addBit((byte)(px.Alpha & 1), ref bufferAlpha, ref byteAlpha, ref bitAlpha, ref indexAlpha);
                }

                addBit((byte)(px.Red & 1), ref bufferRgb, ref byteRgb, ref bitRgb, ref indexRgb);
                addBit((byte)(px.Green & 1), ref bufferRgb, ref byteRgb, ref bitRgb, ref indexRgb);
                addBit((byte)(px.Blue & 1), ref bufferRgb, ref byteRgb, ref bitRgb, ref indexRgb);

                if (state == 0)
                {
                    if (indexAlpha == stealth_pnginfo_signature_alpha[0].Length)
                    {
                        string decodedSig = Encoding.UTF8.GetString(bufferAlpha);

                        for (int k = 0; k < stealth_pnginfo_signature_alpha.Length; k++)
                        {
                            if (stealth_pnginfo_signature_alpha[k] == decodedSig)
                            {
                                state++;
                                alphaMode = true;
                                compressed = k == 0;
                                bufferAlpha = new byte[4];
                                indexAlpha = 0;
                                break;
                            }
                        }

                        if (state != StealthPngReadingState.ReadingParamLen)
                            return content;
                    }
                    else if (indexRgb == stealth_pnginfo_signature_rgb[0].Length)
                    {
                        string decodedSig = Encoding.UTF8.GetString(bufferRgb);

                        for (int k = 0; k < stealth_pnginfo_signature_rgb.Length; k++)
                        {
                            if (stealth_pnginfo_signature_rgb[k] == decodedSig)
                            {
                                state++;
                                compressed = k == 0;
                                bufferRgb = new byte[4];
                                indexRgb = 0;
                                break;
                            }
                        }

                        if (state != StealthPngReadingState.ReadingParamLen && (!hasAlpha || indexAlpha >= 15))
                            state = StealthPngReadingState.ReadingEnd;
                    }
                }
                else if (state == StealthPngReadingState.ReadingParamLen)
                {
                    if (alphaMode && indexAlpha == 4)
                    {
                        paramLen = BinaryPrimitives.ReadInt32BigEndian(bufferAlpha) / 8;
                        state++;
                        bufferAlpha = new byte[paramLen];
                        indexAlpha = 0;
                    }
                    else if (indexRgb == 4 && bitRgb == 1)
                    {
                        byte pop = byteRgb;
                        paramLen = BinaryPrimitives.ReadInt32BigEndian(bufferRgb) / 8;
                        state++;
                        bufferRgb = new byte[paramLen];
                        indexRgb = byteRgb = bitRgb = 0;
                        addBit(pop, ref bufferRgb, ref byteRgb, ref bitRgb, ref indexRgb);
                    }
                }
                else if (state == StealthPngReadingState.ReadingData)
                {
                    if (alphaMode)
                    {
                        if (indexAlpha == paramLen)
                        {
                            byteData = bufferAlpha;
                            state++;
                            break;
                        }
                    }
                    else if (indexRgb >= paramLen)
                    {
                        int diff = paramLen - indexRgb;

                        if (diff < 0)
                            bufferRgb = bufferRgb[..diff];

                        byteData = bufferRgb;
                        state++;
                        break;
                    }
                }
                else
                {
                    state = StealthPngReadingState.ReadingEnd;
                }
            }

            if (state == StealthPngReadingState.ReadingEnd)
                break;
        }

        if (byteData.Length > 0)
        {
            if (compressed)
            {
                using var dataStream = new MemoryStream(byteData);
                using var gzip = new GZipStream(dataStream, CompressionMode.Decompress);
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

    private enum StealthPngReadingState
    {
        ReadingSignature,
        ReadingParamLen,
        ReadingData,
        ReadingEnd,
    }
}
