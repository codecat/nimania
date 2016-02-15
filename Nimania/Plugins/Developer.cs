using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Developer : Plugin
	{
		public override void Initialize()
		{
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					if (player.m_localPlayer.Group != null && player.m_localPlayer.Group.IsDeveloper) {
						SendViewToLogin(player.m_login, "Developer/Bar.xml");
					}
				}
			}
		}

		public override void Uninitialize()
		{
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			if (player.m_localPlayer.Group != null && player.m_localPlayer.Group.IsDeveloper) {
				SendViewToLogin(player.m_login, "Developer/Bar.xml");
			}
		}

		public override void OnAction(PlayerInfo player, string action)
		{
			if (player.m_localPlayer.Group == null || !player.m_localPlayer.Group.IsDeveloper) {
				Console.WriteLine("User " + player.m_login + " tried accessing developer controls, not allowed!");
				return;
			}
			switch (action) {
				case "Reload": Program.CurrentController.Reload(); break;
				case "Shutdown": Program.CurrentController.Shutdown(); break;
			}
		}
	}
}
