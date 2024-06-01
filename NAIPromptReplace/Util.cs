using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

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

    public static SKBitmap RemoveImageAlpha(Stream stream)
    {
        var bitmap = SKBitmap.Decode(stream);

        for (int i = 0; i < bitmap.Width; i++)
        {
            for (int j = 0; j < bitmap.Height; j++)
            {
                var pixel = bitmap.GetPixel(i, j);
                bitmap.SetPixel(i, j, pixel.WithAlpha(byte.MaxValue));
            }
        }

        return bitmap;
    }

    public static Bitmap ResizeBitmap(Bitmap image, int? maxWidth = null, int? maxHeight = null)
    {
        if (maxHeight.HasValue || maxWidth.HasValue)
        {
            int width = image.PixelSize.Width;
            int height = image.PixelSize.Height;
            maxWidth ??= width;
            maxHeight ??= height;
            float ratioBitmap = width / (float)height;
            float ratioMax = maxWidth.Value / (float)maxHeight.Value;

            int finalWidth = maxWidth.Value;
            int finalHeight = maxHeight.Value;
            if (ratioMax > ratioBitmap)
            {
                finalWidth = (int) (maxHeight * ratioBitmap);
            }
            else
            {
                finalHeight = (int) (maxWidth / ratioBitmap);
            }

            image = image.CreateScaledBitmap(new PixelSize(finalWidth, finalHeight));
            return image;
        }
  
        return image;
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
}
