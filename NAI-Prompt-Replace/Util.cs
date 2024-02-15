namespace NAI_Prompt_Replace;

public static class Util
{
    public static string GetValidFileName(string originalFileName)
    {
        originalFileName = replaceInvalidFileNameChars(originalFileName);

        string fileName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);

        int i = 0;

        while (File.Exists(fileName + extension))
        {
            fileName = originalFileName + " (" + ++i + ")";
        }

        return fileName + extension;
    }

    public static string GetValidDirectoryName(string originalPath)
    {
        return replaceInvalidFileNameChars(originalPath);
    }

    private static string replaceInvalidFileNameChars(string original)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            original = original.Replace(invalid, '_');
        }

        return original;
    }

    public static int CalculateCost(GenerationConfig config, SubscriptionInfo? subscriptionInfo)
    {
        int width = config.GenerationParameter.Width;
        int height = config.GenerationParameter.Height;
        int imageSize = width * height;
        imageSize = Math.Max(imageSize, 65536);
        string model = config.Model;
        int steps = config.GenerationParameter.Steps;
        int batchSize = config.BatchSize;
        float v = 0;
        if (subscriptionInfo?.Tier >= 3 && steps <= 28 && imageSize <= 1048576)
            batchSize = 0;

        if (model == "nai-diffusion-3")
        {
            bool sm = config.GenerationParameter.Smea;
            bool dyn = sm && config.GenerationParameter.Dyn;
            v = MathF.Ceiling(2951823174884865e-21f * imageSize + 5.753298233447344e-7f * imageSize * steps) * (dyn ? 1.4f : sm ? 1.2f : 1f);
        }
        int a = Math.Max((int)MathF.Ceiling(v), 2);

        if (config.GenerationParameter.UncondScale != 1)
            a = (int)Math.Ceiling(1.3f * a);
        return a * batchSize;
    }
}
