using System.IO.Pipes;
using System.Text;

namespace OPLauncher.Utilities;

/// <summary>
/// Manages single-instance enforcement for the launcher.
/// Ensures only one instance of the launcher runs at a time.
/// When a deep link is activated and an instance already exists,
/// the deep link is sent to the existing instance via named pipes.
/// </summary>
public class SingleInstanceManager : IDisposable
{
    private const string MutexName = "OldPortalLauncher_SingleInstance_Mutex";
    private const string PipeName = "OldPortalLauncher_IPC_Pipe";
    private readonly Mutex _mutex;
    private readonly bool _isFirstInstance;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    /// <summary>
    /// Event raised when a deep link is received from another instance.
    /// </summary>
    public event EventHandler<string>? DeepLinkReceived;

    /// <summary>
    /// Gets whether this is the first (primary) instance of the application.
    /// </summary>
    public bool IsFirstInstance => _isFirstInstance;

    /// <summary>
    /// Initializes a new instance of the SingleInstanceManager.
    /// </summary>
    public SingleInstanceManager()
    {
        _mutex = new Mutex(true, MutexName, out _isFirstInstance);
    }

    /// <summary>
    /// Starts listening for deep links from other instances.
    /// Should only be called if this is the first instance.
    /// </summary>
    public void StartListening()
    {
        if (!_isFirstInstance)
        {
            throw new InvalidOperationException("Only the first instance should listen for messages.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        Task.Run(() => ListenForDeepLinksAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Sends a deep link to the existing instance.
    /// Should only be called if this is NOT the first instance.
    /// </summary>
    /// <param name="deepLink">The deep link URL to send.</param>
    /// <returns>True if successfully sent, false otherwise.</returns>
    public async Task<bool> SendDeepLinkToExistingInstanceAsync(string deepLink)
    {
        if (_isFirstInstance)
        {
            throw new InvalidOperationException("Cannot send to self - this is the first instance.");
        }

        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);

            // Try to connect with a 5-second timeout
            await pipeClient.ConnectAsync(5000);

            var message = Encoding.UTF8.GetBytes(deepLink);
            await pipeClient.WriteAsync(message, 0, message.Length);
            await pipeClient.FlushAsync();

            return true;
        }
        catch (TimeoutException)
        {
            // Existing instance didn't respond in time
            return false;
        }
        catch (IOException)
        {
            // Pipe communication error
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Listens for deep links sent from other instances via named pipes.
    /// </summary>
    private async Task ListenForDeepLinksAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Create a new pipe server for each connection
                _pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // Wait for a client to connect
                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                // Read the message
                var buffer = new byte[1024];
                var bytesRead = await _pipeServer.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead > 0)
                {
                    var deepLink = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Raise the event on the UI thread
                    DeepLinkReceived?.Invoke(this, deepLink);
                }

                // Disconnect and dispose the current server
                _pipeServer.Disconnect();
                _pipeServer.Dispose();
                _pipeServer = null;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception)
            {
                // Log errors if needed, but continue listening
                // Sleep briefly to avoid tight loop on persistent errors
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the SingleInstanceManager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _pipeServer?.Dispose();

        if (_isFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();

        _disposed = true;
    }
}
