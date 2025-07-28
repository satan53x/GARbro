//! \file       ImageBSG.cs
//! \date       Sat Oct 24 17:07:43 2015
//! \brief      Bishop graphics image.
//
// Copyright (C) 2015 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using GameRes.Utility;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameRes.Formats.Bishop
{
    internal class BsgMetaData : ImageMetaData
    {
        public int  UnpackedSize;
        public int  ColorMode;
        public int  CompressionMode;
        public int  DataOffset;
        public int  DataSize;
        public int  PaletteOffset;
        public bool HasPalette;
    }

    [Export(typeof(ImageFormat))]
    public class BsgFormat : ImageFormat
    {
        public override string         Tag { get { return "BSG"; } }
        public override string Description { get { return "Bishop image format"; } }
        public override uint     Signature { get { return 0x2D535342; } } // 'BSS-'

        public override ImageMetaData ReadMetaData (IBinaryStream stream)
        {
            var header = stream.ReadHeader (0x60);
            int base_offset = 0;
            if (header.AsciiEqual ("BSS-Composition\0"))
                base_offset = 0x20;
            if (!header.AsciiEqual (base_offset, "BSS-Graphics\0"))
                return null;
            int type = header[base_offset+0x30];
            if (type > 2)
                return null;
            return new BsgMetaData
            {
                Width       = header.ToUInt16 (base_offset+0x16),
                Height      = header.ToUInt16 (base_offset+0x18),
                OffsetX     = header.ToInt16 (base_offset+0x20),
                OffsetY     = header.ToInt16 (base_offset+0x22),
                UnpackedSize = header.ToInt32 (base_offset+0x12),
                BPP = 2 == type ? 8 : 32,
                ColorMode   = type,
                CompressionMode = header[base_offset+0x31],
                DataOffset  = header.ToInt32 (base_offset+0x32)+base_offset,
                DataSize    = header.ToInt32 (base_offset+0x36),
                PaletteOffset = header.ToInt32 (base_offset+0x3A)+base_offset,
                HasPalette  = header.ToInt32 (base_offset+0x3A) != 0
            };
        }

        public override ImageData Read (IBinaryStream stream, ImageMetaData info)
        {
            var meta = (BsgMetaData)info;
            using (var reader = new BsgReader (stream, meta))
            {
                reader.Unpack();
                return ImageData.CreateFlipped (info, reader.Format, reader.Palette, reader.Data, reader.Stride);
            }
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException ("BsgFormat.Write not implemented");
        }
    }

    internal sealed class BsgReader : IDisposable
    {
        IBinaryStream       m_input;
        BsgMetaData         m_info;
        byte[]              m_output;

        public byte[]           Data { get { return m_output; } }
        public PixelFormat    Format { get; private set; }
        public BitmapPalette Palette { get; private set; }
        public int            Stride { get; private set; }

        public BsgReader (IBinaryStream input, BsgMetaData info)
        {
            m_info = info;
            if (m_info.CompressionMode > 2)
                throw new NotSupportedException ("Not supported BSS Graphics compression");

            m_input = input;
            m_output = new byte[m_info.UnpackedSize];
            switch (m_info.ColorMode)
            {
            case 0:
                Format = PixelFormats.Bgra32;
                Stride = (int)m_info.Width * 4;
                break;
            case 1:
                Format = PixelFormats.Bgr32;
                Stride = (int)m_info.Width * 4;
                break;
            case 2:
                Format = m_info.HasPalette ? PixelFormats.Indexed8 : PixelFormats.Gray8;
                Stride = (int)m_info.Width;
                Palette = m_info.HasPalette ? ReadPalette() : null;
                break;
            }
        }

        public void Unpack ()
        {
            m_input.Position = m_info.DataOffset;
            if (0 == m_info.CompressionMode)
            {
                if (1 == m_info.ColorMode)
                {
                    int dst = 0;
                    for (int count = m_info.DataSize / 3; count > 0; --count)
                    {
                        m_input.Read (m_output, dst, 3);
                        dst += 4;
                    }
                }
                else
                {
                    m_input.Read (m_output, 0, m_info.DataSize);
                }
            }
            else
            {
                Action<int, int> unpacker;
                if (1 == m_info.CompressionMode)
                    unpacker = UnpackRle;
                else
                    unpacker = UnpackLz;
                if (0 == m_info.ColorMode)
                {
                    for (int channel = 0; channel < 4; ++channel)
                        unpacker (channel, 4);
                }
                else if (1 == m_info.ColorMode)
                {
                    for (int channel = 0; channel < 3; ++channel)
                        unpacker (channel, 4);
                }
                else
                {
                    unpacker (0, 1);
                }
            }
        }

        void UnpackRle (int dst, int pixel_size)
        {
            int remaining = m_input.ReadInt32();
            while (remaining > 0)
            {
                int count = m_input.ReadInt8();
                --remaining;
                if (count >= 0)
                {
                    for (int i = 0; i <= count; ++i)
                    {
                        m_output[dst] = m_input.ReadUInt8();
                        --remaining;
                        dst += pixel_size;
                    }
                }
                else
                {
                    count = 1 - count;
                    byte repeat = m_input.ReadUInt8();
                    --remaining;
                    for (int i = 0; i < count; ++i)
                    {
                        m_output[dst] = repeat;
                        dst += pixel_size;
                    }
                }
            }
        }

        void UnpackLz (int plane, int pixel_size)
        {
            int dst = plane;
            byte control = m_input.ReadUInt8();
            int remaining = m_input.ReadInt32() - 5;
            while (remaining > 0)
            {
                byte c = m_input.ReadUInt8();
                --remaining;

                if (c == control)
                {
                    int offset = m_input.ReadUInt8();
                    --remaining;
                    if (offset != control)
                    {
                        int count = m_input.ReadUInt8();
                        --remaining;

                        if (offset > control)
                            --offset;

                        offset *= pixel_size;

                        while (count --> 0)
                        {
                            m_output[dst] = m_output[dst-offset];
                            dst += pixel_size;
                        }
                        continue;
                    }
                }
                m_output[dst] = c;
                dst += pixel_size;
            }
            for (int i = plane + pixel_size; i < m_output.Length; i += pixel_size)
                m_output[i] += m_output[i-pixel_size];
        }

        BitmapPalette ReadPalette ()
        {
            m_input.Position = m_info.PaletteOffset;
            return ImageFormat.ReadPalette (m_input.AsStream);
        }

        #region IDisposable Members
        public void Dispose ()
        {
        }
        #endregion
    }
}
