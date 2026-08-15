using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text.Json;
using System.Threading.Channels;

namespace MinimalBastion.Multiplayer;

public enum CoOpMessageType
{
    Hello,
    Welcome,
    Rejected,
    CommandRequest,
    CommandReceipt,
    TickSync,
    Ready,
    StartSession,
    AuthoritativeCommand,
    WaveReady,
    Ping,
    StateSnapshot,
    ResyncRequest,
    RestartRequest,
    Disconnect,
    Cursor
}

public sealed record CoOpEnvelope
{
    public const int CurrentProtocolVersion = 8;
    public CoOpMessageType Type { get; init; }
    public int ProtocolVersion { get; init; } = CurrentProtocolVersion;
    public string JoinCode { get; init; } = "";
    public string BuildFingerprint { get; init; } = "";
    public int PlayerId { get; init; }
    public string Message { get; init; } = "";
    public GameCommand? Command { get; init; }
    public CommandReceipt? Receipt { get; init; }
    public long Tick { get; init; }
    public string Checksum { get; init; } = "";
    public bool Ready { get; init; }
    public int ReadyMask { get; init; }
    public bool EarlyBonus { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int EntityId { get; init; }
    public CoOpStateSnapshot? State { get; init; }
}

public sealed class LanCoOpConnection : IAsyncDisposable
{
    public const int MaximumMessageBytes = 2_097_152;
    public const int MaximumQueuedSends = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Channel<PendingSend> _sendQueue = Channel.CreateUnbounded<PendingSend>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    private readonly Task _sendPump;
    private int _disposeState;
    private int _queuedSendCount;

    internal LanCoOpConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _sendPump = PumpSendsAsync();
    }

    public bool Connected => _client.Connected;

    public async Task SendAsync(CoOpEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        if (Interlocked.Increment(ref _queuedSendCount) > MaximumQueuedSends)
        {
            Interlocked.Decrement(ref _queuedSendCount);
            throw new IOException("Co-op outbound queue exceeded its safety limit.");
        }
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
            if (payload.Length > MaximumMessageBytes) throw new InvalidDataException("Co-op message exceeds the protocol limit.");
            var frame = new byte[payload.Length + sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, sizeof(int)), payload.Length);
            payload.CopyTo(frame.AsSpan(sizeof(int)));
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _sendQueue.Writer.WriteAsync(new PendingSend(frame, completion, cancellationToken), cancellationToken);
            await completion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _queuedSendCount);
        }
    }

    public async Task<CoOpEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var header = new byte[sizeof(int)];
        var firstByte = await _stream.ReadAsync(header.AsMemory(0, 1), cancellationToken);
        if (firstByte == 0) return null;
        await _stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken);
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(header);
        if (payloadLength <= 0 || payloadLength > MaximumMessageBytes)
            throw new InvalidDataException("Co-op message exceeds the protocol limit.");
        var payload = new byte[payloadLength];
        await _stream.ReadExactlyAsync(payload, cancellationToken);
        var envelope = JsonSerializer.Deserialize<CoOpEnvelope>(payload, JsonOptions)
            ?? throw new InvalidDataException("Co-op message was empty.");
        if (envelope.ProtocolVersion != CoOpEnvelope.CurrentProtocolVersion)
            throw new InvalidDataException($"Unsupported co-op protocol {envelope.ProtocolVersion}.");
        return envelope;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
        _client.Close();
        _sendQueue.Writer.TryComplete();
        try { await _sendPump; }
        catch { }
    }

    private async Task PumpSendsAsync()
    {
        Exception? failure = null;
        try
        {
            await foreach (var pending in _sendQueue.Reader.ReadAllAsync())
            {
                if (pending.CancellationToken.IsCancellationRequested)
                {
                    pending.Completion.TrySetCanceled(pending.CancellationToken);
                    continue;
                }
                await _stream.WriteAsync(pending.Frame, pending.CancellationToken);
                pending.Completion.TrySetResult();
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            while (_sendQueue.Reader.TryRead(out var pending))
            {
                if (failure is null) pending.Completion.TrySetCanceled();
                else pending.Completion.TrySetException(failure);
            }
        }
    }

    private readonly record struct PendingSend(byte[] Frame, TaskCompletionSource Completion, CancellationToken CancellationToken);
}

