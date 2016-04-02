using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public class UserData
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		private Dictionary<string, Dictionary<string, object>> m_userData = new Dictionary<string, Dictionary<string, object>>();

		public bool HasCategory(string category)
		{
			return m_userData.ContainsKey(category);
		}

		public void AddCategory(string category)
		{
			if (HasCategory(category)) {
				return;
			}
			m_userData.Add(category, new Dictionary<string, object>());
		}

		public T Get<T>(string key, T def = default(T))
		{
			var parse = key.Split(new[] { '.' }, 2);
			if (parse.Length != 2) {
				return def;
			}
			Dictionary<string, object> dic;
			if (!m_userData.TryGetValue(parse[0], out dic)) {
				return def;
			}
			object obj;
			if (!dic.TryGetValue(parse[1], out obj)) {
				return def;
			}
			if (obj.GetType() != typeof(T)) {
				m_logger.Warn("Incompatible UserData type! Requested {0} but value is {1}", typeof(T).FullName, obj.GetType().FullName);
				return def;
			}
			return (T)obj;
		}

		public void Set<T>(string key, T value)
		{
			var parse = key.Split(new[] { '.' }, 2);
			if (parse.Length != 2) {
				m_logger.Error("No UserData category defined for Set()");
				return;
			}
			Dictionary<string, object> dic;
			if (!m_userData.TryGetValue(parse[0], out dic)) {
				dic = new Dictionary<string, object>();
				m_userData.Add(parse[0], dic);
			}
			dic[parse[1]] = value;
		}

		public bool Has(string key)
		{
			var parse = key.Split(new[] { '.' }, 2);
			if (parse.Length != 2) {
				return false;
			}
			Dictionary<string, object> dic;
			if (!m_userData.TryGetValue(parse[0], out dic)) {
				return false;
			}
			return dic.ContainsKey(parse[1]);
		}

		public void Dump()
		{
			m_logger.Debug("{0} categories:", m_userData.Count);
			foreach (var dic in m_userData) {
				m_logger.Debug("  '{0}':", dic.Key);
				foreach (var kv in dic.Value) {
					m_logger.Debug("    '{0}': '{1}' ({2})", kv.Key, kv.Value, kv.Value.GetType().FullName);
				}
			}
		}
	}
}
