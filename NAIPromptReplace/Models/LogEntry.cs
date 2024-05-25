using Avalonia.Logging;
using ReactiveUI;

namespace NAIPromptReplace.Models;

public class LogEntry : ReactiveObject
{
    private string? text = string.Empty;
    private LogEventLevel logLevel;

    public string? Text
    {
        get => text;
        set => this.RaiseAndSetIfChanged(ref text, value);
    }

    public LogEventLevel LogLevel
    {
        get => logLevel;
        set => this.RaiseAndSetIfChanged(ref logLevel, value);
    }
}
