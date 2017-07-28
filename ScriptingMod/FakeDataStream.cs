using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod
{

    /// <summary>
    /// Wraps a real data stream and allows instructing the stream to fake data while reading from it.
    /// </summary>
    class FakeDataStream : Stream, IDisposable
    {
        private Stream _baseStream;
        private byte[] _fakeData;
        private int _fakeDataPos;

        public FakeDataStream(Stream stream)
        {
            _baseStream = stream;
        }

        /// <summary>
        /// Instructs the stream to fake data in the following read(s)
        /// </summary>
        /// <param name="bytesToRead">The bytes that the read() command should return instead of the original</param>
        /// <param name="delayBytes">When to start returning the fake data; for example:
        /// 0 = start immediately with the next read byte,
        /// 4 = return four REAL bytes from the stream and then return the fake data instead</param>
        public void FakeRead(byte[] bytesToRead, int delayBytes = 0)
        {
            _fakeData = bytesToRead;
            _fakeDataPos = -1 * delayBytes;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _baseStream.Read(buffer, offset, count);

            // Advance fake data position according to number of bytes read
            if (_fakeData != null && _fakeDataPos < _fakeData.Length)
            {
                // Replace buffer bytes with our fake data
                if (_fakeDataPos >= 0)
                {
                    var fakeDataLength = Math.Min(bytesRead, _fakeData.Length - _fakeDataPos);
                    Array.Copy(_fakeData, _fakeDataPos, buffer, offset, fakeDataLength);
                }
                _fakeDataPos += bytesRead;
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);

            // Advance fake data position according to number of bytes written
            if (_fakeData != null && _fakeDataPos < _fakeData.Length)
                _fakeDataPos += count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _fakeData = null; // seeking resets fake status
            return _baseStream.Seek(offset, origin);
        }

        public override long Position
        {
            get { return _baseStream.Position; }
            set
            {
                _fakeData = null; // seeking resets fake status
                _baseStream.Position = value;
            }
        }

        public new void Dispose()
        {
            if (_baseStream != null)
            {
                _baseStream.Dispose();
            }
            base.Dispose();
        }

        #region Delegate everything else to the _baseStream

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override bool CanRead
        {
            get { return _baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _baseStream.CanWrite; }
        }

        public override long Length
        {
            get { return _baseStream.Length; }
        }

        #endregion

    }
}
