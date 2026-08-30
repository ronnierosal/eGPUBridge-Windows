namespace eGPUBridge.App.Services;

public interface IEventLogger
{
    void Info(string eventName, string message, object? data = null);

    void Error(string eventName, string message, Exception? exception = null, object? data = null);
}
