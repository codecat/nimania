using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Plugins;

namespace Nimania.Runtime
{
	public class PluginManager
	{
		private List<Plugin> m_plugins = new List<Plugin>();
		private bool m_initialized = false;

		private GbxRemote m_remote;

		public PluginManager(GbxRemote remote)
		{
			m_remote = remote;
		}

		public void Load(string name)
		{
			switch (name) {
				case "Developer": m_plugins.Add(new Developer() { m_remote = m_remote }); break;
				case "Admin": m_plugins.Add(new Admin() { m_remote = m_remote }); break;
				default: Console.WriteLine("Unknown plugin: '" + name + "'"); break;
			}
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
	}
}
