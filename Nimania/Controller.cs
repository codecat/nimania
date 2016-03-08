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
using NLog;

//TODO: Better (thread-safe) logging

namespace Nimania
{
	public class Controller
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		public ConfigFile m_config;
		public GbxRemote m_remote;
		public DbDriver m_database;

		//TODO: This should not be public, but Developer plugin uses this!
		public PluginManager m_plugins;

		public GameInfo m_game;

		private bool m_runningTimer = true;

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

		public void SoftReload()
		{
			m_remote.Execute("ChatSendServerMessage", "$fffNimania: $666Soft-reloading");
			lock (m_plugins.m_plugins) {
				foreach (var plugin in m_plugins.m_plugins) {
					plugin.SoftReload();
				}
			}
		}

		public void Run()
		{
			string dbDriverName = m_config["Database.Driver"];
			switch (dbDriverName.ToLower()) {
				case "mysql": m_database = new Mysql(m_config); break;
				default: throw new Exception("Unknown database driver: " + dbDriverName);
			}

			m_remote = new GbxRemote();
			m_remote.Connect(m_config["Server.Host"], m_config.GetInt("Server.Port"));

			bool loginOk = false;
			m_remote.Query("Authenticate", (GbxValue res) => {
				if (res == null) {
					m_logger.Fatal("Authentication failed!");
					return;
				}
				loginOk = true;
			}, m_config["Server.Username"], m_config["Server.Password"]).Wait();

			if (!loginOk) {
				return;
			}

			m_remote.Execute("ChatSendServerMessage", "$fffNimania: $666Starting");
			m_remote.Execute("SendHideManialinkPage");

			SetupCore();

			m_logger.Info("Loading plugins..");
			m_plugins = new PluginManager(m_config, m_remote, m_database);
			var pluginNames = m_config.GetArray("Plugins", "Plugin");
			foreach (var name in pluginNames) {
				var newPlugin = m_plugins.Load(name);
				if (newPlugin != null) {
					newPlugin.m_game = m_game;
				} else {
					m_remote.Execute("ChatSendServerMessage", "$fffNimania: $f00Failed $fffto load plugin $666" + name);
				}
			}
			m_plugins.Initialize();

			var timerThread = new Thread(TimerThread);
			timerThread.Start();

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

			m_remote.AddCallback("TrackMania.PlayerManialinkPageAnswer", (GbxValue[] cb) => {
				int id = cb[0].Get<int>();
				string login = cb[1].Get<string>();
				string action = cb[2].Get<string>();

				m_logger.Debug("User \"{0}\" called action \"{1}\"", login, action);

				string[] parse = action.Split(new[] { '.' });
				if (parse.Length < 2) {
					m_logger.Error("Invalid action format \"{0}\", must be like: \"Plugin.ActionName[.arg1.arg2]\"", action);
					return;
				}

				var plugin = m_plugins.GetPlugin(parse[0]);
				if (plugin == null) {
					m_logger.Error("Plugin \"{0}\" not found for action \"{1}\"", parse[0], action);
					return;
				}

				var player = m_game.GetPlayer(id);
				if (player == null) {
					Utils.Assert(false);
					return;
				}

				plugin.OnAction(player, parse[1], parse.Skip(2).ToArray());
			});

			// Wait for these because plugin Initializers might need it
			{
				var results = m_remote.MultiQueryWait("GetSystemInfo", // 0
					"GetServerName", "GetServerComment", "GetHideServer", "GetMaxPlayers", "GetMaxSpectators", "GetGameMode", // 1-6
					"GetCurrentMapInfo" // 7
				);

				m_game.m_serverIP = results[0].Get<string>("PublishedIp");
				m_game.m_serverPort = results[0].Get<int>("Port");
				m_game.m_serverLogin = results[0].Get<string>("ServerLogin");

				m_game.m_serverName = results[1].Get<string>();
				m_game.m_serverComment = results[2].Get<string>();
				m_game.m_serverPrivate = results[3].Get<int>() == 1;
				m_game.m_serverMaxPlayers = results[4].Get<int>("CurrentValue");
				m_game.m_serverMaxSpecs = results[5].Get<int>("CurrentValue");
				m_game.m_serverGameMode = results[6].Get<int>();

				m_game.m_currentMap = LoadMapInfo(results[7]);

				results = m_remote.MultiQueryWait(new GbxMultiCall() {
					m_methodName = "GetPlayerList", // 0
					m_methodParams = new[] { 255, 0, 0 }
				}, new GbxMultiCall() {
					m_methodName = "GetCurrentRanking", // 1
					m_methodParams = new[] { 255, 0 }
				}, new GbxMultiCall() {
					m_methodName = "GetMapList", // 2
					m_methodParams = new[] { 9999, 0 }
				});

				{
					var players = results[0].Get<ArrayList>();
					foreach (GbxValue player in players) {
						m_game.m_players.Add(LoadPlayerInfo(player));
					}
				}

				{
					var players = results[1].Get<ArrayList>();
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

				{
					var maps = results[2].Get<ArrayList>();
					foreach (GbxValue map in maps) {
						m_game.m_maps.Add(LoadMapInfo(map));
					}
				}
			}

			m_remote.AddCallback("TrackMania.BeginChallenge", (GbxValue[] cb) => {
				m_game.m_currentMap = LoadMapInfo(cb[0]);
				lock (m_game.m_players) {
					foreach (var player in m_game.m_players) {
						player.m_prevBestTime = -1;
						player.m_bestTime = -1;
						player.m_lastTime = -1;
					}
					// Remove disconnected players
					for (int i = 0; i < m_game.m_players.Count; i++) {
						if (!m_game.m_players[i].m_connected) {
							m_logger.Debug("Removing disconnected player '" + m_game.m_players[i].m_login + "'");
							m_game.m_players.RemoveAt(i);
							i--;
						}
					}
				}
				m_remote.Query("GetGameMode", (GbxValue res) => {
					m_game.m_serverGameMode = res.Get<int>();
					m_plugins.OnBeginChallenge();
				});
			});

			m_remote.AddCallback("TrackMania.EndRound", (GbxValue[] cb) => {
				m_remote.Query("GetCurrentRanking", (GbxValue res) => {
					var players = res.Get<ArrayList>();
					foreach (GbxValue player in players) {
						if (player.Get<int>("PlayerId") == 255) {
							continue;
						}
						var login = player.Get<string>("Login");
						var ply = m_game.GetPlayer(login);
						if (ply == null) {
							//TODO: wtf?
							continue;
						}
						ply.m_score = player.Get<int>("Score");
					}
					m_plugins.OnEndRound();
				}, 255, 0);
			});

			m_remote.AddCallback("TrackMania.EndChallenge", (GbxValue[] cb) => {
				m_plugins.OnEndChallenge();

				if (m_game.m_queue.Count > 0) {
					var uid = m_game.m_queue[0];
					m_game.m_queue.RemoveAt(0);
					m_remote.Execute("SetNextMapIdent", uid);

					var map = m_database.FindByAttributes<Map>("UId", uid);
					if (map != null) {
						m_plugins.OnNextMap(map);
					}
				} else {
					m_remote.Query("GetNextMapInfo", (GbxValue res) => {
						var uid = res.Get<string>("UId");
						var map = m_database.FindByAttributes<Map>("UId", uid);
						if (map != null) {
							m_plugins.OnNextMap(map);
						}
					});
				}
			});

			m_remote.AddCallback("TrackMania.PlayerConnect", (GbxValue[] cb) => {
				string login = cb[0].Get<string>();
				m_remote.Query("GetPlayerInfo", (GbxValue res) => {
					var player = m_game.GetPlayer(res.Get<string>("Login"));
					if (player == null) {
						player = LoadPlayerInfo(res);
						lock (m_game.m_players) {
							m_game.m_players.Add(player);
						}
					} else {
						player.m_id = res.Get<int>("PlayerId");
						player.m_nickname = res.Get<string>("NickName");
						player.m_spectating = res.Get<bool>("IsSpectator");
					}
					m_plugins.OnPlayerConnect(player);
				}, login);
			});

			m_remote.AddCallback("TrackMania.PlayerInfoChanged", (GbxValue[] cb) => {
				GbxValue val = cb[0];
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

			m_remote.AddCallback("TrackMania.PlayerDisconnect", (GbxValue[] cb) => {
				string login = cb[0].Get<string>();
				var player = m_game.GetPlayer(login);
				if (player != null) {
					m_plugins.OnPlayerDisconect(player);
				}
				lock (m_game.m_players) {
					for (int i = 0; i < m_game.m_players.Count; i++) {
						if (m_game.m_players[i].m_login == login) {
							m_game.m_players[i].m_connected = false;
						}
					}
				}
			});

			m_remote.AddCallback("TrackMania.PlayerCheckpoint", (GbxValue[] cb) => {
				string login = cb[1].Get<string>();
				int time = cb[2].Get<int>();
				int n = cb[4].Get<int>();

				var player = m_game.GetPlayer(login);
				if (player == null) {
					//TODO: Fix this! Happens randomly when a player leaves and rejoins (in the same session)? Not entirely sure, but something like that!
					//Utils.Assert(false);
					return;
				}

				if (n + 1 > player.m_checkpoints.Count || m_game.m_currentMap.m_laps) {
					player.m_checkpoints.Add(time);
				}
				m_plugins.OnPlayerCheckpoint(player, n, time);
			});

			m_remote.AddCallback("TrackMania.PlayerFinish", (GbxValue[] cb) => {
				int id = cb[0].Get<int>();
				int time = cb[2].Get<int>();

				var player = m_game.GetPlayer(id);
				if (player == null) {
					// somehow, this happens to be the dedicated server (with dedi login) doing time 0 on retire..??
					//Utils.Assert(false);
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
					player.m_bestCheckpointsLap.Clear();
					player.m_bestCheckpointsLap.AddRange(player.m_checkpoints.Skip(Math.Max(0, player.m_checkpoints.Count() - m_game.m_currentMap.m_nCheckpoints)));
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

			// Sadly, there are times when we don't get as much info as we might want.
			val.TryGet("NbCheckpoints", ref map.m_nCheckpoints);

			val.TryGet("BronzeTime", ref map.m_timeBronze);
			val.TryGet("SilverTime", ref map.m_timeSilver);
			val.TryGet("AuthorTime", ref map.m_timeAuthor);
			val.TryGet("LapRace", ref map.m_laps);

			// But this one does make the cut. Thanks Nadeo.
			map.m_timeGold = val.Get<int>("GoldTime");
			return map;
		}
	}
}
