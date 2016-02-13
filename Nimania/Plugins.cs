using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Plugins;
using Nimania.Runtime;
using Nimania.Runtime.DbModels;

namespace Nimania.Runtime
{
	public class PluginManager
	{
		private List<Plugin> m_plugins = new List<Plugin>();
		private bool m_initialized = false;

		private GbxRemote m_remote;
		private DbDriver m_database;

		public PluginManager(GbxRemote remote, DbDriver dbDriver)
		{
			m_remote = remote;
			m_database = dbDriver;
		}

		public Plugin Load(string name)
		{
			Plugin newPlugin = null;
			switch (name) {
				case "Developer": newPlugin = new Developer(); break;
				case "Admin": newPlugin = new Admin(); break;
				case "Locals": newPlugin = new Locals(); break;
			}

			if (newPlugin == null) {
				Console.WriteLine("Unknown plugin: '" + name + "'");
				return null;
			}

			newPlugin.m_remote = m_remote;
			newPlugin.m_database = m_database;
			m_plugins.Add(newPlugin);
			return newPlugin;
		}

		public void Add(Plugin plugin)
		{
			if (m_initialized) {
				throw new Exception("Can't add new plugins when plugins are already initialized.");
			}
			m_plugins.Add(plugin);
		}

		public void Initialize()
		{
			foreach (var plugin in m_plugins) {
				plugin.Initialize();
			}
			m_initialized = true;
		}

		public void Uninitialize()
		{
			if (!m_initialized) {
				throw new Exception("Plugins aren't initialized in the first place.");
			}
			while (m_plugins.Count > 0) {
				m_plugins[0].Uninitialize();
				m_plugins.RemoveAt(0);
			}
			m_initialized = false;
		}

		public Plugin GetPlugin(string name)
		{
			foreach (var plugin in m_plugins) {
				if (plugin.GetType().Name == name) {
					return plugin;
				}
			}
			return null;
		}

		public void OnBeginChallenge()
		{
			foreach (var plugin in m_plugins) {
				Task.Factory.StartNew(() => {
					plugin.OnBeginChallenge();
				});
			}
		}
	}
}
