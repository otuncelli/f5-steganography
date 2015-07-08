using System;
using System.IO;

namespace F5.Util
{
    internal sealed class EmbedData : IDisposable
    {
        private readonly Stream data;

        internal EmbedData(Stream data)
        {
            this.data = data;
            Seek(0, SeekOrigin.Begin);
        }
        internal EmbedData(byte[] data)
            : this(new MemoryStream(data))
        {
        }
        public byte Read()
        {
            int b = this.data.ReadByte();
            return (byte)(b == -1 ? 0 : b);
        }

        /// <summary>
        /// Read Integer from Stream
        /// </summary>
        public int ReadInt()
        {
            int b = Read();
            b <<= 8;
            b ^= Read();
            return b;
        }
        public long Available
        {
            get { return this.data.Length - this.data.Position; }
        }
        public long Length
        {
            get { return this.data.Length; }
        }
        public void Close()
        {
            this.data.Close();
        }
        public long Seek(long offset, SeekOrigin origin)
        {
            return this.data.Seek(offset, origin);
        }

        #region IDisposable
        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EmbedData()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (this._disposed)
                return;
            if (disposing)
                this.data.Dispose();
            this._disposed = true;
        }
        #endregion
    }
}
