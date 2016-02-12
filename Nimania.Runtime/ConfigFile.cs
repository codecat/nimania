using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public class ConfigFileEntry
	{
		public string Value = "";
		public List<string> ArrayValue = new List<string>();
	}

	public class ConfigFile
	{
		private Dictionary<string, Dictionary<string, ConfigFileEntry>> Groups = new Dictionary<string, Dictionary<string, ConfigFileEntry>>();

		public string m_filename;

		public ConfigFile(string filename)
		{
			m_filename = filename;

			using (StreamReader reader = new StreamReader(File.OpenRead(filename))) {
				int currLine = 1;
				string inGroup = "";

				while (!reader.EndOfStream) {
					string line = reader.ReadLine().TrimStart();

					if (line.Length == 0 || line[0] == '#') {
						currLine++;
						continue;
					}

					if (line[0] == '[') {
						inGroup = line.Trim('[', ']').Trim();
						if (!Groups.ContainsKey(inGroup)) {
							Groups.Add(inGroup, new Dictionary<string, ConfigFileEntry>());
						}
						currLine++;
						continue;
					}

					if (inGroup == "" || !Groups.ContainsKey(inGroup)) {
						throw new Exception("Unexpected key/value pair on line " + currLine + " while reading " + filename);
					}

					string[] parse = line.Split(new char[] { '=' }, 2);
					if (parse.Length != 2) {
						throw new Exception("Unexpected key on line " + currLine + " while reading " + filename);
					}

					string key = parse[0];
					string value = parse[1];
					bool isArray = key.EndsWith("[]");

					if (isArray) {
						key = key.Substring(0, key.Length - 2);
					}

					if (!Groups[inGroup].ContainsKey(key)) {
						Groups[inGroup][key] = new ConfigFileEntry();
					}

					if (isArray) {
						Groups[inGroup][key].ArrayValue.Add(value);
					} else {
						Groups[inGroup][key].Value = value;
					}
				}
			}
		}

		public string this[string index] { get { return GetString(index); } }

		public string GetString(string query)
		{
			string[] parse = query.Split(new char[] { '.' }, 2);
			if (parse.Length != 2) {
				throw new Exception("Invalid indexer format '" + query + "'");
			}

			string group = parse[0];
			string key = parse[1];

			if (!Groups.ContainsKey(group)) {
				throw new Exception("Unknown group '" + group + "'");
			}

			if (!Groups[group].ContainsKey(key)) {
				throw new Exception("Unknown key '" + key + "' in group '" + group + "'");
			}

			return Groups[group][key].Value;
		}

		public int GetInt(string query)
		{
			string s = GetString(query);
			return int.Parse(s);
		}

		public bool GetBool(string query)
		{
			string s = GetString(query);
			return s == "1" || s.ToLower() == "true";
		}

		public string[] GetArray(string group, string key)
		{
			if (!Groups.ContainsKey(group)) {
				return new string[] { };
			}
			if (!Groups[group].ContainsKey(key)) {
				return new string[] { };
			}
			return Groups[group][key].ArrayValue.ToArray();
		}
	}
}
