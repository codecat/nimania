using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using Nimania.Runtime.DbDrivers;
using Nimania.Runtime.DbModels;

//TODO: Better (thread-safe) logging

namespace Nimania
{
	public class Controller
	{
		public ConfigFile m_config;
		public GbxRemote m_remote;
		public DbDriver m_database;

		private PluginManager m_plugins;

		public GameInfo m_game;

		private bool m_runningTimer;

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

			string dbDriverName = m_config["Database.Driver"];
			switch (dbDriverName.ToLower()) {
				case "mysql": m_database = new Mysql(m_config); break;
			}

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

			SetupCore();

			Console.WriteLine("Loading plugins..");
			m_plugins = new PluginManager(m_config, m_remote, m_database);
			var pluginNames = m_config.GetArray("Plugins", "Plugin");
			foreach (var name in pluginNames) {
				var newPlugin = m_plugins.Load(name);
				if (newPlugin != null) {
					newPlugin.m_game = m_game;
				}
			}
			m_plugins.Initialize();

			m_remote.EnableCallbacks(true);
		}

		private void TimerThread()
		{
			while (m_runningTimer) {
				m_plugins.EverySecond();
				Thread.Sleep(1000);
			}
		}

		private void SetupCore()
		{
			m_game = new GameInfo();

			var timerThread = new Thread(TimerThread);
			timerThread.Start();

			m_remote.AddCallback("TrackMania.PlayerManialinkPageAnswer", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
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

				var player = m_game.GetPlayer(id);
				if (player == null) {
					Debug.Assert(false);
					return;
				}

				plugin.OnAction(player, parse[1]);
			});

