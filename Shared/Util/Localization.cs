using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Shared.Util
{
    public static class Localization
    {
        private static Dictionary<string, Dictionary<string, string>> _storage = new Dictionary<string, Dictionary<string, string>>();

        private static bool isLoadDefault = false;
        private static void LoadDefault()
        {
            Parse(Environment.CurrentDirectory, "*.lang", SearchOption.AllDirectories);
            using (var fileReader = new FileReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Shared.Embed.Default.lang")))
            {
                _storage["DEFAULT"] = new Dictionary<string, string>();
                foreach (var eachLine in fileReader)
                {
                    var pos = eachLine.Value.IndexOf('\t');
                    if (pos < 0) continue;

                    var key = eachLine.Value.Substring(0, pos).Trim().ToUpperInvariant();
                    var val = eachLine.Value.Substring(pos + 1);
                    _storage["DEFAULT"][key] = val.Replace("\\t", "\t").Replace("\\r\\n", "\n").Replace("\\n", "\n");
                }
            }
            isLoadDefault = true;
        }

        public static void Parse(string path, string searchPattern, System.IO.SearchOption searchOption)
        {
            foreach (var file in Directory.GetFiles(path, searchPattern, searchOption))
                LoadFile(file);
        }

        private static void LoadFile(string path)
        {
            using (var fileReader = new FileReader(path))
            {
                var lang = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                _storage[lang] = new Dictionary<string, string>();
                foreach (var eachLine in fileReader)
                {
                    var pos = eachLine.Value.IndexOf('\t');
                    if (pos < 0) continue;

                    var key = eachLine.Value.Substring(0, pos).Trim().ToUpperInvariant();
                    var val = eachLine.Value.Substring(pos + 1);

                    _storage[lang][key] = val.Replace("\\t", "\t").Replace("\\r\\n", "\n").Replace("\\n", "\n");
                }
            }
        }

        public static string Get(string key)
        {
            return Get(key, CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        }
        public static string Get(string key, string lang)
        {
            Dictionary<string, string> dic;
            lang = lang.ToUpperInvariant();
            if (!isLoadDefault) { LoadDefault(); }
            if (_storage.TryGetValue(lang, out dic) || _storage.TryGetValue("DEFAULT", out dic))
            {
                string val;
                if (dic.TryGetValue(key.ToUpperInvariant(), out val)) { return val; }
                else if (lang == "DEFAULT") { return key; }
                return Get(key, "DEFAULT");
            }
            return key;
        }
    }
}
