//! \file       ArcAOS.cs
//! \date       Tue Aug 04 06:00:39 2015
//! \brief      LiLiM resource archive implementation.
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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using GameRes.Compression;

namespace GameRes.Formats.Lilim
{
    [Export(typeof(ArchiveFormat))]
    public class AosOpener : ArchiveFormat
    {
        public override string         Tag { get { return "AOS"; } }
        public override string Description { get { return "LiLiM/Le.Chocolat engine resource archive"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return false; } }

        public AosOpener ()
        {
            ContainedFormats = new[] { "BMP", "ABM", "IMG/BMP", "DAT/GENERIC", "OGG" };
        }

        static readonly byte[] IndexLink = Enumerable.Repeat<byte> (0xff, 0x10).ToArray();
        static readonly byte[] IndexEnd  = Enumerable.Repeat<byte> (0, 0x10).ToArray();

        public override ArcFile TryOpen (ArcView file)
        {
            if (0 == file.View.ReadByte (0))
                return null;
            uint first_offset = file.View.ReadUInt32 (0x10);
            if (first_offset >= file.MaxOffset || 0 != (first_offset & 0x1F))
                return null;
            var name_buf = file.View.ReadBytes (first_offset, 0x10);
            if (!name_buf.SequenceEqual (IndexLink) && !name_buf.SequenceEqual (IndexEnd))
                return null;

            string last_name = null;
            long current_offset = 0;
            var dir = new List<Entry> (0x3E);
            while (current_offset < file.MaxOffset)
            {
                if (0x10 != file.View.Read (current_offset, name_buf, 0, 0x10))
                    break;
                if (name_buf.SequenceEqual (IndexLink))
                {
                    uint next_offset = file.View.ReadUInt32 (current_offset+0x10);
                    current_offset += 0x20 + next_offset;
                }
                else
                {
                    int name_length = Array.IndexOf<byte> (name_buf, 0);
                    if (0 == name_length)
                        break;
                    if (-1 == name_length)
                        name_length = name_buf.Length;
                    var name = Encodings.cp932.GetString (name_buf, 0, name_length);
                    if (last_name == name || string.IsNullOrWhiteSpace (name))
                        return null;
                    last_name = name;
                    var entry = FormatCatalog.Instance.Create<PackedEntry> (name);
                    entry.Offset = file.View.ReadUInt32 (current_offset+0x10);
                    entry.Size   = file.View.ReadUInt32 (current_offset+0x14);
                    current_offset += 0x20;
                    entry.Offset += current_offset;
                    if (!entry.CheckPlacement (file.MaxOffset))
                        return null;
                    entry.IsPacked = name.HasExtension (".scr");
                    dir.Add (entry);
                }
            }
            return new ArcFile (file, this, dir);
        }

        public override Stream OpenEntry (ArcFile arc, Entry entry)
        {
            var aent = entry as PackedEntry;
            if (null == aent || !aent.IsPacked)
                return base.OpenEntry (arc, entry);

            aent.UnpackedSize = arc.File.View.ReadUInt32 (entry.Offset);
            var packed = arc.File.CreateStream (entry.Offset+4, entry.Size-4);
            var unpacked = new HuffmanStream (packed);
            return new LimitStream (unpacked, aent.UnpackedSize);
        }

        public override void Create(Stream output, IEnumerable<Entry> list, ResourceOptions options,
                                     EntryCallback callback)
        {
            int file_count = list.Count();
            if (null != callback)
                callback(file_count + 2, null, null);
            //int callback_count = 0;
            using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
            {
                var encoding = Encodings.cp932;
                byte[] name_buf = new byte[0x100];
                byte[] padding_buf = new byte[0x10];

                uint count = 0x20; // first chunck 0x20, other 0x1F
                uint index_offset = 0;
                uint index_size = count * 0x20;
                uint base_offset = index_offset + index_size;
                int entry_index = 0; 

                foreach (var entry in list)
                {
                    writer.BaseStream.Position = base_offset;
                    entry.Offset = base_offset;
                    using (var input = File.OpenRead(entry.Name))
                    {
                        // write data
                        var name = Path.GetFileName(entry.Name);
                        uint size = (uint)input.Length;
                        if (size > uint.MaxValue || base_offset + size > uint.MaxValue)
                        {
                            throw new FileSizeException();
                        }
                        bool tryPack = false;
                        if (name.HasExtension(".scr"))
                        {
                            tryPack = true;
                        }
                        else if (name.HasExtension(".abm"))
                        {
                            entry.Name = Path.ChangeExtension(entry.Name, ".cmp");
                        }
                        if (tryPack)
                        {
                            byte[] unpacked = new byte[input.Length];
                            input.Read(unpacked, 0, (int)input.Length);
                            byte[] packed = HuffmanEncoder.HuffmanEncoding(unpacked);
                            writer.Write(unpacked.Length);
                            writer.Write(packed, 0, packed.Length);
                            size = 4 + (uint)packed.Length;
                        }
                        else
                        {
                            input.CopyTo(output);
                        }
                        base_offset += size;
                        entry.Size = size;
                        // write padding
                        uint p = 0x10 - entry.Size % 0x10;
                        writer.Write(padding_buf, 0, (int)p);
                        base_offset += p;

                        // write index
                        writer.BaseStream.Position = index_offset;
                        name = Path.GetFileName(entry.Name);
                        size = (uint)encoding.GetBytes(name, 0, name.Length, name_buf, 0);
                        for (uint i = size; i < 0x10; ++i)
                            name_buf[i] = 0;
                        writer.Write(name_buf, 0, 0x10);
                        index_offset += 0x20;
                        writer.Write((uint)(entry.Offset - index_offset));
                        writer.Write(entry.Size);
                        writer.Write(padding_buf, 0, 8);

                        entry_index++;
                    }

                    // chunck end
                    if (entry_index >= count-1)
                    {
                        index_offset += 0x20;
                        for (int i = 0; i < 4; i++)
                        {
                            writer.Write(0xFFFFFFFF);
                        }
                        writer.Write(base_offset - index_offset); // next chunck offset
                        writer.Write(0);
                        writer.Write(padding_buf, 0, 8);
                        // reset
                        count = 0x1F;
                        index_offset = base_offset;
                        index_size = count * 0x20;
                        base_offset = index_offset + index_size;
                        entry_index = 0;
                    }
                }
                // pad chunck
                while (entry_index < count)
                {
                    writer.Write(padding_buf);
                    writer.Write(padding_buf);
                    entry_index++;
                }
            }
        }

