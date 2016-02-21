﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using NLog;

namespace Nimania.Runtime
{
	public abstract class Plugin
	{
		private static Logger m_pluginLogger = LogManager.GetCurrentClassLogger();

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
		public virtual void OnAction(PlayerInfo player, string action) { }

		public virtual void OnBeginChallenge() { }
		public virtual void OnEndChallenge() { }

		public virtual void OnPlayerConnect(PlayerInfo player) { }
		public virtual void OnPlayerDisconnect(PlayerInfo player) { }

		public virtual void OnEndRound() { }

		// Trackmania synced callbacks
		public virtual void OnPlayerBegin(PlayerInfo player) { } // called on retire and begin of 321go
		public virtual void OnPlayerCheckpoint(PlayerInfo player, int n, int time) { }
		public virtual void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints) { }

		public void SendView(string file, params string[] kvs) { SendView(file, 0, false, kvs); }
		public void SendView(string file, int timeout, params string[] kvs) { SendView(file, timeout, false, kvs); }
		public void SendView(string file, int timeout, bool clickHides, params string[] kvs)
		{
			string xml = GetView(file, kvs);
			if (xml == "") {
				return;
			}
			m_remote.Execute("SendDisplayManialinkPage", xml, timeout, clickHides);
		}

		public void SendViewToLogin(string login, string file, params string[] kvs) { SendViewToLogin(login, file, 0, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, params string[] kvs) { SendViewToLogin(login, file, timeout, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, bool clickHides, params string[] kvs)
		{
			string xml = GetView(file, kvs);
			if (xml == "") {
				return;
			}
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

		public string GetView(string file, params string[] kvs)
		{
			if (kvs.Length % 2 != 0) {
				throw new Exception("Uneven amount of strings passed to SendView!");
			}
#if DEBUG
			string xmlFilename = "../../Data/Views/" + file;
#else
			string xmlFilename = "Data/Views/" + file;
#endif
			if (!File.Exists(xmlFilename)) {
				m_pluginLogger.Warn("View not found: " + file);
				return "";
			}
			string xml = File.ReadAllText(xmlFilename);
			for (int i = 0; i < kvs.Length; i += 2) {
				xml = xml.Replace("<?=" + kvs[i] + "?>", kvs[i + 1]);
			}
			return xml;
		}
	}
}
