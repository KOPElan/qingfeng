using Microsoft.AspNetCore.SignalR;
using QingFeng.Services;

namespace QingFeng.Hubs;

public class TerminalHub : Hub
{
    private readonly ITerminalService _terminalService;
    private readonly ILogger<TerminalHub> _logger;
    private static readonly Dictionary<string, string> _connectionSessions = new();
    private static readonly object _sessionsLock = new();

    public TerminalHub(ITerminalService terminalService, ILogger<TerminalHub> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    public async Task<string> CreateSession()
    {
        try
        {
            var sessionId = await _terminalService.CreateSessionAsync();
            
            lock (_sessionsLock)
            {
                _connectionSessions[Context.ConnectionId] = sessionId;
            }
            
            _logger.LogInformation("Created terminal session {SessionId} for connection {ConnectionId}", 
                sessionId, Context.ConnectionId);
            
            // Start sending output to client
            _ = Task.Run(async () => await SendOutputToClient(sessionId));
            
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating terminal session");
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
        
        lock (_sessionsLock)
        {
            if (_connectionSessions.TryGetValue(Context.ConnectionId, out sessionId))
            {
                _connectionSessions.Remove(Context.ConnectionId);
            }
        }

        if (sessionId != null)
        {
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

    private async Task SendOutputToClient(string sessionId)
    {
        try
        {
            while (_terminalService.SessionExists(sessionId))
            {
                var output = await _terminalService.ReadOutputAsync(sessionId);
                if (!string.IsNullOrEmpty(output))
                {
                    await Clients.Caller.SendAsync("ReceiveOutput", output);
                }
                await Task.Delay(50); // Poll every 50ms
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending output to client for session {SessionId}", sessionId);
        }
    }
}
