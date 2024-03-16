using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace NAI_Prompt_Replace;

public static class PngMetadataReader
{
    private static readonly byte[] png_header = [137, 80, 78, 71, 13, 10, 26, 10];

    public static GenerationConfig FromJson(string json)
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
    
    public static GenerationConfig FromFile(string path)
    {
        const string comment_key = "Comment";
        const string source_key = "Source";

        using var stream = File.OpenRead(path);
        var headers = ReadTextHeaders(stream);

        if (!headers.TryGetValue(comment_key, out string? comment))
            throw new Exception($"Image {Path.GetFileName(path)} does not contain generation info");

        var config = FromJson(comment);

        if (!headers.TryGetValue(source_key, out string? source))
            return config;

        config.Model = NovelAIApi.ModelNameFromDescription(source);
        
        return config;
    }

    public static Dictionary<string, string> ReadTextHeaders(Stream stream)
    {
        using (BinaryReader binaryReader = new BinaryReader(stream))
        {
            byte[] headerBytes = binaryReader.ReadBytes(8);

            if (!headerBytes.SequenceEqual(png_header))
            {
                throw new Exception("Invalid PNG file");
            }

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
