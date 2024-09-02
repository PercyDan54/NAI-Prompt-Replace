using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data;
using Avalonia.Data.Converters;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public partial class TextToTokenCountConverter : IMultiValueConverter
{
    private static readonly SimpleTextTokenizer tokenizer = SimpleTextTokenizer.Load();

    [GeneratedRegex("[{}\\[\\]]+", RegexOptions.Compiled)]
    private static partial Regex bracketsRegex();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not string prompt)
            return BindingOperations.DoNothing;

        if (values.Count >= 2 && values[1] is Dictionary<string, string> replacements)
        {
            prompt = GenerationConfig.GetReplacedPrompt(prompt, replacements);
        }

        prompt = bracketsRegex().Replace(prompt, string.Empty);

        return (double) tokenizer.Encode(prompt).Count;
    }
}
