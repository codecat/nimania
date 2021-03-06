﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime.DbModels;

namespace Nimania.Runtime
{
	public enum GameType
	{
		Unknown,

		TrackMania,
		ShootMania
	}

	public class GameInfo
	{
		public UserData m_userData = new UserData();

		public Map m_currentMap;
		public List<PlayerInfo> m_players = new List<PlayerInfo>();
		public List<Map> m_maps = new List<Map>();
		public List<string> m_queue = new List<string>();

		public GameType m_gameType;
		public string m_serverPack;
		public string m_serverScript;

		public string m_serverIP;
		public int m_serverPort;
		public string m_serverLogin;
		public string m_serverName;
		public string m_serverComment;
		public bool m_serverPrivate;
		public int m_serverMaxPlayers;
		public int m_serverMaxSpecs;
		public int m_serverGameMode;

		public int GetPlayerCount()
		{
			lock (m_players) {
				return m_players.Count;
			}
		}

		public int GetSpectatorCount()
		{
			int ret = 0;
			lock (m_players) {
				foreach (var player in m_players) {
					if (player.m_spectating) {
						ret++;
					}
				}
			}
			return ret;
		}

		public PlayerInfo GetPlayer(int id)
		{
			lock (m_players) {
				foreach (var player in m_players) {
					if (player.m_id == id) {
						return player;
					}
				}
			}
			return null;
		}

		public PlayerInfo GetPlayer(string login)
		{
			lock (m_players) {
				foreach (var player in m_players) {
					if (player.m_login == login) {
						return player;
					}
				}
			}
			return null;
		}

		public bool IsQueued(Map map)
		{
			return m_queue.Contains(map.UId);
		}

		public void QueueMap(Map map)
		{
			m_queue.Add(map.UId);
		}
	}
}
