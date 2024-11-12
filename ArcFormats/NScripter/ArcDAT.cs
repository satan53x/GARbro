using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using GameRes.Formats.Strings;
using GameRes.Utility;
using GameRes.Compression;

namespace GameRes.Formats.NScripter
{
    public class DatEntry : NsaEntry
    {
        public uint Hash { get; set; }
    }

    [Export(typeof(ArchiveFormat))]
    public class DatOpener : NsaOpener
    {
        public override string Tag { get { return "NSA/DAT"; } }

        public DatOpener ()
        {
            Extensions = new string[] { "dat" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            List<Entry> dir = null;
            bool zero_signature = 0 == file.View.ReadInt16 (0);
            try
            {
                using (var input = file.CreateStream())
                {
                    if (zero_signature)
                        input.Seek (2, SeekOrigin.Begin);
                    dir = ReadIndex (input);
                    if (null != dir)
                        return new ArcFile (file, this, dir);
                }
            }
            catch { /* ignore parse errors */ }
            return null;
        }

        protected new List<Entry> ReadIndex (Stream file)
        {
            using (var input = new ArcView.Reader (file))
            {
                int count = Binary.BigEndian (input.ReadInt16());
                if (!IsSaneCount (count))
                    return null;

                var dir = new List<Entry>();
                for (int i = 0; i < count; ++i)
                {
                    var hash = Binary.BigEndian(input.ReadUInt32());
                    var name = string.Format("{0:D4}_{1:X8}.txt", i, hash);
                    var entry = FormatCatalog.Instance.Create<DatEntry> (name);
                    entry.Hash = hash;
                    byte compression_type = input.ReadByte();
                    entry.Offset = Binary.BigEndian (input.ReadUInt32());
                    entry.Size   = Binary.BigEndian (input.ReadUInt32());
                    if (!entry.CheckPlacement (file.Length))
                        return null;
                    entry.UnpackedSize = Binary.BigEndian (input.ReadUInt32());
                    entry.IsPacked = compression_type != 0;
                    switch (compression_type)
                    {
                    case 0:  entry.CompressionType = Compression.None; break;
                    case 1:  entry.CompressionType = Compression.SPB; break;
                    case 2:  entry.CompressionType = Compression.LZSS; break;
                    case 4:  entry.CompressionType = Compression.NBZ; break;
                    case 8:  entry.CompressionType = Compression.ZLIB; break;
                    default: entry.CompressionType = Compression.Unknown; break;
                    }
                    dir.Add (entry);
                }
                return dir;
            }
        }

        public override void Create(Stream output, IEnumerable<Entry> list, ResourceOptions options,
                                     EntryCallback callback)
        {
            var ons_options = GetOptions<NsaOptions>(options);
            var encoding = Encodings.cp932.WithFatalFallback();
            int callback_count = 0;

            var real_entry_list = new List<NsaEntry>();
            var used_names = new HashSet<string>();
            int index_size = 0;
            foreach (var entry in list)
            {
                if (!used_names.Add(entry.Name)) // duplicate name
                    continue;
                try
                {
                    index_size += 4;
                }
                catch (EncoderFallbackException X)
                {
                    throw new InvalidFileName(entry.Name, arcStrings.MsgIllegalCharacters, X);
                }
                var header_entry = new NsaEntry { Name = entry.Name };
                if (Compression.None != ons_options.CompressionType)
                {
                    if (!entry.Name.HasExtension(".bmp")) // ??? diff with NSA
                        header_entry.CompressionType = ons_options.CompressionType;
                }
                index_size += 13;
                real_entry_list.Add(header_entry);
            }

            long start_offset = output.Position;
            long base_offset = 4 + index_size;
            output.Seek(base_offset, SeekOrigin.Current);
            foreach (var entry in real_entry_list)
            {
                using (var input = File.OpenRead(entry.Name))
                {
                    var file_size = input.Length;
                    if (file_size > uint.MaxValue)
                        throw new FileSizeException();
                    long file_offset = output.Position;
                    if (file_offset + file_size > uint.MaxValue)
                        throw new FileSizeException();
                    if (null != callback)
                        callback(callback_count++, entry, arcStrings.MsgAddingFile);
                    entry.Offset = file_offset;
                    entry.UnpackedSize = (uint)file_size;
                    if (Compression.LZSS == entry.CompressionType)
                    {
                        var packer = new Packer(input, output);
                        entry.Size = packer.EncodeLZSS();
                    }
                    else if (Compression.ZLIB == entry.CompressionType)
                    {
                        var dest = new MemoryStream();
                        using (var zs = new ZLibStream(dest, CompressionMode.Compress, CompressionLevel.BestCompression, true))
                        {
                            input.CopyTo(zs);
                        }
                        dest.Position = 0;
                        entry.Size = (uint)dest.Length;
                        dest.CopyTo(output);
                    }
                    else
                    {
                        entry.Size = entry.UnpackedSize;
                        entry.CompressionType = Compression.None;
                        input.CopyTo(output);
                    }
                }
            }

            if (null != callback)
                callback(callback_count++, null, arcStrings.MsgWritingIndex);
            output.Position = start_offset;
            using (var writer = new BinaryWriter(output, encoding, true))
            {
                writer.Write(Binary.BigEndian((uint)real_entry_list.Count));
                foreach (var entry in real_entry_list)
                {
                    var sa = entry.Name.Split(new char[2] {'_', '.'});
                    uint hash = Convert.ToUInt32(sa[1], 16);
                    writer.Write(Binary.BigEndian(hash));
                    writer.Write((byte)entry.CompressionType);
                    writer.Write(Binary.BigEndian((uint)entry.Offset));
                    writer.Write(Binary.BigEndian((uint)entry.Size));
                    writer.Write(Binary.BigEndian((uint)entry.UnpackedSize));
                }
            }
        }
    }
}
