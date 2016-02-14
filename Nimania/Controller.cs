﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
				newPlugin.m_game = m_game;
			}
			m_plugins.Initialize();

			m_remote.EnableCallbacks(true);
		}

		private void SetupCore()
		{
			m_game = new GameInfo();

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
				int id = cb.m_params[0].Get<int>();
				string login = cb.m_params[1].Get<string>();
				string message = cb.m_params[2].Get<string>();
				if (login == "ansjh") {
					switch (message) {
						case "/reload": Reload(); break;
						case "/shutdown": Shutdown(); break;
						case "/playtime":
							var pi = m_game.GetPlayer(id);
							m_remote.Execute("ChatSendServerMessageToId", "$fffYou have played for: $666" + Utils.TimeStringHMS((int)(DateTime.Now - pi.m_joinTime).TotalSeconds), id);
							break;
					}
				}
			});

			// Wait for these because plugin Initializers might need it
			{
				m_remote.Query("GetSystemInfo", (GbxResponse res) => {
					m_game.m_serverIP = res.m_value.Get<string>("PublishedIp");
					m_game.m_serverPort = res.m_value.Get<int>("Port");
					m_game.m_serverLogin = res.m_value.Get<string>("ServerLogin");
				}).Wait();

				m_game.m_serverName = m_remote.QueryWait("GetServerName").m_value.Get<string>();
				m_game.m_serverComment = m_remote.QueryWait("GetServerComment").m_value.Get<string>();
				m_game.m_serverPrivate = m_remote.QueryWait("GetHideServer").m_value.Get<int>() == 1;
				m_game.m_serverMaxPlayers = m_remote.QueryWait("GetMaxPlayers").m_value.Get<int>("CurrentValue");
				m_game.m_serverMaxSpecs = m_remote.QueryWait("GetMaxSpectators").m_value.Get<int>("CurrentValue");
				m_game.m_serverGameMode = m_remote.QueryWait("GetGameMode").m_value.Get<int>();

				m_game.m_currentMap = LoadMapInfo(m_remote.QueryWait("GetCurrentMapInfo").m_value);

				m_remote.Query("GetPlayerList", (GbxResponse res) => {
					var players = res.m_value.Get<ArrayList>();
					foreach (GbxValue player in players) {
						m_game.m_players.Add(LoadPlayerInfo(player));
					}
				}, 255, 0, 0).Wait();
			}

			m_remote.AddCallback("TrackMania.BeginChallenge", (GbxCallback cb) => {
				m_game.m_currentMap = LoadMapInfo(cb.m_params[0]);
				m_plugins.OnBeginChallenge();
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
					Debug.Assert(false);
					return;
				}

				if (time == 0) {
					player.m_checkpoints.Clear();
					m_plugins.OnPlayerBegin(player);
					return;
				}

				if (time < player.m_bestTime) {
					player.m_bestTime = time;
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
