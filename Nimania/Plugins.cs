using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using Nimania.Runtime.DbModels;
using System.IO;
using NLog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;

namespace Nimania
{
	public class PluginManager
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		//TODO: This should not be public, but Developer plugin uses this!
		public List<Plugin> m_plugins = new List<Plugin>();
		private bool m_initialized = false;

		public int m_errorCount = 0;

		private ConfigFile m_config;
		private GbxRemote m_remote;
		private DbDriver m_database;

		//private List<AsmHelper> m_scripting = new List<AsmHelper>();

		public PluginManager(ConfigFile config, GbxRemote remote, DbDriver dbDriver)
		{
			var dd = typeof(Enumerable).GetTypeInfo().Assembly.Location;
			var coreDir = Directory.GetParent(dd);

			CSharpCompilation compiler = CSharpCompilation.Create("Nimania.Plugins")
				.WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
				.AddReferences(
				MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location), // System.Private.CoreLib
				MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).GetTypeInfo().Assembly.Location), // System.Linq
				MetadataReference.CreateFromFile(typeof(GbxRemoteNet.GbxValue).GetTypeInfo().Assembly.Location), // GbxRemoteNet
				MetadataReference.CreateFromFile(typeof(Nimania.PluginManager).GetTypeInfo().Assembly.Location), // Nimania
				MetadataReference.CreateFromFile(typeof(Nimania.Runtime.Plugin).GetTypeInfo().Assembly.Location), // Nimania.Runtime
				MetadataReference.CreateFromFile(typeof(NLog.Logger).GetTypeInfo().Assembly.Location) // NLog
				);

			m_config = config;
			m_remote = remote;
			m_database = dbDriver;

			string[] scriptPaths = Directory.GetFiles("Data\\Plugins\\", "*.cs", SearchOption.AllDirectories);

			var tasks = new List<Task>();
			foreach (var path in scriptPaths) {
				using (FileStream fs = File.OpenRead(path)) {
					try {
						compiler = compiler.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Microsoft.CodeAnalysis.Text.SourceText.From(fs), null, path));
						m_logger.Info("Compiled script: {0}", path);
					} catch (Exception ex) {
						m_logger.Error("Couldn't compile script: {0} - {1}", path, ex.Message);
					}
				}
			}

			try {
				using (MemoryStream ms = new MemoryStream()) {
					var result = compiler.Emit(ms);
					if (!result.Success) {
						foreach (var diag in result.Diagnostics) {
							m_logger.Error("Compilation error: {0}", diag.ToString());
						}
					} else {
						AssemblyLoadContext.Default.LoadFromStream(ms);
					}
				}
			} catch (Exception ex) {
				m_logger.Error("Failed to load script assembly: {0}", ex.Message);
			}

			m_logger.Info("Done compiling all scripts!");
		}

		public Plugin Load(string name)
		{
			Plugin newPlugin = null;

			/*
			foreach (var module in m_scripting) {
				var o = module.TryCreateObject(name);
				if (o == null) {
					continue;
				}
				newPlugin = (Plugin)o;
				break;
			}
			*/

			if (newPlugin == null) {
				m_logger.Warn("Unknown plugin: '{0}'", name);
				return null;
			}

			newPlugin.m_config = m_config;
			newPlugin.m_remote = m_remote;
			newPlugin.m_database = m_database;
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
