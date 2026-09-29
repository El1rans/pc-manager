using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>In-memory <see cref="IFileContentReader"/> that also counts how many bytes of each file
/// were actually read, so tests can prove the staged comparison avoids needless reads.</summary>
internal sealed class FakeFileContentReader : IFileContentReader
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _locked = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, long> BytesRead { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Opened { get; } = [];

    /// <summary>Runs on every read, before data is returned (used to cancel mid-file).</summary>
    public Action<string>? OnRead { get; set; }

    public FakeFileContentReader Add(string path, byte[] content)
    {
        _files[path] = content;
        return this;
    }

    public FakeFileContentReader Lock(string path)
    {
        _locked.Add(path);
        return this;
    }

    public long TotalBytesRead => BytesRead.Values.Sum();

    public Stream OpenRead(string path)
    {
        Opened.Add(path);
        if (_locked.Contains(path))
        {
            throw new IOException("The process cannot access the file because it is being used by another process.");
        }

        return new CountingStream(this, path, new MemoryStream(_files[path], writable: false));
    }

    private sealed class CountingStream(FakeFileContentReader owner, string path, Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            owner.OnRead?.Invoke(path);
            var read = inner.Read(buffer, offset, count);
            owner.BytesRead[path] = owner.BytesRead.GetValueOrDefault(path) + read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
