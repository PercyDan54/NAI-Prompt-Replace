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
}
