using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using QingFeng.Services;
using System.Collections.Concurrent;

namespace QingFeng.Hubs;

[Authorize(Roles = "Admin")]
public class TerminalHub : Hub
{
    private readonly ITerminalService _terminalService;
    private readonly ILogger<TerminalHub> _logger;
    private static readonly ConcurrentDictionary<string, string> _connectionSessions = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _outputTasks = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();
    private static readonly object _userSessionsLock = new();
    private const int MaxSessionsPerUser = 3;

    public TerminalHub(ITerminalService terminalService, ILogger<TerminalHub> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    public async Task<string> CreateSession()
    {
        string? sessionId = null;
        var username = Context.User?.Identity?.Name ?? "anonymous";
        
        try
        {
            // Check session limit per user
            lock (_userSessionsLock)
            {
                if (_userSessions.TryGetValue(username, out var sessions) && sessions.Count >= MaxSessionsPerUser)
                {
                    throw new InvalidOperationException($"Maximum number of terminal sessions ({MaxSessionsPerUser}) reached for user {username}.");
                }
            }

            sessionId = await _terminalService.CreateSessionAsync();
            
            _connectionSessions[Context.ConnectionId] = sessionId;
            
            // Track session per user
            lock (_userSessionsLock)
            {
                if (!_userSessions.TryGetValue(username, out var sessions))
                {
                    sessions = new HashSet<string>();
                    _userSessions[username] = sessions;
                }
                sessions.Add(sessionId);
            }
            
            _logger.LogInformation("Created terminal session {SessionId} for user {Username} connection {ConnectionId}", 
                sessionId, username, Context.ConnectionId);
            
            // Start sending output to client with cancellation token
            var cts = new CancellationTokenSource();
            _outputTasks[sessionId] = cts;
            
            var sendOutputTask = Task.Run(async () => await SendOutputToClient(sessionId, cts.Token), cts.Token);
            _ = sendOutputTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Unhandled exception in background send output task for session {SessionId}", sessionId);
                }
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating terminal session for user {Username}", username);

            // Clean up if session was created but connection setup failed
            if (sessionId != null)
            {
                if (_connectionSessions.TryRemove(Context.ConnectionId, out var removedSessionId))
                {
                    try
                    {
                        await _terminalService.CloseSessionAsync(removedSessionId);
                        
                        // Remove from user sessions tracking
                        lock (_userSessionsLock)
                        {
                            if (_userSessions.TryGetValue(username, out var sessions))
                            {
                                sessions.Remove(removedSessionId);
                                if (sessions.Count == 0)
                                {
                                    _userSessions.TryRemove(username, out _);
                                }
                            }
                        }
                        
                        _logger.LogInformation("Closed terminal session {SessionId} after failure for user {Username}",
                            removedSessionId, username);
                    }
                    catch (Exception closeEx)
                    {
                        _logger.LogError(closeEx, "Error closing terminal session {SessionId} after failure", removedSessionId);
                    }
                }
            }

            throw;
        }
    }

    public async Task SendInput(string sessionId, string input)
    {
        try
        {
            await _terminalService.WriteInputAsync(sessionId, input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending input to session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task ResizeTerminal(string sessionId, int rows, int cols)
    {
        try
        {
            await _terminalService.ResizeTerminalAsync(sessionId, rows, cols);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing terminal for session {SessionId}", sessionId);
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? sessionId = null;
        var username = Context.User?.Identity?.Name ?? "anonymous";
        
        if (_connectionSessions.TryRemove(Context.ConnectionId, out sessionId))
        {
            // Cancel output task
            if (_outputTasks.TryRemove(sessionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            // Remove from user sessions tracking
            lock (_userSessionsLock)
            {
                if (_userSessions.TryGetValue(username, out var sessions))
                {
                    sessions.Remove(sessionId);
                    if (sessions.Count == 0)
                    {
                        _userSessions.TryRemove(username, out _);
                    }
                }
            }

            try
            {
                await _terminalService.CloseSessionAsync(sessionId);
                _logger.LogInformation("Closed terminal session {SessionId} for user {Username} disconnected connection {ConnectionId}", 
                    sessionId, username, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing terminal session {SessionId}", sessionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendOutputToClient(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            while (_terminalService.SessionExists(sessionId) && !cancellationToken.IsCancellationRequested)
            {
                var output = await _terminalService.ReadOutputAsync(sessionId);
                if (!string.IsNullOrEmpty(output))
                {
                    await Clients.Caller.SendAsync("ReceiveOutput", output, cancellationToken);
                    // When there is output, continue immediately to keep latency low
                    continue;
                }
                
                // When there is no output, back off to reduce CPU and I/O load
                await Task.Delay(200, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection is closed
            _logger.LogDebug("Output polling cancelled for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending output to client for session {SessionId}", sessionId);
        }
    }
}