        public override object GetCreationWidget()
        {
            return new GUI.CreateAOSWidget();
        }

        public override ResourceOptions GetDefaultOptions()
        {
            return new ArcOptions { 
                Description = Properties.Settings.Default.AOSDescription,
                Version = Properties.Settings.Default.AOSVersion
            };
        }
    }

    [Export(typeof(ArchiveFormat))]
    public class Aos2Opener : AosOpener
    {
        public override string         Tag { get { return "AOS/LiLiM"; } }
        public override string Description { get { return "LiLiM resource archive version 2"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return true; } }

        public Aos2Opener ()
        {
            Extensions = new string[] { "aos" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            if (0 != file.View.ReadInt32 (0))
                return null;

            long base_offset = file.View.ReadUInt32 (4);
            uint index_offset = 0x111;
            int index_size = file.View.ReadInt32 (8);
            if (base_offset >= file.MaxOffset || index_offset+index_size >= file.MaxOffset
                || base_offset < index_offset+index_size)
                return null;
            int count = index_size / 0x28;
            if (!IsSaneCount (count))
                return null;

            var dir = new List<Entry> (count);
            for (int i = 0; i < count; ++i)
            {
                var name = file.View.ReadString (index_offset, 0x20);
                if (0 == name.Length)
                    return null;
                var entry = FormatCatalog.Instance.Create<PackedEntry> (name);
                entry.Offset = base_offset + file.View.ReadUInt32 (index_offset+0x20);
                entry.Size   = file.View.ReadUInt32 (index_offset+0x24);
                if (!entry.CheckPlacement (file.MaxOffset))
                    return null;
                if (name.HasExtension (".scr"))
                    entry.IsPacked = true;
                else if (name.HasExtension (".cmp"))
                {
                    entry.IsPacked = true;
                    entry.Name = Path.ChangeExtension (entry.Name, ".abm");
                    entry.Type = "image";
                }
                dir.Add (entry);
                index_offset += 0x28;
            }
            return new ArcFile (file, this, dir);
        }

        public override void Create(Stream output, IEnumerable<Entry> list, ResourceOptions options,
                                     EntryCallback callback)
        {
            var arc_options = GetOptions<ArcOptions>(options);
            if (arc_options.Version == 1)
            {
                base.Create(output, list, options, callback);
                return;
            }

            int file_count = list.Count();
            if (null != callback)
                callback(file_count + 2, null, null);
            //int callback_count = 0;
            using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
            {
                var encoding = Encodings.cp932;
                byte[] name_buf = new byte[0x100];
                Int32 i32 = 0;
                writer.Write(i32);

                uint count = (uint)list.Count();
                uint index_offset = 0x111;
                uint index_size = count * 0x28;
                uint base_offset = index_offset + index_size;
                writer.Write(base_offset);
                writer.Write(index_size);
                // description
                encoding.GetBytes(arc_options.Description, 0, arc_options.Description.Length, name_buf, 0);
                writer.Write(name_buf, 0, name_buf.Length);

                // data section
                writer.BaseStream.Position = base_offset;
                foreach (var entry in list)
                {
                    entry.Offset = base_offset;
                    using (var input = File.OpenRead(entry.Name))
                    {
                        var name = Path.GetFileName(entry.Name);
                        uint size = (uint)input.Length;
                        if (size > uint.MaxValue || base_offset + size > uint.MaxValue)
                        {
                            throw new FileSizeException();
                        }
                        bool tryPack = false;
                        if (name.HasExtension(".scr"))
                        {
                            tryPack = true;
                        }
                        else if (name.HasExtension(".abm"))
                        {
                            tryPack = true;
                            entry.Name = Path.ChangeExtension(entry.Name, ".cmp");
                        }
                        if (tryPack)
                        {
                            byte[] unpacked = new byte[input.Length];
                            input.Read(unpacked, 0, (int)input.Length);
                            byte[] packed = HuffmanEncoder.HuffmanEncoding(unpacked);
                            writer.Write(unpacked.Length);
                            writer.Write(packed, 0, packed.Length);
                            size = 4 + (uint)packed.Length;
                        }
                        else
                        {
                            input.CopyTo(output);
                        }
                        base_offset += size;
                        entry.Size = size;
                    }
                }

                // index section
                writer.BaseStream.Position = index_offset;
                base_offset = index_offset + index_size;
                foreach (var entry in list)
                {
                    writer.BaseStream.Position = index_offset;
                    var name = Path.GetFileName(entry.Name);
                    int size = encoding.GetBytes(name, 0, name.Length, name_buf, 0);
                    for (int i = size; i < 0x20; ++i)
                        name_buf[i] = 0;
                    writer.Write(name_buf, 0, 0x20);
                    writer.Write((uint)(entry.Offset - base_offset));
                    writer.Write(entry.Size);
                    index_offset += 0x28;
                }
            }
        }
    }

    public class ArcOptions : ResourceOptions
    {
        public string Description { get; set; }
        public int Version { get; set; }
    }
}
