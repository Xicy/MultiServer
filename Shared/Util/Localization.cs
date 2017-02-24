using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Shared.Util
{
    public static class Localization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Storage = new Dictionary<string, Dictionary<string, string>>();

        private static bool _isLoadDefault;
        
        private static void LoadDefault()
        {
            if (_isLoadDefault) return;
            LoadByEmbededAssembly(Assembly.GetExecutingAssembly());
            LoadByEmbededAssembly(Assembly.GetEntryAssembly());
            LoadByDirectory(Environment.CurrentDirectory, "*.lang", SearchOption.AllDirectories);
            _isLoadDefault = true;
        }

        public static void LoadByDirectory(string path, string searchPattern, SearchOption searchOption)
        {
            foreach (var file in Directory.GetFiles(path, searchPattern, searchOption))
                using (var fileReader = new FileReader(file))
                    Load(fileReader, Path.GetFileName(file));
        }
        public static void LoadByEmbededAssembly(Assembly assembly)
        {
            foreach (var file in assembly.GetManifestResourceNames().Where(s => s.EndsWith("lang")))
            {
                using (var fileReader = new FileReader(assembly.GetManifestResourceStream(file)))
                {
                    var filename = file.Split('.');
                    Load(fileReader, filename[filename.Length - 2] + ".lang");
                }
            }
        }

        private static void Load(FileReader fileReader, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var lang = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            if (!Storage.ContainsKey(lang)) Storage[lang] = new Dictionary<string, string>();
            foreach (var eachLine in fileReader)
            {
                var pos = eachLine.Value.IndexOf('\t');
                if (pos < 0) continue;

                var key = eachLine.Value.Substring(0, pos).Trim().ToUpperInvariant();
                var val = eachLine.Value.Substring(pos + 1);

                if (!Storage[lang].ContainsKey(key)) Storage[lang][key] = val.Replace("\\t", "\t").Replace("\\r\\n", "\n").Replace("\\n", "\n");
            }
        }

        private const string DefaultLangName = "EN";
        public static string Get(string key)
        {
            return Get(key, CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        }
        public static string Get(string key, string lang)
        {
            Dictionary<string, string> dic;
            lang = lang.ToUpperInvariant();
            if (!_isLoadDefault) { LoadDefault(); }
            if (!Storage.TryGetValue(lang, out dic) && !Storage.TryGetValue(DefaultLangName, out dic)) return key;
            string val;
            if (dic.TryGetValue(key.ToUpperInvariant(), out val)) { return val; }
            else if (lang == DefaultLangName) { return key; }
            return Get(key, DefaultLangName);
        }
    }
}
