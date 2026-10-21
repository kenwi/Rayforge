namespace Rayforge;

/// <summary>Host log. On the web it maps to the browser console; on desktop to stdout.</summary>
public interface IGameLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
