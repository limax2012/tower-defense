using System.Diagnostics;
using System.Text;

namespace MaximalBastion.Diagnostics;

/// <summary>Streams RGBA frames into an MP4 encoder without retaining a frame sequence in memory.</summary>
public sealed class ReplayVideoEncoder : IDisposable
{
    private const int MaximumDiagnosticCharacters = 16_384;
    private readonly Process _process;
    private readonly Task _errorReader;
    private readonly StringBuilder _diagnostics = new();
    private readonly object _diagnosticLock = new();
    private readonly string _temporaryPath;
    private readonly int _frameBytes;
    private bool _completed;
    private bool _disposed;

    public string OutputPath { get; }
    public long FramesWritten { get; private set; }

    public ReplayVideoEncoder(string ffmpegPath, string outputPath, int width, int height, int framesPerSecond)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (width is < 2 or > 8192 || height is < 2 or > 8192 || width % 2 != 0 || height % 2 != 0 ||
            framesPerSecond is < 1 or > 240)
            throw new ArgumentException("Video dimensions must be positive even values up to 8192 and frame rate must be between 1 and 240.");
        OutputPath = Path.GetFullPath(outputPath);
        if (!Path.GetExtension(OutputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Replay video output must use the .mp4 extension.", nameof(outputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        _temporaryPath = Path.Combine(Path.GetDirectoryName(OutputPath)!,
            $".{Path.GetFileNameWithoutExtension(OutputPath)}.{Guid.NewGuid():N}.partial.mp4");
        _frameBytes = checked(width * height * 4);
        var start = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostats", "-y",
            "-f", "rawvideo", "-pixel_format", "rgba", "-video_size", $"{width}x{height}",
            "-framerate", framesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "fast", "-crf", "20",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-f", "mp4", _temporaryPath
        }) start.ArgumentList.Add(argument);
        _process = new Process { StartInfo = start };
        try
        {
            if (!_process.Start()) throw new IOException("The video encoder could not be started.");
            _errorReader = ReadDiagnostics(_process.StandardError);
        }
        catch
        {
            _process.Dispose();
            throw;
        }
    }

    public void WriteFrame(ReadOnlySpan<byte> rgba)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) throw new InvalidOperationException("The replay video is already complete.");
        if (rgba.Length != _frameBytes) throw new ArgumentException($"Expected exactly {_frameBytes} RGBA bytes.", nameof(rgba));
        if (_process.HasExited) throw EncoderFailure("The video encoder stopped before all frames were written.");
        try
        {
            _process.StandardInput.BaseStream.Write(rgba);
            FramesWritten++;
        }
        catch (IOException exception)
        {
            throw EncoderFailure("The video encoder could not accept a frame.", exception);
        }
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) return;
        if (FramesWritten == 0) throw new InvalidOperationException("A replay video must contain at least one frame.");
        _process.StandardInput.Close();
        if (!_process.WaitForExit(60_000))
        {
            StopEncoder();
            throw EncoderFailure("The video encoder did not finish within 60 seconds after its final frame.");
        }
        _errorReader.GetAwaiter().GetResult();
        if (_process.ExitCode != 0 || !File.Exists(_temporaryPath) || new FileInfo(_temporaryPath).Length == 0)
            throw EncoderFailure($"The video encoder failed with exit code {_process.ExitCode}.");
        File.Move(_temporaryPath, OutputPath, overwrite: true);
        _completed = true;
    }

    private async Task ReadDiagnostics(StreamReader reader)
    {
        var buffer = new char[2048];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                lock (_diagnosticLock)
                {
                    _diagnostics.Append(buffer, 0, count);
                    if (_diagnostics.Length > MaximumDiagnosticCharacters)
                        _diagnostics.Remove(0, _diagnostics.Length - MaximumDiagnosticCharacters);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException) { }
    }

    private IOException EncoderFailure(string message, Exception? inner = null)
    {
        string details;
        lock (_diagnosticLock) details = _diagnostics.ToString().Trim();
        return new IOException(details.Length == 0 ? message : message + Environment.NewLine + details, inner);
    }

    private void StopEncoder()
    {
        if (_process.HasExited) return;
        _process.Kill(entireProcessTree: true);
        _process.WaitForExit(5000);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_completed) StopEncoder();
        }
        finally
        {
            _process.Dispose();
            if (!_completed && File.Exists(_temporaryPath)) File.Delete(_temporaryPath);
        }
    }
}
