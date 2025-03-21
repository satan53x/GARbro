﻿//! \file       Program.cs
//! \date       Mon Jun 30 20:12:13 2014
//! \brief      game resources browser.
//

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using GameRes;
using Newtonsoft.Json;

namespace GARbro
{
    class ConsoleBrowser
    {
        private string      m_arc_name;
        private ImageFormat m_image_format;
        private bool        m_extract_all;
        private bool        m_output_data = false;

        void ListFormats ()
        {
            Console.WriteLine ("Recognized resource formats:");
            foreach (var impl in FormatCatalog.Instance.ArcFormats)
            {
                Console.WriteLine ("{0,-4} {1}", impl.Tag, impl.Description);
            }
        }

        void OutputData()
        {
            string dir = "GameData";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Console.WriteLine("Output GameData to json:");
            // FormatsDataBase 
            string content = JsonConvert.SerializeObject(FormatCatalog.Instance.FormatsDataBase, Formatting.Indented);
            string filePath = Path.Combine(dir, "FormatsDataBase.json");
            File.WriteAllText(filePath, content);
            Console.WriteLine(filePath);
            // Formats
            List<IResource> lst = new List<IResource>();
            int i = 0;
            foreach (var format in FormatCatalog.Instance.Formats)
            {
                lst.Add(format);
                i++;
            }
            content = JsonConvert.SerializeObject(lst, Formatting.Indented);
            filePath = Path.Combine(dir, "Formats.json");
            File.WriteAllText(filePath, content);
            Console.WriteLine(filePath);
            //
            Console.WriteLine("Done.");
        }

        void ExtractAll (ArcFile arc)
        {
            arc.ExtractFiles ((i, entry, msg) => {
                if (null != entry)
                {
                    Console.WriteLine ("Extracting {0} ...", entry.Name);
                }
                else if (null != msg)
                {
                    Console.WriteLine (msg);
                }
                return ArchiveOperation.Continue;
            });
        }

        void ExtractFile (ArcFile arc, string name)
        {
            Entry entry = arc.Dir.FirstOrDefault (e => e.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
            if (null == entry)
            {
                Console.Error.WriteLine ("'{0}' not found within {1}", name, m_arc_name);
                return;
            }
            Console.WriteLine ("Extracting {0} ...", entry.Name);
            arc.Extract (entry);
        }

        void TestArc (string[] args)
        {
/*
            if (args.Length > 1)
            {
                uint pass = GameRes.Formats.IntOpener.EncodePassPhrase (args[1]);
                Console.WriteLine ("{0:X8}", pass);
            }
*/
        }

        ImageFormat FindFormat (string format)
        {
            var range = FormatCatalog.Instance.LookupExtension<ImageFormat> (format);
            return range.FirstOrDefault();
        }

        void Run (string[] args)
        {
            int argn = 0;
            while (argn < args.Length)
            {
                if (args[argn].Equals ("-l"))
                {
                    ListFormats();
                    return;
                }
                else if (args[argn].Equals ("-t"))
                {
                    TestArc (args);
                    return;
                }
                else if (args[argn].Equals ("-c"))
                {
                    if (argn+1 >= args.Length)
                    {
                        Usage();
                        return;
                    }
                    var tag = args[argn+1];
                    m_image_format = ImageFormat.FindByTag (tag);
                    if (null == m_image_format)
                    {
                        Console.Error.WriteLine ("{0}: unknown format specified", tag);
                        return;
                    }
                    argn += 2;
                }
                else if (args[argn].Equals ("-x"))
                {
                    m_extract_all = true;
                    ++argn;
                    if (args.Length <= argn)
                    {
                        Usage();
                        return;
                    }
                }
                else if (args[argn].Equals("-o"))
                {
                    m_output_data = true;
                    break;
                }
                else
                {
                    break;
                }
            }
            if (argn >= args.Length)
            {
                Usage();
                return;
            }
            DeserializeGameData();
            if (m_output_data)
            {
                OutputData();
                return;
            }
            foreach (var file in VFS.GetFiles (args[argn]))
            {
                m_arc_name = file.Name;
                try
                {
                    VFS.ChDir (m_arc_name);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine ("{0}: unknown format", m_arc_name);
                    continue;
                }
                var arc = ((ArchiveFileSystem)VFS.Top).Source;
                if (args.Length > argn+1)
                {
                    for (int i = argn+1; i < args.Length; ++i)
                        ExtractFile (arc, args[i]);
                }
                else if (m_extract_all)
                {
                    ExtractAll (arc);
                }
                else
                {
                    foreach (var entry in arc.Dir.OrderBy (e => e.Offset))
                    {
                        Console.WriteLine ("{0,9} [{2:X8}] {1}", entry.Size, entry.Name, entry.Offset);
                    }
                }
            }
        }

        void DeserializeGameData ()
        {
            string scheme_file = Path.Combine (FormatCatalog.Instance.DataDirectory, "Formats.dat");
            try
            {
                using (var file = File.OpenRead (scheme_file))
                    FormatCatalog.Instance.DeserializeScheme (file);
            }
            catch (Exception X)
            {
                Console.Error.WriteLine ("Scheme deserialization failed: {0}", X.Message);
            }
        }

        static void Usage ()
        {
            Console.WriteLine ("Usage: gameres [OPTIONS] ARC [ENTRIES]");
            Console.WriteLine ("    -l          list recognized archive formats");
            Console.WriteLine ("    -x          extract all files");
            Console.WriteLine ("    -c FORMAT   convert images to specified format");
            Console.WriteLine ("    -o          output GameData to json");
            Console.WriteLine ("Without options displays contents of specified archive.");
        }

        static void Main (string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (0 == args.Length)
            {
                Usage();
                return;
            }
            var listener = new TextWriterTraceListener (Console.Error);
            Trace.Listeners.Add(listener);
            try
            {
                var browser = new ConsoleBrowser();
                browser.Run (args);
            }
            catch (Exception X)
            {
                Console.Error.WriteLine (X.Message);
            }
        }
    }
}
