using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SchemeTool
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load database
            using (Stream stream = File.OpenRead(".\\GameData\\Formats.dat"))
            {
                GameRes.FormatCatalog.Instance.DeserializeScheme(stream);
            }

            GameRes.Formats.Malie.DatOpener format = GameRes.FormatCatalog.Instance.ArcFormats
                .FirstOrDefault(a => a is GameRes.Formats.Malie.DatOpener) as GameRes.Formats.Malie.DatOpener;

            if (format != null)
            {
                GameRes.Formats.Malie.MalieScheme scheme = format.Scheme as GameRes.Formats.Malie.MalieScheme;
                // Add scheme information here
                byte[] key = {0xa4, 0xa7, 0xa6, 0xa1, 0xa0, 0xa3, 0xa2, 0xac, 0xaf, 0xae, 0xa9, 0xa8, 0xab, 0xaa, 0xb4, 0xb7, 0xb6, 0xb1, 0xb0, 0xb3, 0xb2, 0xbc, 0xbf, 0xbe, 0xb9, 0xb8, 0xbb, 0xba, 0xa1, 0xa9, 0xb1, 0xb9};
                {
                    uint[] rot_key = { 0x70752D37, 0x4A526B58, 0x7841457A, 0x67416155 };
                    var crypt = new GameRes.Formats.Malie.LibCfiScheme(0x400, key, rot_key);
                    string name = "Silverio Ragnarok";
                    if (!scheme.KnownSchemes.ContainsKey(name)) scheme.KnownSchemes.Add(name, crypt);
                }
                {
                    uint[] rot_key = { 0x62466D43, 0x2B347A65, 0x74456279, 0x6D467A6F };
                    var crypt = new GameRes.Formats.Malie.LibCfiScheme(0x400, key, rot_key);
                    string name = "Silverio Vendetta -Verse of Orpheus-";
                    if (!scheme.KnownSchemes.ContainsKey(name)) scheme.KnownSchemes.Add(name, crypt);
                }
                {
                    uint[] rot_key = { 0x372D3668, 0x336B6234, 0x6635662B, 0x78723869 };
                    var crypt = new GameRes.Formats.Malie.LibCfiScheme(0x400, key, rot_key);
                    string name = "Silverio Trinity -Beyond the Horizon-";
                    if (!scheme.KnownSchemes.ContainsKey(name)) scheme.KnownSchemes.Add(name, crypt);
                }
                {
                    uint[] rot_key = { 0x3C787768, 0x466E2D69, 0x35726440, 0x612B6743 };
                    var crypt = new GameRes.Formats.Malie.LibCfiScheme(0x400, key, rot_key);
                    string name = "Dies irae Interview with Kaziklu Bey [ENG]";
                    if (!scheme.KnownSchemes.ContainsKey(name)) scheme.KnownSchemes.Add(name, crypt);
                }

            }

            var gameMap = typeof(GameRes.FormatCatalog).GetField("m_game_map", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(GameRes.FormatCatalog.Instance) as Dictionary<string, string>;

            if (gameMap != null)
            {
                // Add file name here
                //gameMap.Add("game.exe", "game title");
            }

            // Save database
            using (Stream stream = File.Create(".\\GameData\\Formats.dat"))
            {
                GameRes.FormatCatalog.Instance.SerializeScheme(stream);
            }
        }
    }
}
