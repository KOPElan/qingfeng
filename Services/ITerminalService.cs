namespace QingFeng.Services;

public interface ITerminalService
{
    /// <summary>
    /// Creates a new terminal session and returns the session ID
    /// </summary>
    Task<string> CreateSessionAsync();
    
    /// <summary>
    /// Writes input to the terminal session
    /// </summary>
    Task WriteInputAsync(string sessionId, string input);
    
    /// <summary>
    /// Reads output from the terminal session
    /// </summary>
    Task<string> ReadOutputAsync(string sessionId);
    
    /// <summary>
    /// Resizes the terminal
    /// </summary>
    Task ResizeTerminalAsync(string sessionId, int rows, int cols);
    
    /// <summary>
    /// Closes the terminal session
    /// </summary>
    Task CloseSessionAsync(string sessionId);
    
    /// <summary>
    /// Checks if a session exists
    /// </summary>
    bool SessionExists(string sessionId);
}
