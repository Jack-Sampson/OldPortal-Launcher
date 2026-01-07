using Serilog;
using System;

namespace OPLauncher.Services;

/// <summary>
/// Wrapper service for logging functionality using Serilog.
/// Provides a consistent interface for logging throughout the application.
/// </summary>
public class LoggingService
{
    private readonly ILogger _logger;

    public LoggingService()
    {
        _logger = Log.ForContext<LoggingService>();
    }

    /// <summary>
    /// Create a logger for a specific context/class.
    /// </summary>
    public ILogger ForContext<T>()
    {
        return Log.ForContext<T>();
    }

    /// <summary>
    /// Create a logger for a specific context with a name.
    /// </summary>
    public ILogger ForContext(string contextName)
    {
        return Log.ForContext("SourceContext", contextName);
    }

    /// <summary>
    /// Log verbose/trace level message.
    /// </summary>
    public void Verbose(string messageTemplate, params object[] propertyValues)
    {
        _logger.Verbose(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log debug level message.
    /// </summary>
    public void Debug(string messageTemplate, params object[] propertyValues)
    {
        _logger.Debug(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log information level message.
    /// </summary>
    public void Information(string messageTemplate, params object[] propertyValues)
    {
        _logger.Information(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log warning level message.
    /// </summary>
    public void Warning(string messageTemplate, params object[] propertyValues)
    {
        _logger.Warning(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log warning with exception.
    /// </summary>
    public void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        _logger.Warning(exception, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log error level message.
    /// </summary>
    public void Error(string messageTemplate, params object[] propertyValues)
    {
        _logger.Error(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log error with exception.
    /// </summary>
    public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        _logger.Error(exception, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log fatal level message.
    /// </summary>
    public void Fatal(string messageTemplate, params object[] propertyValues)
    {
        _logger.Fatal(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log fatal with exception.
    /// </summary>
    public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        _logger.Fatal(exception, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Log an operation with timing information.
    /// </summary>
    public IDisposable BeginOperation(string operationName)
    {
        return new OperationTimer(this, operationName);
    }

    private class OperationTimer : IDisposable
    {
        private readonly LoggingService _loggingService;
        private readonly string _operationName;
        private readonly DateTime _startTime;

        public OperationTimer(LoggingService loggingService, string operationName)
        {
            _loggingService = loggingService;
            _operationName = operationName;
            _startTime = DateTime.UtcNow;
            _loggingService.Debug("Starting operation: {OperationName}", _operationName);
        }

        public void Dispose()
        {
            var duration = DateTime.UtcNow - _startTime;
            _loggingService.Debug("Completed operation: {OperationName} in {Duration}ms",
                _operationName, duration.TotalMilliseconds);
        }
    }
}
