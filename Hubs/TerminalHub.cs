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
    private const int MaxSessionsPerUser = 3;

    public TerminalHub(ITerminalService terminalService, ILogger<TerminalHub> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    public async Task<string> CreateSession()
    {
        string? sessionId = null;
        try
        {
            // Check session limit per user
            var userSessions = _connectionSessions.Values.Distinct().Count();
            if (userSessions >= MaxSessionsPerUser)
            {
                throw new InvalidOperationException($"Maximum number of terminal sessions ({MaxSessionsPerUser}) reached.");
            }

            sessionId = await _terminalService.CreateSessionAsync();
            
            _connectionSessions[Context.ConnectionId] = sessionId;
            
            _logger.LogInformation("Created terminal session {SessionId} for connection {ConnectionId}", 
                sessionId, Context.ConnectionId);
            
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
            _logger.LogError(ex, "Error creating terminal session");

            // Clean up if session was created but connection setup failed
            if (sessionId != null && _connectionSessions.TryRemove(Context.ConnectionId, out var removedSessionId))
            {
                try
                {
                    await _terminalService.CloseSessionAsync(removedSessionId);
                    _logger.LogInformation("Closed terminal session {SessionId} after failure for connection {ConnectionId}",
                        removedSessionId, Context.ConnectionId);
                }
                catch (Exception closeEx)
                {
                    _logger.LogError(closeEx, "Error closing terminal session {SessionId} after failure", removedSessionId);
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
        
        if (_connectionSessions.TryRemove(Context.ConnectionId, out sessionId))
        {
            // Cancel output task
            if (_outputTasks.TryRemove(sessionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            try
            {
                await _terminalService.CloseSessionAsync(sessionId);
                _logger.LogInformation("Closed terminal session {SessionId} for disconnected connection {ConnectionId}", 
                    sessionId, Context.ConnectionId);
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
