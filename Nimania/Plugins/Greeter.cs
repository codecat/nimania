using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Greeter : Plugin
	{
		public string[] m_joinMessages;
		public string[] m_leaveMessages;

		public override void Initialize()
		{
			m_joinMessages = m_config.GetArray("Messages", "Greeter.JoinMessage");
			m_leaveMessages = m_config.GetArray("Messages", "Greeter.LeaveMessage");
		}

		public override void Uninitialize()
		{
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			m_remote.Query("GetDetailedPlayerInfo", (GbxResponse res) => {
				string[] path = res.m_value.Get<string>("Path").Split('|');

				string from = path.Last();
				if (path.Length > 3) {
					from = path[2];
				}

				player.m_localPlayer.Visits++;
				player.m_localPlayer.Save();

				string joinMessage = m_joinMessages[m_random.Next(m_joinMessages.Length)];
				string groupName = "";
				if (player.m_localPlayer.Group != null) {
					groupName = player.m_localPlayer.Group.Name;
				}
				SendChat(string.Format(joinMessage, groupName, player.m_nickname, from, player.m_localPlayer.Visits.ToString()));
			}, player.m_login);
		}

		public override void OnPlayerDisconnect(PlayerInfo player)
		{
			string leaveMessage = m_leaveMessages[m_random.Next(m_leaveMessages.Length)];
			int playtime = (int)(DateTime.Now - player.m_joinTime).TotalSeconds;
			string groupName = "";
			if (player.m_localPlayer.Group != null) {
				groupName = player.m_localPlayer.Group.Name;
			}
			SendChat(string.Format(leaveMessage, groupName, player.m_nickname, Utils.TimeStringHMS(playtime)));
		}
	}
}
