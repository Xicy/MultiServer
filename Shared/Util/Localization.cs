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
        private const string DefaultLangName = "EN";
        private const string DefaultFileExtention = "lang";

        private static readonly Dictionary<string, Dictionary<string, string>> Storage = new Dictionary<string, Dictionary<string, string>>();

        private static bool _isLoadDefault;

        private static void LoadDefault()
        {
            if (_isLoadDefault) return;
            LoadByEmbededAssembly(Assembly.GetExecutingAssembly());
            LoadByEmbededAssembly(Assembly.GetEntryAssembly());
            LoadByDirectory(Environment.CurrentDirectory, "*." + DefaultFileExtention);
            _isLoadDefault = true;
        }

        public static void LoadByDirectory(string path, string searchPattern, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrEmpty(searchPattern)) throw new ArgumentNullException(nameof(searchPattern));

            foreach (var file in Directory.GetFiles(path, searchPattern, searchOption))
                using (var fileReader = new FileReader(file))
                    Load(fileReader, Path.GetFileName(file));
        }
        public static void LoadByEmbededAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            foreach (var file in assembly.GetManifestResourceNames().Where(s => s.EndsWith(DefaultFileExtention)))
            {
                using (var fileReader = new FileReader(assembly.GetManifestResourceStream(file)))
                {
                    var filename = file.Split('.');
                    Load(fileReader, filename[filename.Length - 2] + "." + DefaultFileExtention);
                }
            }
        }

        private static void Load(FileReader fileReader, string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (fileReader == null) throw new ArgumentNullException(nameof(fileReader));

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

        public static string Get(string key)
        {
            return Get(key, CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        }
        public static string Get(string key, string lang)
        {
            if (!_isLoadDefault)
                LoadDefault();

            lang = lang.ToUpperInvariant();
            key = key.ToUpperInvariant();

            Dictionary<string, string> dic;
            string val;

            if (Storage.TryGetValue(lang, out dic) && dic.TryGetValue(key, out val)) return val;
            if (Storage.TryGetValue(DefaultLangName, out dic) && dic.TryGetValue(key, out val)) return val;
            return key;
        }
    }
}
