using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Pty.Net;

namespace QingFeng.Services;

public class TerminalService : ITerminalService, IDisposable
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(ILogger<TerminalService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateSessionAsync()
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new TerminalSession(sessionId, _logger);
        
        if (_sessions.TryAdd(sessionId, session))
        {
            await session.StartAsync();
            _logger.LogInformation("Created terminal session: {SessionId}", sessionId);
            return sessionId;
        }
        
        throw new InvalidOperationException("Failed to create terminal session");
    }

    public Task WriteInputAsync(string sessionId, string input)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.WriteInput(input);
            return Task.CompletedTask;
        }
        
        throw new InvalidOperationException($"Session {sessionId} not found");
    }

    public Task<string> ReadOutputAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(session.ReadOutput());
        }
        
        throw new InvalidOperationException($"Session {sessionId} not found");
    }

    public Task ResizeTerminalAsync(string sessionId, int rows, int cols)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Resize(rows, cols);
            return Task.CompletedTask;
        }
        
        throw new InvalidOperationException($"Session {sessionId} not found");
    }

    public Task CloseSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
            _logger.LogInformation("Closed terminal session: {SessionId}", sessionId);
            return Task.CompletedTask;
        }
        
        throw new InvalidOperationException($"Session {sessionId} not found");
    }

    public bool SessionExists(string sessionId)
    {
        return _sessions.ContainsKey(sessionId);
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }

    private class TerminalSession : IDisposable
    {
        private readonly string _sessionId;
        private readonly ILogger _logger;
        private IPtyConnection? _pty;
        private const int MaxOutputBufferSize = 1024 * 1024; // 1 MB cap to prevent unbounded growth
        private readonly StringBuilder _outputBuffer = new StringBuilder();
        private readonly object _outputLock = new();
        private bool _disposed = false;
        private int _currentBufferSize = 0;
        private CancellationTokenSource? _readCts;

        public TerminalSession(string sessionId, ILogger logger)
        {
            _sessionId = sessionId;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            try
            {
                var options = new PtyOptions
                {
                    Name = "xterm-256color",
                    Cols = 80,
                    Rows = 24,
                    Cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment = new Dictionary<string, string>
                    {
                        ["TERM"] = "xterm-256color",
                        ["COLORTERM"] = "truecolor"
                    }
                };

                string app;

                if (OperatingSystem.IsWindows())
                {
                    app = "cmd.exe";
                    options.App = app;
                }
                else
                {
                    app = "/bin/bash";
                    options.App = app;
                    options.CommandLine = new[] { "-i" };
                }

                _pty = await PtyProvider.SpawnAsync(options, CancellationToken.None);
                
                _logger.LogInformation("Started PTY terminal session {SessionId} with PID {Pid}", _sessionId, _pty.Pid);

                // Start reading output asynchronously
                _readCts = new CancellationTokenSource();
                StartReadingOutput(_readCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTY terminal session {SessionId}", _sessionId);
                throw new InvalidOperationException("Failed to start terminal process", ex);
            }
        }

        private void StartReadingOutput(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[4096];
                    while (!_disposed && _pty != null && !cancellationToken.IsCancellationRequested)
                    {
                        var count = await _pty.ReaderStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (count > 0)
                        {
                            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
                            lock (_outputLock)
                            {
                                // Check if adding this data would exceed the buffer limit
                                if (_currentBufferSize + text.Length > MaxOutputBufferSize)
                                {
                                    // Remove old data from the beginning to make room
                                    var excessSize = (_currentBufferSize + text.Length) - MaxOutputBufferSize;
                                    var currentContent = _outputBuffer.ToString();
                                    _outputBuffer.Clear();
                                    _outputBuffer.Append(currentContent.Substring(excessSize));
                                    _currentBufferSize = _outputBuffer.Length;
                                    _logger.LogWarning("Terminal output buffer reached maximum size for session {SessionId}, discarding old data", _sessionId);
                                }
                                
                                _outputBuffer.Append(text);
                                _currentBufferSize += text.Length;
                            }
                        }
                        else if (count == 0)
                        {
                            // PTY closed
                            _logger.LogInformation("PTY connection closed for session {SessionId}", _sessionId);
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when disposing
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading PTY output for session {SessionId}", _sessionId);
                }
            }, cancellationToken);
        }

        public void WriteInput(string input)
        {
            if (_pty != null && !_disposed)
            {
                try
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                    _pty.WriterStream.Write(bytes, 0, bytes.Length);
                    _pty.WriterStream.Flush();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing to PTY for session {SessionId}", _sessionId);
                }
            }
        }

        public string ReadOutput()
        {
            lock (_outputLock)
            {
                var output = _outputBuffer.ToString();
                _outputBuffer.Clear();
                _currentBufferSize = 0;
                return output;
            }
        }

        public void Resize(int rows, int cols)
        {
            if (_pty != null && !_disposed)
            {
                try
                {
                    _pty.Resize(cols, rows);
                    _logger.LogDebug("Resized PTY terminal to {Cols}x{Rows} for session {SessionId}", cols, rows, _sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resizing PTY for session {SessionId}", _sessionId);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                // Cancel read operation
                _readCts?.Cancel();
                _readCts?.Dispose();
                
                // Dispose PTY connection
                _pty?.Dispose();
                
                _logger.LogInformation("Disposed PTY terminal session {SessionId}", _sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing terminal session {SessionId}", _sessionId);
            }
        }
    }
}
