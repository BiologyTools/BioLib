using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BioGTK
{
    public static class Settings
    {
        /* Creating a new dictionary with a string as the key and a string as the value. */
        static Dictionary<string,string> Default = new Dictionary<string,string>();
        /// <summary>
        /// The function "GetSettings" returns the value associated with a given name from a dictionary,
        /// or an empty string if the name is not found.
        /// </summary>
        /// <param name="name">The name parameter is a string that represents the name of the setting
        /// that you want to retrieve.</param>
        /// <returns>
        /// The method is returning a string value. If the specified name exists in the Default
        /// dictionary, the corresponding value is returned. Otherwise, an empty string is returned.
        /// </returns>
        public static string GetSettings(string name)
        {
            if(Default.ContainsKey(name)) return Default[name];
            return "";
        }

        /// <summary>
        /// The function "AddSettings" adds a name-value pair to the "Default" dictionary.
        /// </summary>
        /// <param name="name">The name parameter is a string that represents the name of the
        /// setting.</param>
        /// <param name="val">The "val" parameter is a string value that represents the value to be
        /// added to the settings.</param>
        public static void AddSettings(string name,string val)
        {
            Default.Add(name, val);
        }
        static string path = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
        /// <summary>
        /// The Save function writes the key-value pairs from the Default dictionary to a text file.
        /// </summary>
        public static void Save()
        {
            string val = "";
            foreach (var item in Default)
            {
                val += item.Key + "=" + item.Value + Environment.NewLine;
            }
            File.WriteAllText(path + "/Settings.txt", val);
        }

        /// <summary>
        /// The Load function reads settings from a file and adds them to a dictionary.
        /// </summary>
        /// <returns>
        /// If the file "Settings.txt" does not exist, the method will return without doing anything.
        /// </returns>
        public static void Load()
        {
            if (!File.Exists(path + "/Settings.txt"))
                return;
            string[] sts = File.ReadAllLines(path + "/Settings.txt");
            foreach (string item in sts)
            {
                string[] st = item.Split('=');
                Default.Add(st[0], st[1]);
            }
        }
    }
}
