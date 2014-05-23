using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace DarkAutumn.Twitch
{
    // From IrcDotNet.
    internal class CircularBufferStream : Stream
    {
        byte[] m_buffer;
        long m_writePosition;
        long m_readPosition;

        public CircularBufferStream(int length)
            : this(new byte[length])
        {
        }

        public CircularBufferStream(byte[] buffer)
        {
            m_buffer = buffer;
            m_writePosition = 0;
            m_readPosition = 0;
        }

        public byte[] Buffer
        {
            get { return m_buffer; }
        }

        public long WritePosition
        {
            get { return m_writePosition; }
            set { m_writePosition = value % m_buffer.Length; }
        }

        public override long Position
        {
            get { return m_readPosition; }
            set { m_readPosition = value % m_buffer.Length; }
        }

        public override long Length
        {
            get
            {
                var length = m_writePosition - m_readPosition;
                return length < 0 ? m_buffer.Length + length : length;
            }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Write block of bytes from given buffer into circular buffer, wrapping around when necessary.
            int writeCount;
            while ((writeCount = Math.Min(count, (int)(m_buffer.Length - m_writePosition))) > 0)
            {
                var oldWritePosition = m_writePosition;
                var newWritePosition = (m_writePosition + writeCount) % m_buffer.Length;
                if (newWritePosition > m_readPosition && oldWritePosition < m_readPosition)
                {
                    throw new InternalBufferOverflowException("Der CircularBuffer wurde überlaufen!");
                }
                System.Buffer.BlockCopy(buffer, offset, m_buffer, (int)m_writePosition, writeCount);
                m_writePosition = newWritePosition;
               
                offset += writeCount;
                count -= writeCount; //writeCount <= count => now is count >=0
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read block of bytes from circular buffer, wrapping around when necessary.
            int totalReadCount = 0;
            int readCount;
            count = Math.Min(buffer.Length - offset, count);
            while ((readCount = Math.Min(count, (int)(Length))) > 0)
            {
                if (readCount > m_buffer.Length - m_readPosition)
                {
                    readCount = (int)(m_buffer.Length - m_readPosition);
                }
                System.Buffer.BlockCopy(m_buffer, (int)m_readPosition, buffer, offset, readCount);
                m_readPosition = (m_readPosition + readCount) % m_buffer.Length;
                offset += readCount;
                count = Math.Min(buffer.Length - offset, count);
                totalReadCount += readCount;
            }
            return totalReadCount;
        }
    }

    internal class SafeLineReader
    {
        TextReader m_reader;
        StringBuilder m_curr = new StringBuilder(1024);

        public SafeLineReader(TextReader textReader)
        {
            m_reader = textReader;
        }

        // Reads line from source, ensuring that line is not returned unless it terminates with line break.
        public string ReadLine()
        {
            int nextChar;

            while (true)
            {
                // Check whether to stop reading characters.
                nextChar = m_reader.Peek();
                if (nextChar == -1)
                    break;

                if (nextChar == '\r' || nextChar == '\n')
                {
                    m_reader.Read();
                    if (m_reader.Peek() == '\n')
                        m_reader.Read();

                    var line = m_curr.ToString();
                    m_curr.Clear();
                    return line;
                }

                // Append next character to line.
                m_curr.Append((char)m_reader.Read());
            }

            return null;
        }
    }
}
