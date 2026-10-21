namespace Rayforge;

/// <summary>
/// Default <see cref="IGameLog"/>: writes to the console. On the web that is the browser console;
/// on desktop it is stdout.
/// </summary>
public sealed class ConsoleGameLog : IGameLog
{
    /// <summary>Shared instance; the logger is stateless.</summary>
    public static readonly ConsoleGameLog Instance = new();

    /// <inheritdoc />
    public void Info(string message) => Console.WriteLine($"[rayforge] {message}");

    /// <inheritdoc />
    public void Warn(string message) => Console.WriteLine($"[rayforge] warn: {message}");

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
        => Console.WriteLine(exception is null
            ? $"[rayforge] error: {message}"
            : $"[rayforge] error: {message}{Environment.NewLine}{exception}");
}
