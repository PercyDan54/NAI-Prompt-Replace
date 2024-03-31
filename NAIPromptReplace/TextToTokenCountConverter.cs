using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using NAIPromptReplace.Models;

namespace NAIPromptReplace;

public class TextToTokenCountConverter : IMultiValueConverter
{
    private static readonly SimpleTextTokenizer tokenizer = SimpleTextTokenizer.Load();
    private static readonly Regex bracketsRegex = new Regex("[{}\\[\\]]+", RegexOptions.Compiled);

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not string prompt)
            return 0;

        if (values.Count >= 2 && values[1] is Dictionary<string, string> replacements)
        {
            prompt = GenerationConfig.GetReplacedPrompt(prompt, replacements);
        }

        prompt = bracketsRegex.Replace(prompt, string.Empty);

        return tokenizer.Encode(prompt).Count;
    }
}
