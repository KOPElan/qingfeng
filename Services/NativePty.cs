using System.Runtime.InteropServices;
using System.Text;

namespace QingFeng.Services;

/// <summary>
/// Native PTY implementation using P/Invoke for Linux and ConPTY for Windows
/// </summary>
public static class NativePty
{
    public static INativePtyConnection Create(string shell, string[] args, string workingDirectory, int cols, int rows)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixPtyConnection(shell, args, workingDirectory, cols, rows);
        }
        else if (OperatingSystem.IsWindows())
        {
            return new WindowsPtyConnection(shell, args, workingDirectory, cols, rows);
        }
        else
        {
            throw new PlatformNotSupportedException("PTY is only supported on Linux, macOS, and Windows");
        }
    }
}

public interface INativePtyConnection : IDisposable
{
    Stream OutputStream { get; }
    Stream InputStream { get; }
    int ProcessId { get; }
    void Resize(int cols, int rows);
    bool IsAlive { get; }
}

/// <summary>
/// Unix PTY implementation using forkpty
/// </summary>
internal class UnixPtyConnection : INativePtyConnection
{
    private readonly int _masterFd;
    private readonly int _pid;
    private readonly FileStream _stream;
    private bool _disposed;

    public Stream OutputStream => _stream;
    public Stream InputStream => _stream;
    public int ProcessId => _pid;
    public bool IsAlive => !_disposed && UnixNative.waitpid(_pid, IntPtr.Zero, UnixNative.WNOHANG) == 0;

    public UnixPtyConnection(string shell, string[] args, string workingDirectory, int cols, int rows)
    {
        var winsize = new UnixNative.Winsize
        {
            ws_row = (ushort)rows,
            ws_col = (ushort)cols
        };

        // Fork a new process with PTY
        _pid = UnixNative.forkpty(out _masterFd, IntPtr.Zero, IntPtr.Zero, ref winsize);

        if (_pid < 0)
        {
            throw new InvalidOperationException($"forkpty failed with error: {Marshal.GetLastWin32Error()}");
        }

        if (_pid == 0)
        {
            // Child process
            try
            {
                // Change working directory
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    Directory.SetCurrentDirectory(workingDirectory);
                }

                // Set environment
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");
                Environment.SetEnvironmentVariable("COLORTERM", "truecolor");

                // Execute shell
                var allArgs = new[] { shell }.Concat(args).ToArray();
                UnixNative.execvp(shell, allArgs);
                
                // If execvp returns, it failed
                Environment.Exit(1);
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        // Parent process - create stream for master FD
        _stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)_masterFd, true), FileAccess.ReadWrite, 4096);
    }

    public void Resize(int cols, int rows)
    {
        var winsize = new UnixNative.Winsize
        {
            ws_row = (ushort)rows,
            ws_col = (ushort)cols
        };

        UnixNative.ioctl(_masterFd, UnixNative.TIOCSWINSZ, ref winsize);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // Kill the child process
            UnixNative.kill(_pid, UnixNative.SIGTERM);
            
            // Wait a bit for graceful shutdown
            Thread.Sleep(100);
            
            // Force kill if still alive
            if (IsAlive)
            {
                UnixNative.kill(_pid, UnixNative.SIGKILL);
            }

            // Wait for process to exit
            UnixNative.waitpid(_pid, IntPtr.Zero, 0);
        }
        catch
        {
            // Ignore errors during cleanup
        }

        _stream?.Dispose();
    }
}

/// <summary>
/// Windows PTY implementation using Process (simplified for now)
/// </summary>
internal class WindowsPtyConnection : INativePtyConnection
{
    private readonly System.Diagnostics.Process _process;
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private bool _disposed;

    public Stream OutputStream => _outputStream;
    public Stream InputStream => _inputStream;
    public int ProcessId => _process.Id;
    public bool IsAlive => !_disposed && !_process.HasExited;

    public WindowsPtyConnection(string shell, string[] args, string workingDirectory, int cols, int rows)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = shell,
            Arguments = string.Join(" ", args),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["TERM"] = "xterm-256color";

        _process = System.Diagnostics.Process.Start(startInfo) 
            ?? throw new InvalidOperationException("Failed to start process");

        _inputStream = _process.StandardInput.BaseStream;
        
        // Merge stdout and stderr
        var mergedStream = new MergedStream(_process.StandardOutput.BaseStream, _process.StandardError.BaseStream);
        _outputStream = mergedStream;
    }

    public void Resize(int cols, int rows)
    {
        // Windows resize would require ConPTY API which is complex
        // For now, just log the resize request
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
                _process.WaitForExit(1000);
            }
        }
        catch
        {
            // Ignore errors
        }

        _process?.Dispose();
    }
}

/// <summary>
/// Helper class to merge two streams
/// </summary>
internal class MergedStream : Stream
{
    private readonly Stream _stream1;
    private readonly Stream _stream2;
    private readonly byte[] _buffer1 = new byte[4096];
    private readonly byte[] _buffer2 = new byte[4096];
    private readonly Queue<byte> _queue = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readTask1;
    private readonly Task _readTask2;
    private bool _disposed;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public MergedStream(Stream stream1, Stream stream2)
    {
        _stream1 = stream1;
        _stream2 = stream2;
        _readTask1 = StartReadingAsync(_stream1, _buffer1, _cts.Token);
        _readTask2 = StartReadingAsync(_stream2, _buffer2, _cts.Token);
    }

    private async Task StartReadingAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (count == 0)
                {
                    // End of stream
                    break;
                }
                
                if (count > 0)
                {
                    lock (_lock)
                    {
                        for (int i = 0; i < count; i++)
                            _queue.Enqueue(buffer[i]);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch
        {
            // Ignore other errors
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            int bytesRead = 0;
            while (bytesRead < count && _queue.Count > 0)
            {
                buffer[offset + bytesRead] = _queue.Dequeue();
                bytesRead++;
            }
            return bytesRead;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cts.Cancel();
                try
                {
                    Task.WaitAll(new[] { _readTask1, _readTask2 }, TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // Ignore timeout or task errors
                }
                _cts.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// Unix native functions
/// </summary>
internal static class UnixNative
{
    public const int TIOCSWINSZ = 0x5414;
    public const int WNOHANG = 1;
    public const int SIGTERM = 15;
    public const int SIGKILL = 9;

    [StructLayout(LayoutKind.Sequential)]
    public struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    [DllImport("libc", SetLastError = true)]
    public static extern int forkpty(out int master, IntPtr name, IntPtr termp, ref Winsize winsize);

    [DllImport("libc", SetLastError = true)]
    public static extern int execvp(string file, string[] argv);

    [DllImport("libc", SetLastError = true)]
    public static extern int ioctl(int fd, int request, ref Winsize winsize);

    [DllImport("libc", SetLastError = true)]
    public static extern int waitpid(int pid, IntPtr status, int options);

    [DllImport("libc", SetLastError = true)]
    public static extern int kill(int pid, int signal);
}
