namespace StorageServer.Helpers;

public sealed class ChunkedStream : Stream
{
    private readonly Stream inner;
    private readonly byte[] lineBuffer = new byte[4096];

    private int chunkRemaining;
    private bool finished;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public ChunkedStream(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    protected override void Dispose(bool disposing)
    {
        // The inner stream is owned by the request pipeline; do not dispose it.
        base.Dispose(disposing);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (finished || buffer.Length == 0)
        {
            return 0;
        }

        if (chunkRemaining == 0)
        {
            var headerLine = await ReadLineAsync(cancellationToken);
            if (headerLine is null)
            {
                finished = true;
                return 0;
            }

            while (headerLine.Length == 0)
            {
                headerLine = await ReadLineAsync(cancellationToken);
                if (headerLine is null)
                {
                    finished = true;
                    return 0;
                }
            }

            // Parse: {hex_size}[;extensions...]
            var semiIdx = headerLine.IndexOf(';', StringComparison.Ordinal);
            var hexPart = semiIdx >= 0 ? headerLine[..semiIdx] : headerLine;
            chunkRemaining = Convert.ToInt32(hexPart, 16);

            if (chunkRemaining == 0)
            {
                finished = true;
                return 0;
            }
        }

        var toRead = Math.Min(buffer.Length, chunkRemaining);
        var bytesRead = await inner.ReadAsync(buffer[..toRead], cancellationToken);
        if (bytesRead == 0)
        {
            finished = true;
            return 0;
        }

        chunkRemaining -= bytesRead;
        return bytesRead;
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var pos = 0;
        while (pos < lineBuffer.Length)
        {
            var buf = new byte[1];
            var n = await inner.ReadAsync(buf.AsMemory(0, 1), cancellationToken);
            if (n == 0)
            {
                return pos > 0 ? System.Text.Encoding.ASCII.GetString(lineBuffer, 0, pos) : null;
            }

            var b = buf[0];
            if (b == '\r')
            {
                await inner.ReadAsync(buf.AsMemory(0, 1), cancellationToken);
                return System.Text.Encoding.ASCII.GetString(lineBuffer, 0, pos);
            }

            lineBuffer[pos++] = b;
        }

        return System.Text.Encoding.ASCII.GetString(lineBuffer, 0, pos);
    }
}
