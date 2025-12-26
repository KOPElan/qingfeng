using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace QingFeng.Services;

public class TerminalService : ITerminalService, IDisposable
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(ILogger<TerminalService> logger)
    {
        _logger = logger;
    }

    public Task<string> CreateSessionAsync()
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new TerminalSession(sessionId, _logger);
        
        if (_sessions.TryAdd(sessionId, session))
        {
            session.Start();
            _logger.LogInformation("Created terminal session: {SessionId}", sessionId);
            return Task.FromResult(sessionId);
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
        private Process? _process;
        private readonly StringBuilder _outputBuffer = new();
        private readonly object _outputLock = new();
        private bool _disposed = false;

        public TerminalSession(string sessionId, ILogger logger)
        {
            _sessionId = sessionId;
            _logger = logger;
        }

        public void Start()
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "cmd.exe";
            }
            else
            {
                // Start bash in interactive mode
                startInfo.FileName = "/bin/bash";
                startInfo.Arguments = "-i";
                startInfo.EnvironmentVariables["TERM"] = "xterm-256color";
                startInfo.EnvironmentVariables["PS1"] = "\\u@\\h:\\w\\$ ";
            }

            _process = Process.Start(startInfo);
            
            if (_process == null)
            {
                throw new InvalidOperationException("Failed to start terminal process");
            }

            // Enable auto-flush for input
            _process.StandardInput.AutoFlush = true;

            // Read output asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    while (!_disposed && _process != null && !_process.HasExited)
                    {
                        var count = await _process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                        if (count > 0)
                        {
                            lock (_outputLock)
                            {
                                _outputBuffer.Append(buffer, 0, count);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading terminal output for session {SessionId}", _sessionId);
                }
            });

            // Read error output asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    while (!_disposed && _process != null && !_process.HasExited)
                    {
                        var count = await _process.StandardError.ReadAsync(buffer, 0, buffer.Length);
                        if (count > 0)
                        {
                            lock (_outputLock)
                            {
                                _outputBuffer.Append(buffer, 0, count);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading terminal error output for session {SessionId}", _sessionId);
                }
            });
        }

        public void WriteInput(string input)
        {
            if (_process != null && !_process.HasExited)
            {
                _process.StandardInput.Write(input);
                _process.StandardInput.Flush();
            }
        }

        public string ReadOutput()
        {
            lock (_outputLock)
            {
                var output = _outputBuffer.ToString();
                _outputBuffer.Clear();
                return output;
            }
        }

        public void Resize(int rows, int cols)
        {
            // Note: PTY resize is complex in .NET and requires platform-specific code
            // For now, we'll just log the resize request
            // A full implementation would require using pty libraries or P/Invoke
            _logger.LogDebug("Terminal resize requested: {Rows}x{Cols} for session {SessionId}", rows, cols, _sessionId);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                    _process.WaitForExit(1000);
                }
                _process?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing terminal session {SessionId}", _sessionId);
            }
        }
    }
}
