using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime.DbModels;

namespace Nimania.Runtime
{
	public class GameInfo
	{
		public Map m_currentMap;
		public List<PlayerInfo> m_players = new List<PlayerInfo>();

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
	}
}
