using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

//TODO: Better (thread-safe) logging

namespace Nimania
{
	public class Controller
	{
		public ConfigFile m_config;
		public GbxRemote m_remote;

		public PluginManager m_plugins;

		public Controller(string configFilename)
		{
			m_config = new ConfigFile(configFilename);
		}

		public void Stop()
		{
			m_remote.EnableCallbacks(false);
			m_plugins.Uninitialize();
			m_remote.Execute("SendHideManialinkPage");
			m_remote.Terminate();
		}

		public void Reload()
		{
			m_remote.Execute("ChatSendServerMessage", "$fffNimania: $666Reloading");
			Stop();
			Program.Running = false;
		}

		public void Shutdown()
		{
			m_remote.Execute("ChatSendServerMessage", "$fffNimania: $666Shutting down");
			Stop();
			Program.Shutdown = true;
			Program.Running = false;
		}

		public void Run()
		{
			GbxRemote.ReportDebug = m_config.GetBool("Debug.GbxRemote");

			m_remote = new GbxRemote();
			m_remote.Connect(m_config["Server.Host"], m_config.GetInt("Server.Port"));

			bool loginOK = false;
			m_remote.Query("Authenticate", (GbxResponse res) => {
				if (res == null) {
					Console.WriteLine("Authentication failed!");
					return;
				}
				loginOK = true;
			}, m_config["Server.Username"], m_config["Server.Password"]).Wait();

			if (!loginOK) {
				return;
			}

			m_remote.Execute("ChatSendServerMessage", "$fffNimania: $666Starting 1.000");
			m_remote.Execute("SendHideManialinkPage");

			m_remote.AddCallback("TrackMania.PlayerManialinkPageAnswer", (GbxCallback cb) => {
				string login = cb.m_params[1].Get<string>();
				string action = cb.m_params[2].Get<string>();

				Console.WriteLine("User \"" + login + "\" called action \"" + action + "\"");

				string[] parse = action.Split(new char[] { '.' }, 2);
				if (parse.Length != 2) {
					Console.WriteLine("Invalid action format, must be like: \"Plugin.ActionName\"");
					return;
				}

				var plugin = m_plugins.GetPlugin(parse[0]);
				if (plugin == null) {
					Console.WriteLine("Plugin \"" + parse[0] + "\" not found!");
					return;
				}

				plugin.OnAction(login, parse[1]);
			});

			m_remote.AddCallback("TrackMania.PlayerChat", (GbxCallback cb) => {
				string login = cb.m_params[1].Get<string>();
				string message = cb.m_params[2].Get<string>();
				if (login == "ansjh") {
					switch (message) {
						case "/reload": Reload(); break;
						case "/shutdown": Shutdown(); break;
					}
				}
			});

			Console.WriteLine("Loading plugins..");
			m_plugins = new PluginManager(m_remote);
			var pluginNames = m_config.GetArray("Plugins", "Plugin");
			foreach (var name in pluginNames) {
				m_plugins.Load(name);
			}
			m_plugins.Initialize();

			m_remote.EnableCallbacks(true);
		}
	}
}
