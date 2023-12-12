using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ControlSync
{
    /// <summary>
    /// Manages the controller mapping profiles.
    /// </summary>
    public static class ProfileManager
    {
        static readonly string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static List<string> Profiles
        {
            get
            {
                string dir = Path.Combine(docs, "ControlSync");
                DirectoryInfo d = new DirectoryInfo(dir);
                FileInfo[] files = d.GetFiles("*.json");
                return files.Select(x => x.Name.Remove(x.Name.LastIndexOf('.'))).ToList();
            }
        }

        public static void SaveProfile(List<ControllerMap> map, string name)
        {
            string dir = Path.Combine(docs, "ControlSync");
            string json = JsonConvert.SerializeObject(map);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string path = Path.Combine(docs, "ControlSync", name + ".json");
            File.WriteAllText(path, json);
        }
        public static List<ControllerMap> LoadProfile(string name)
        {
            string path = Path.Combine(docs, "ControlSync", name + ".json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<ControllerMap>>(json);
            }
            return null;
        }
    }
}
