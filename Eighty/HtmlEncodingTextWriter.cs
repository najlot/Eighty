using System;
using System.Buffers;
using System.Globalization;
using System.IO;

namespace Eighty
{
    /// <summary>
    /// A mutable struct, be careful.
    /// 
    /// NB. Any changes to this file need to be paralleled in AsyncHtmlEncodingTextWriter.
    /// 
    /// See also https://github.com/dotnet/corefx/blob/3de3cd74ce3d81d13f75928eae728fb7945b6048/src/System.Runtime.Extensions/src/System/Net/WebUtility.cs
    /// </summary>
    internal struct HtmlEncodingTextWriter
    {
        private readonly TextWriter _underlyingWriter;
        private char[] _buffer;
        private int _bufPos;

        public HtmlEncodingTextWriter(TextWriter underlyingWriter)
        {
            _underlyingWriter = underlyingWriter;
            _buffer = ArrayPool<char>.Shared.Rent(2048);
            _bufPos = 0;
        }

        public bool IsDefault()
            => _buffer == null;

        public void FlushAndClear()
        {
            Flush();
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = null;
            _bufPos = 0;
        }

        public void Write(string s)
        {
            var position = 0;
            while (position < s.Length)
            {
                var safeChunkLength = HtmlEncodingHelpers.SafePrefixLength(s, position);

                WriteRawImpl(s, position, safeChunkLength);
                position += safeChunkLength;

                // we're now looking at an HTML-encoding character
                position = WriteEncodingChars(s, position);
            }
        }

        /// <summary>
        /// Consume a run of HTML-encoding characters from the string
        /// </summary>
        /// <returns>The new position</returns>
        private int WriteEncodingChars(string s, int position)
        {
            while (position < s.Length && HtmlEncodingHelpers.ShouldEncode(s[position]))
            {
                var c = s[position];
                position++;
                switch (c)
                {
                    case '<':
                        WriteRaw("&lt;");
                        continue;
                    case '>':
                        WriteRaw("&gt;");
                        continue;
                    case '&':
                        WriteRaw("&amp;");
                        continue;
                    case '"':
                        WriteRaw("&quot;");
                        continue;
                    case '\'':
                        WriteRaw("&#39;");
                        continue;
                }
                if (char.IsSurrogate(c))
                {
                    if (position >= s.Length)  // there's no low surrogate
                    {
                        WriteUnicodeReplacementChar();
                        continue;
                    }

                    var highSurrogate = c;
                    var lowSurrogate = s[position];
                    // don't increment position until we're sure we're going to consume lowSurrogate

                    if (!char.IsSurrogatePair(highSurrogate, lowSurrogate))
                    {
                        WriteUnicodeReplacementChar();
                        continue;
                    }

                    position++;

                    var codePoint = char.ConvertToUtf32(highSurrogate, lowSurrogate);
                    if (HtmlEncodingHelpers.IsBasicMultilingualPlane(codePoint))
                    {
                        WriteRaw(highSurrogate);
                        WriteRaw(lowSurrogate);
                        continue;
                    }
                    WriteNumericEntity(codePoint);
                    continue;
                }

                // shouldn't be reachable, because we checked ShouldEncode at the start of the while loop
                WriteRaw(c);
            }
            return position;
        }

        public void WriteRaw(char c)
        {
            FlushIfNecessary();
            _buffer[_bufPos] = c;
            _bufPos++;
        }

        public void WriteRaw(string s)
        {
            WriteRawImpl(s, 0, s.Length);
        }

        private void WriteRawImpl(string s, int start, int count)
        {
            while (count > 0)
            {
                var chunkSize = Math.Min(count, _buffer.Length - _bufPos);

                s.CopyTo(start, _buffer, _bufPos, chunkSize);

                count -= chunkSize;
                start += chunkSize;
                _bufPos += chunkSize;

                FlushIfNecessary();
            }
        }

        private void WriteUnicodeReplacementChar()
        {
            WriteRaw(HtmlEncodingHelpers.UNICODE_REPLACEMENT_CHAR);
        }

        private void WriteNumericEntity(int number)
        {
            WriteRawImpl("&#", 0, 2);
            // TODO: this will typically allocate a (small) string.
            // can do better by just appending the characters one by one
            WriteRaw(number.ToString(CultureInfo.InvariantCulture));
            WriteRaw(';');
        }

        private void FlushIfNecessary()
        {
            if (_bufPos == _buffer.Length)
            {
                Flush();
            }
        }

        private void Flush()
        {
            var len = _bufPos;
            _bufPos = 0;
            _underlyingWriter.Write(_buffer, 0, len);
        }
    }
}
