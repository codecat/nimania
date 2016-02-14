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
				case "Greeter": newPlugin = new Greeter(); break;
				case "Live": newPlugin = new Live(); break;
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
			var tasks = new List<Task>();
			foreach (var plugin in m_plugins) {
				tasks.Add(Task.Factory.StartNew(() => {
					plugin.Initialize();
				}));
			}
			Console.WriteLine("Waiting for Initializing tasks to complete");
			Task.WaitAll(tasks.ToArray());
			Console.WriteLine("Done!");
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

		public void OnPlayerConnect(PlayerInfo player)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnPlayerConnect(player);
					});
				}
			}
		}

		public void OnPlayerDisconect(PlayerInfo player)
		{
			var tasks = new List<Task>();
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					tasks.Add(Task.Factory.StartNew(() => {
						plugin.OnPlayerDisconnect(player);
					}));
				}
			}
			// Wait for all tasks to be complete, because after this the PlayerInfo will be out of the game!
			Task.WaitAll(tasks.ToArray());
		}

		public void OnPlayerBegin(PlayerInfo player)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnPlayerBegin(player);
					});
				}
			}
		}

		public void OnPlayerCheckpoint(PlayerInfo player, int n, int time)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnPlayerCheckpoint(player, n, time);
					});
				}
			}
		}

		public void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnPlayerFinish(player, time, checkpoints);
					});
				}
			}
		}
	}
}
