using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime;
using NLog;

namespace Nimania.Plugins
{
	public class Admin : Plugin
	{
		public override void Initialize()
		{
			SendWidget();
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			if (player.IsAdmin) {
				SendWidget(player.m_login);
			}
		}

		public void SendWidget()
		{
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					if (player.IsAdmin) {
						SendViewToLogin(player.m_login, "Admin/Bar.xml");
					}
				}
			}
		}

		public void SendWidget(string login)
		{
			SendViewToLogin(login, "Admin/Bar.xml");
		}

		public override void OnAction(PlayerInfo player, string action, string[] args)
		{
			if (!player.IsAdmin) {
				m_logger.Warn("User {0} tried accessing admin controls, not allowed!", player.m_login);
				return;
			}
			switch (action) {
				case "RestartMap":
					SendChat(string.Format(m_config["Messages.Admin.RestartMap"], player.m_localPlayer.Group.Name, player.m_nickname));
					m_remote.Execute("RestartMap");
					break;

				case "ForceEndRound":
					SendChat(string.Format(m_config["Messages.Admin.ForceEndRound"], player.m_localPlayer.Group.Name, player.m_nickname));
					m_remote.Execute("ForceEndRound");
					break;

				case "NextMap":
					SendChat(string.Format(m_config["Messages.Admin.NextMap"], player.m_localPlayer.Group.Name, player.m_nickname));
					m_remote.Execute("NextMap");
					break;
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
