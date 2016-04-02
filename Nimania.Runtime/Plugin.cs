﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using NLog;
using Nimania.Runtime.DbModels;

namespace Nimania.Runtime
{
	public abstract class Plugin
	{
		protected Logger m_logger { get; private set; }
		protected Plugin()
		{
			m_logger = LogManager.GetLogger(GetType().FullName);
		}

		public ConfigFile m_config;
		public GbxRemote m_remote;
		public DbDriver m_database;

		public GameInfo m_game;

		public static Random m_random = new Random();

		public virtual void Initialize() { }
		public virtual void Uninitialize() { }

		public virtual void EverySecond() { }

		// Soft reload, typically called from developer UI, should usually only resend widgets
		public virtual void SoftReload() { }

		// Synced callbacks
		public virtual void OnAction(PlayerInfo player, string action, string[] args) { }

		public virtual void OnBeginChallenge() { }
		public virtual void OnEndChallenge() { }

		public virtual void OnPlayerConnect(PlayerInfo player) { }
		public virtual void OnPlayerDisconnect(PlayerInfo player) { }

		public virtual void OnEndRound() { }

		public virtual void OnNextMap(Map map) { }

		// Trackmania synced callbacks
		public virtual void OnPlayerBegin(PlayerInfo player) { } // called on retire and begin of 321go
		public virtual void OnPlayerCheckpoint(PlayerInfo player, int n, int time) { }
		public virtual void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints) { }

		public void SendView(string file, params object[] kvs) { SendView(file, 0, false, kvs); }
		public void SendView(string file, int timeout, params object[] kvs) { SendView(file, timeout, false, kvs); }
		public void SendView(string file, int timeout, bool clickHides, params object[] kvs)
		{
			string xml = GetView(file, kvs);
			if (xml == "") {
				return;
			}
			m_logger.Debug("Sending view {0} to all players", file);
			m_remote.Execute("SendDisplayManialinkPage", xml, timeout, clickHides);
		}

		public void SendViewToLogin(string login, string file, params object[] kvs) { SendViewToLogin(login, file, 0, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, params object[] kvs) { SendViewToLogin(login, file, timeout, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, bool clickHides, params object[] kvs)
		{
			string xml = GetView(file, kvs);
			if (xml == "") {
				return;
			}
			m_logger.Debug("Sending view {0} to {1}", file, login);
			m_remote.Execute("SendDisplayManialinkPageToLogin", login, xml, timeout, clickHides);
		}

		public void SendChat(string s)
		{
			m_remote.Execute("ChatSendServerMessage", s);
		}

		public void SendChatTo(string login, string text)
		{
			m_remote.Execute("ChatSendServerMessageToLogin", text, login);
		}

		public void SendChatTo(int id, string text)
		{
			m_remote.Execute("ChatSendServerMessageToId", text, id);
		}

		public string GetView(string file, params object[] kvs)
		{
			if (kvs.Length % 2 != 0) {
				throw new Exception("Uneven amount of params passed to SendView!");
			}
#if DEBUG
			string xmlFilename = "../../Data/Views/" + file;
#else
			string xmlFilename = "Data/Views/" + file;
#endif

			if (!File.Exists(xmlFilename)) {
				m_logger.Warn("View not found: " + file);
				return "";
			}
			var lines = File.ReadAllLines(xmlFilename);
			var xml = new StringBuilder();
			bool skipLines = false;
			for (int i = 0; i < lines.Length; i++) {
				var line = lines[i];

				if (line.StartsWith("%if")) {
					var parse = line.SplitCommandline();
					var key = parse.Get(1);
					var op = parse.Get(2, "===");
					var value = parse.Get(3, "True");

					bool found = false;
					for (int j = 0; j < kvs.Length; j++) {
						if (kvs[j].ToString() != key) {
							continue;
						}
						found = true;

						if (op == "==") {
							skipLines = !(kvs[j + 1].ToString().ToLower() == value.ToLower());
						} else if (op == "===") {
							skipLines = !(kvs[j + 1].ToString() == value);
						} else if (op == "!=") {
							skipLines = (kvs[j + 1].ToString().ToLower() == value.ToLower());
						} else if (op == "!==") {
							skipLines = (kvs[j + 1].ToString() == value);
						}
						break;
					}

					if (!found) {
						m_logger.Warn("Encountered %if with unexisting key '{0}' in view '{1}'", key, file);
						skipLines = true;
					}
				} else if (line == "%endif") {
					skipLines = false;
				} else if (!skipLines) {
					for (int j = 0; j < kvs.Length; j += 2) {
						line = line.Replace("<?=" + kvs[j].ToString() + "?>", kvs[j + 1].ToString());
					}
					xml.Append(line.Trim() + "\n");
				}
			}
			return xml.ToString();
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
