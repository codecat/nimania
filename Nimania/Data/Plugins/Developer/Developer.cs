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
			SendWidget();
		}

		public override void Uninitialize()
		{
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			if (player.IsDeveloper) {
				SendWidget(player.m_login);
			}
		}

		public void SendWidget()
		{
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					if (player.IsDeveloper) {
						SendWidget(player.m_login);
					}
				}
			}
		}

		public void SendWidget(string login)
		{
			var pluginManager = Program.CurrentController.m_plugins;
			SendViewToLogin(login, "Developer/Bar.xml",
				"plugin_count", pluginManager.m_plugins.Count.ToString(),
				"plugin_errors", pluginManager.m_errorCount.ToString());
		}

		public override void OnAction(PlayerInfo player, string action)
		{
			if (!player.IsDeveloper) {
				Console.WriteLine("User " + player.m_login + " tried accessing developer controls, not allowed!");
				return;
			}
			switch (action) {
				case "Reload": Program.CurrentController.Reload(); break;
				case "Shutdown": Program.CurrentController.Shutdown(); break;
				case "SoftReload": Program.CurrentController.SoftReload(); break;
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
