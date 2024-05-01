using NAIPromptReplace.Models;

namespace NAIPromptReplace;

public static class Util
{
    public static string GetValidFileName(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string originalFileName = ReplaceInvalidFileNameChars(Path.GetFileName(path));

        string fileName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);

        int i = 0;
        string current = Path.Combine(directory, Path.ChangeExtension(fileName, extension));

        while (File.Exists(current))
        {
            current = Path.Combine(directory, Path.ChangeExtension(fileName + " (" + ++i + ")", extension));
        }

        return current;
    }

    public static string GetValidDirectoryName(string originalPath)
    {
        return ReplaceInvalidFileNameChars(originalPath);
    }

    // https://stackoverflow.com/questions/32571057/generate-all-combinations-from-multiple-n-lists
    public static List<List<string>> GetAllPossibleCombos(List<string[]> strings)
    {
        IEnumerable<List<string>> combos = [[]];

        foreach (string[] inner in strings)
        {
            combos = combos.SelectMany(r => inner
                .Select(x =>
                {
                    var n = r.ToList();
                    n.Add(x);
                    return n;
                }));
        }

        return combos.ToList();
    }

    public static string TruncateString(string input, int maxLength)
    {
        if (input.Length <= maxLength)
        {
            return input;
        }

        int startLength = (maxLength - 3) / 2;
        int endLength = maxLength - 3 - startLength;

        return input[..startLength] + "..." + input[^endLength..];
    }

    public static string ReplaceInvalidFileNameChars(string original)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars().Append(Path.AltDirectorySeparatorChar))
        {
            original = original.Replace(invalid, '_');
        }

        return original;
    }

    public static int CalculateCost(GenerationConfig config, SubscriptionInfo? subscription)
    {
        int width = config.GenerationParameter.Width;
        int height = config.GenerationParameter.Height;
        int imageSize = Math.Max(width * height, 65536);
        string model = config.Model.Id;
        int steps = config.GenerationParameter.Steps;
        int batchSize = config.BatchSize;
        float v = 0;

        if (subscription?.Tier >= 3 && subscription.Active && steps <= 28 && imageSize <= 1048576)
            batchSize = 0;

        if (model == "nai-diffusion-3")
        {
            bool sm = config.GenerationParameter.Smea;
            bool dyn = sm && config.GenerationParameter.Dyn;
            v = MathF.Ceiling(2951823174884865e-21f * imageSize + 5.753298233447344e-7f * imageSize * steps) * (dyn ? 1.4f : sm ? 1.2f : 1f);
        }

        int a = Math.Max((int)MathF.Ceiling(v), 2);

        if (config.GenerationParameter.UncondScale != 1)
            a = (int)MathF.Ceiling(1.3f * a);

        return a * batchSize;
    }
}