public sealed class LanCoOpHost : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly string _buildFingerprint;
    public string JoinCode { get; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public LanCoOpHost(int port = 0, string? joinCode = null, string buildFingerprint = "")
    {
        _listener = new TcpListener(IPAddress.IPv6Any, port);
        _listener.Server.DualMode = true;
        JoinCode = string.IsNullOrWhiteSpace(joinCode) ? CreateJoinCode() : joinCode.Trim().ToUpperInvariant();
        _buildFingerprint = buildFingerprint;
    }

    public void Start() => _listener.Start(1);

    public async Task<LanCoOpConnection> AcceptPlayerAsync(CancellationToken cancellationToken = default)
    {
        var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        var connection = new LanCoOpConnection(client);
        try
        {
            var hello = await connection.ReceiveAsync(cancellationToken);
            if (hello is not { Type: CoOpMessageType.Hello } ||
                !string.Equals(hello.JoinCode, JoinCode, StringComparison.OrdinalIgnoreCase))
            {
                await connection.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.Rejected, Message = "Invalid local join code." }, cancellationToken);
                throw new InvalidDataException("A client supplied an invalid local join code.");
            }
            if (!string.IsNullOrEmpty(_buildFingerprint) &&
                !string.Equals(hello.BuildFingerprint, _buildFingerprint, StringComparison.Ordinal))
            {
                await connection.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.Rejected, Message = "The host and client builds or content files do not match." }, cancellationToken);
                throw new InvalidDataException("A client supplied an incompatible build fingerprint.");
            }

            await connection.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.Welcome, PlayerId = 2, Message = "Connected to Minimal Bastion host." }, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _listener.Stop();
        return ValueTask.CompletedTask;
    }

    private static string CreateJoinCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        Span<char> code = stackalloc char[6];
        for (var index = 0; index < code.Length; index++) code[index] = alphabet[bytes[index] % alphabet.Length];
        return new string(code);
    }
}

public static class LanCoOpClient
{
    public static Task<LanCoOpConnection> ConnectAsync(int port, string joinCode, CancellationToken cancellationToken = default)
        => ConnectAsync("localhost", port, joinCode, cancellationToken);

    public static async Task<LanCoOpConnection> ConnectAsync(string host, int port, string joinCode, CancellationToken cancellationToken = default, string buildFingerprint = "")
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("A host address is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        var client = new TcpClient { NoDelay = true };
        LanCoOpConnection? connection = null;
        try
        {
            await client.ConnectAsync(host.Trim(), port, cancellationToken);
            connection = new LanCoOpConnection(client);
            await connection.SendAsync(new CoOpEnvelope
            {
                Type = CoOpMessageType.Hello,
                JoinCode = joinCode.Trim().ToUpperInvariant(),
                BuildFingerprint = buildFingerprint
            }, cancellationToken);
            var welcome = await connection.ReceiveAsync(cancellationToken);
            if (welcome is not { Type: CoOpMessageType.Welcome, PlayerId: 2 })
                throw new InvalidDataException(welcome?.Message ?? "Host closed the co-op handshake.");
            return connection;
        }
        catch
        {
            if (connection is not null) await connection.DisposeAsync();
            else client.Dispose();
            throw;
        }
    }
}

public readonly record struct OnlineHostEndpoint(string Host, int Port)
{
    public static OnlineHostEndpoint Parse(string value, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException("Enter the host's public IP address or DNS name.");
        if (defaultPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(defaultPort));

        var text = value.Trim();
        if (text.StartsWith("[", StringComparison.Ordinal))
        {
            var close = text.IndexOf(']');
            if (close <= 1) throw new FormatException("The IPv6 host address is invalid.");
            var host = text[1..close];
            if (close == text.Length - 1) return new OnlineHostEndpoint(host, defaultPort);
            if (text[close + 1] != ':' || !TryPort(text[(close + 2)..], out var bracketPort))
                throw new FormatException("The host port is invalid.");
            return new OnlineHostEndpoint(host, bracketPort);
        }

        var firstColon = text.IndexOf(':');
        var lastColon = text.LastIndexOf(':');
        if (firstColon > 0 && firstColon == lastColon)
        {
            if (!TryPort(text[(lastColon + 1)..], out var explicitPort))
                throw new FormatException("The host port is invalid.");
            text = text[..lastColon];
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("The host address is invalid.");
            return new OnlineHostEndpoint(text, explicitPort);
        }

        return new OnlineHostEndpoint(text, defaultPort);
    }

    private static bool TryPort(string value, out int port) =>
        int.TryParse(value, out port) && port is >= 1 and <= 65535;
}
