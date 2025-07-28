//! \file       ArcDAT.cs
//! \date       Sat Feb 25 02:30:54 2017
//! \brief      Valkyria resource archive.
//
// Copyright (C) 2017 by morkt
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

namespace GameRes.Formats.Valkyria
{
    [Export(typeof(ArchiveFormat))]
    public class DatOpener : ArchiveFormat
    {
        public override string         Tag { get { return "DAT/VALKYRIA"; } }
        public override string Description { get { return "Valkyria resource archive"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return false; } }

        public override ArcFile TryOpen (ArcView file)
        {
            uint index_size = file.View.ReadUInt32 (0);
            if (0 == index_size)
                return TryOpenV1 (file);
            if (0 == index_size || index_size >= file.MaxOffset)
                return null;
            int count = (int)index_size / 0x10C;
            if (index_size != (uint)count * 0x10Cu || !IsSaneCount (count))
                return null;
            uint index_offset = 4;
            long base_offset = index_offset + index_size;
            var dir = new List<Entry> (count);
            for (int i = 0; i < count; ++i)
            {
                var name = file.View.ReadString (index_offset, 0x104);
                index_offset += 0x104;
                var entry = FormatCatalog.Instance.Create<Entry> (name);
                entry.Offset = base_offset + file.View.ReadUInt32 (index_offset);
                entry.Size   = file.View.ReadUInt32 (index_offset+4);
                if (!entry.CheckPlacement (file.MaxOffset))
                    return null;
                index_offset += 8;
                dir.Add (entry);
            }
            return new ArcFile (file, this, dir);
        }

        private ArcFile TryOpenV1(ArcView file)
        {
            var index_size = file.View.ReadUInt32 (4);
            if (0 == index_size || index_size >= file.MaxOffset)
                return null;
            int count = (int)index_size / 0x10C;
            if (index_size != (uint)count * 0x10Cu || !IsSaneCount (count))
                return null;
            var dir_path = Path.GetDirectoryName (file.Name);
            if (null == dir_path)
                return null;
            var arc_key = ReadArcKey (dir_path);
            if (null == arc_key)
                return null;
            uint index_offset = 8;
            long base_offset = index_offset + index_size;
            var dir = new List<Entry> (count);
            for (int i = 0; i < count; ++i)
            {
                var name = file.View.ReadString (index_offset, 0x104);
                var key = GetEntryKey (arc_key, file.View.ReadBytes (index_offset, 0x104));
                index_offset += 0x104;
                var entry = FormatCatalog.Instance.Create<Entry> (name);
                entry.Offset = base_offset + (file.View.ReadUInt32 (index_offset) ^ key);
                entry.Size = file.View.ReadUInt32 (index_offset + 4) ^ key;
                if (!entry.CheckPlacement (file.MaxOffset))
                    return null;
                index_offset += 8;
                dir.Add (entry);
            }
            return new ArcFile (file, this, dir);
        }

        private static byte[] ReadArcKey (string dir_path)
        {
            var file_path = Path.Combine (dir_path, "system.dat");
            if (!File.Exists (file_path))
                return null;
            var info = new byte[260];
            using (var fs = File.OpenRead (file_path))
            {
                fs.Position = 0x10E;
                fs.Read (info, 0, 260);
                fs.Close ();
            }
            var key = new byte[4];
            var len = info.TakeWhile (x => x != 0).Count ();
            for (int i = len, j = 0; i != 0; i--)
            {
                key[j] += info[i];
                if (++j == 4)
                    j = 0;
            }
            return key;
        }

        private static uint GetEntryKey (byte[] arc_key, byte[] name)
        {
            var key = new byte[4];
            var len = name.TakeWhile (x => x != 0).Count ();
            for (int i = len, j = 0; i != 0; i--)
            {
                key[j] += name[i];
                if (++j == 4)
                    j = 0;
            }
            key[0] += arc_key[3];
            key[1] += arc_key[2];
            key[2] += arc_key[1];
            key[3] += arc_key[0];
            return BitConverter.ToUInt32 (key, 0);
        }
    }
}
