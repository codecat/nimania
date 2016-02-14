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

		private ConfigFile m_config;
		private GbxRemote m_remote;
		private DbDriver m_database;

		public PluginManager(ConfigFile config, GbxRemote remote, DbDriver dbDriver)
		{
			m_config = config;
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
				case "Dedimania": newPlugin = new Dedimania(); break;
				case "Checkpoints": newPlugin = new Checkpoints(); break;
			}

			if (newPlugin == null) {
				Console.WriteLine("Unknown plugin: '" + name + "'");
				return null;
			}

			newPlugin.m_config = m_config;
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
			//TODO: Initialize plugins simultaneously and use tasks to wait for them to complete
			//TODO: Before you do that, make sure that GbxRemoteNet is thread-safe.. it's probably not.
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
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnBeginChallenge();
					});
				}
			}
		}

		public void OnPlayerConnect(string login)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnPlayerConnect(login);
					});
				}
			}
		}
	}
}