			m_remote.AddCallback("TrackMania.PlayerChat", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
				string message = cb.m_params[2].Get<string>();
				bool command = cb.m_params[3].Get<bool>();

				//TODO: Change to 'command', because on local servers, command is always false
				if (true) { // command) {
					var player = m_game.GetPlayer(id);

					switch (message) {
						case "/reload": if (player.IsDeveloper) { Reload(); } break;
						case "/shutdown": if (player.IsDeveloper) { Shutdown(); } break;
						case "/playtime":
							var pi = m_game.GetPlayer(id);
							m_remote.Execute("ChatSendServerMessageToId", "$fffYou have played for: $666" + Utils.TimeStringHMS((int)(DateTime.Now - pi.m_joinTime).TotalSeconds), id);
							break;
					}
				}
			});

			// Wait for these because plugin Initializers might need it
			{
				var results = m_remote.MultiQueryWait("GetSystemInfo", // 0
					"GetServerName", "GetServerComment", "GetHideServer", "GetMaxPlayers", "GetMaxSpectators", "GetGameMode", // 1-6
					"GetCurrentMapInfo" // 7
				);

				m_game.m_serverIP = results[0].m_value.Get<string>("PublishedIp");
				m_game.m_serverPort = results[0].m_value.Get<int>("Port");
				m_game.m_serverLogin = results[0].m_value.Get<string>("ServerLogin");

				m_game.m_serverName = results[1].m_value.Get<string>();
				m_game.m_serverComment = results[2].m_value.Get<string>();
				m_game.m_serverPrivate = results[3].m_value.Get<int>() == 1;
				m_game.m_serverMaxPlayers = results[4].m_value.Get<int>("CurrentValue");
				m_game.m_serverMaxSpecs = results[5].m_value.Get<int>("CurrentValue");
				m_game.m_serverGameMode = results[6].m_value.Get<int>();

				m_game.m_currentMap = LoadMapInfo(results[7].m_value);

				results = m_remote.MultiQueryWait(new GbxMultiCall() {
					m_methodName = "GetPlayerList", // 0
					m_methodParams = new int[] { 255, 0, 0 }
				}, new GbxMultiCall() {
					m_methodName = "GetCurrentRanking", // 1
					m_methodParams = new int[] { 255, 0 }
				});

				{
					var players = results[0].m_value.Get<ArrayList>();
					foreach (GbxValue player in players) {
						m_game.m_players.Add(LoadPlayerInfo(player));
					}
				}

				{
					var players = results[1].m_value.Get<ArrayList>();
					foreach (GbxValue player in players) {
						int id = player.Get<int>("PlayerId");

						var ply = m_game.GetPlayer(id);
						if (ply != null) { // null happens if they are disconnected!
							ply.m_bestTime = player.Get<int>("BestTime");
							ply.m_prevBestTime = ply.m_bestTime;
							ply.m_lastTime = ply.m_bestTime;
							ply.m_score = player.Get<int>("Score");

							var cps = player.Get<ArrayList>("BestCheckpoints");
							foreach (GbxValue cp in cps) {
								int cpt = cp.Get<int>();
								ply.m_checkpoints.Add(cpt);
								ply.m_bestCheckpoints.Add(cpt);
							}
						}
					}
				}
			}

			m_remote.AddCallback("TrackMania.BeginChallenge", (GbxCallback cb) => {
				m_game.m_currentMap = LoadMapInfo(cb.m_params[0]);
				lock (m_game.m_players) {
					foreach (var player in m_game.m_players) {
						player.m_prevBestTime = -1;
						player.m_bestTime = -1;
						player.m_lastTime = -1;
					}
				}
				m_plugins.OnBeginChallenge();
			});

			m_remote.AddCallback("TrackMania.EndChallenge", (GbxCallback cb) => {
				m_plugins.OnEndChallenge();
			});

			m_remote.AddCallback("TrackMania.PlayerConnect", (GbxCallback cb) => {
				string login = cb.m_params[0].Get<string>();
				m_remote.Query("GetPlayerInfo", (GbxResponse res) => {
					var playerInfo = LoadPlayerInfo(res.m_value);
					lock (m_game.m_players) {
						m_game.m_players.Add(playerInfo);
					}
					m_plugins.OnPlayerConnect(playerInfo);
				}, login);
			});

			m_remote.AddCallback("TrackMania.PlayerInfoChanged", (GbxCallback cb) => {
				GbxValue val = cb.m_params[0];
				int id = val.Get<int>("PlayerId");

				var player = m_game.GetPlayer(id);
				if (player != null) {
					player.m_nickname = val.Get<string>("NickName"); // not sure if this ever changes but whatever
					player.m_team = val.Get<int>("TeamId");
					player.m_spectating = val.Get<int>("SpectatorStatus") > 0; //TODO: save the status (player id?) off somewhere for feature-sake
					player.m_ladder = val.Get<int>("LadderRanking");
					//TODO: figure out val.Get<int>("Flags"); (which is actually a real value like 1000100, not using bitflags.. thanks nadeo)
				}
			});

			m_remote.AddCallback("TrackMania.PlayerDisconnect", (GbxCallback cb) => {
				string login = cb.m_params[0].Get<string>();
				var player = m_game.GetPlayer(login);
				if (player != null) {
					m_plugins.OnPlayerDisconect(player);
				}
				lock (m_game.m_players) {
					for (int i = 0; i < m_game.m_players.Count; i++) {
						if (m_game.m_players[i].m_login == login) {
							m_game.m_players.RemoveAt(i);
						}
					}
				}
			});

			m_remote.AddCallback("TrackMania.PlayerCheckpoint", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
				int time = cb.m_params[2].Get<int>();
				int n = cb.m_params[4].Get<int>();

				var player = m_game.GetPlayer(id);
				if (player == null) {
					Debug.Assert(false);
					return;
				}

				if (n + 1 > player.m_checkpoints.Count) {
					player.m_checkpoints.Add(time);
				}
				m_plugins.OnPlayerCheckpoint(player, n, time);
			});

			m_remote.AddCallback("TrackMania.PlayerFinish", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
				int time = cb.m_params[2].Get<int>();

				var player = m_game.GetPlayer(id);
				if (player == null) {
					// somehow, this happens to be the dedicated server (with dedi login) doing time 0 on retire..??
					//Debug.Assert(false);
					return;
				}

				if (time == 0) {
					player.m_checkpoints.Clear();
					m_plugins.OnPlayerBegin(player);
					return;
				}

				if (time < player.m_bestTime || player.m_bestTime == -1) {
					player.m_prevBestTime = player.m_bestTime;
					player.m_bestTime = time;
					player.m_bestCheckpoints.Clear();
					player.m_bestCheckpoints.AddRange(player.m_checkpoints);
					//TODO: Distinct between all/lap checkpoints
				}
				player.m_lastTime = time;
				m_plugins.OnPlayerFinish(player, time, player.m_checkpoints.ToArray());
			});
		}

		public PlayerInfo LoadPlayerInfo(GbxValue val)
		{
			var player = new PlayerInfo();
			player.m_login = val.Get<string>("Login");
			player.m_nickname = val.Get<string>("NickName");
			player.m_id = val.Get<int>("PlayerId");
			player.m_team = val.Get<int>("TeamId");
			player.m_spectating = val.Get<bool>("IsSpectator");
			player.m_officialMode = val.Get<bool>("IsInOfficialMode");
			player.m_ladder = val.Get<int>("LadderRanking");

			player.m_joinTime = DateTime.Now;

			player.m_localPlayer = m_database.FindByAttributes<LocalPlayer>("Login", player.m_login);
			if (player.m_localPlayer == null) {
				player.m_localPlayer = m_database.Create<LocalPlayer>();
				player.m_localPlayer.Login = player.m_login;
			}

			player.m_localPlayer.Nickname = player.m_nickname;
			player.m_localPlayer.Save();

			return player;
		}

		public Map LoadMapInfo(GbxValue val)
		{
			string uid = val.Get<string>("UId");
			var map = m_database.FindByAttributes<Map>("UId", uid);
			if (map == null) {
				map = m_database.Create<Map>();
				map.UId = uid;
				map.Name = val.Get<string>("Name");
				map.Author = val.Get<string>("Author");
				map.FileName = val.Get<string>("FileName");
				map.Save();
			}
			return map;
		}
	}
}
