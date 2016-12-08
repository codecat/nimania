using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using Nimania.Runtime.DbModels;
using CSScriptLibrary;
using System.IO;
using NLog;

namespace Nimania
{
	public class PluginManager
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		//TODO: This should not be public, but Developer plugin uses this!
		public List<Plugin> m_plugins = new List<Plugin>();
		private bool m_initialized = false;

		public int m_errorCount = 0;

		private GameInfo m_game;
		private ConfigFile m_config;
		private GbxRemote m_remote;
		private DbDriver m_database;

		private List<AsmHelper> m_scripting = new List<AsmHelper>();

		public PluginManager(GameInfo game, ConfigFile config, GbxRemote remote, DbDriver dbDriver)
		{
			m_game = game;
			m_config = config;
			m_remote = remote;
			m_database = dbDriver;

#if DEBUG
			string[] scriptPaths = Directory.GetDirectories("../../Data/Plugins/");
#else
			string[] scriptPaths = Directory.GetDirectories("Data/Plugins/");
#endif

			string[] asmRefs = new string[] { "GbxRemoteNet", "Nimania", "Nimania.Runtime", "CookComputing.XmlRpcV2", "NLog" };

			var tasks = new List<Task>();
			foreach (var path in scriptPaths) {
				tasks.Add(Task.Factory.StartNew(() => {
					string[] scriptFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
					try {
						var asm = new AsmHelper(CSScript.LoadFiles(scriptFiles, asmRefs));
						m_scripting.Add(asm);
						m_logger.Info("Compiled module: {0}", path);
					} catch (Exception ex) {
						m_logger.Error("Couldn't compile module: {0} - {1}", path, ex.ToString());
						m_errorCount++;
					}
				}));
			}
			Task.WaitAll(tasks.ToArray());
		}

		public Plugin Load(string name)
		{
			Plugin newPlugin = null;

			foreach (var module in m_scripting) {
				var o = module.TryCreateObject(name);
				if (o == null) {
					continue;
				}
				newPlugin = (Plugin)o;
				break;
			}

			if (newPlugin == null) {
				m_logger.Warn("Unknown plugin: '{0}'", name);
				return null;
			}

			newPlugin.m_config = m_config;
			newPlugin.m_remote = m_remote;
			newPlugin.m_database = m_database;

			if (!newPlugin.Supports(m_game.m_gameType, m_game.m_serverScript)) {
				return null;
			}

			Add(newPlugin);
			return newPlugin;
		}

		public void Add(Plugin plugin)
		{
			if (m_initialized) {
				throw new Exception("Can't add new plugins when plugins are already initialized.");
			}
			lock (m_plugins) {
				m_plugins.Add(plugin);
			}
		}

		public void Initialize()
		{
			var tasks = new List<Task>();
			foreach (var plugin in m_plugins) {
				tasks.Add(Task.Factory.StartNew(() => {
					plugin.Initialize();
				}));
			}
			m_logger.Debug("Waiting for Initializing tasks to complete");
			Task.WaitAll(tasks.ToArray());
			m_logger.Debug("Done!");
			m_initialized = true;
		}

		public void Uninitialize()
		{
			if (!m_initialized) {
				throw new Exception("Plugins aren't initialized in the first place.");
			}
			lock (m_plugins) {
				while (m_plugins.Count > 0) {
					m_plugins[0].Uninitialize();
					m_plugins.RemoveAt(0);
				}
			}
			m_initialized = false;
		}

		public Plugin GetPlugin(string name)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					if (plugin.GetType().Name == name) {
						return plugin;
					}
				}
			}
			return null;
		}

		public void EverySecond()
		{
			//TODO: Change locking to individual plugin? It's deadlocking here and on Uninitialize :S
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(plugin.EverySecond);
				}
			}
		}

		public void OnBeginChallenge()
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(plugin.OnBeginChallenge);
				}
			}
		}

		public void OnEndChallenge()
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(plugin.OnEndChallenge);
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

		public void OnEndRound()
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnEndRound();
					});
				}
			}
		}

		public void OnNextMap(Map map)
		{
			lock (m_plugins) {
				foreach (var plugin in m_plugins) {
					Task.Factory.StartNew(() => {
						plugin.OnNextMap(map);
					});
				}
			}
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
