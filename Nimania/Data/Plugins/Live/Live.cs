using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Live : Plugin
	{
		public int m_gamemode;

		public override void Initialize()
		{
			ReloadMapInfo();
		}

		public override void OnBeginChallenge()
		{
			ReloadMapInfo();
		}

		public void ReloadMapInfo()
		{
			m_remote.Query("GetGameMode", (GbxResponse res) => {
				m_gamemode = res.m_value.Get<int>();
				SendWidget();
			});
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			SendWidget(player.m_login);
		}

		public override void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints)
		{
			if (time < player.m_prevBestTime || player.m_prevBestTime == -1) {
				SendWidget();
			}
		}

		public void SendWidget(string login = "")
		{
			// sadly, we are forced to send the entire thing every time it updates. :(
			string xmlItems = "";
			lock (m_game.m_players) {
				List<PlayerInfo> players = new List<PlayerInfo>();
				foreach (var player in m_game.m_players) {
					players.Add(player);
				}
				players.Sort((a, b) => {
					if (a.m_bestTime > b.m_bestTime || b.m_bestTime == -1) {
						return 1;
					} else if (a.m_bestTime < b.m_bestTime || a.m_bestTime == -1) {
						return -1;
					}
					return 0;
				});
				int ct = Math.Min(players.Count, 7);
				int n = 0;
				for (int i = 0; i < ct; i++) {
					var player = players[i];
					if (player.m_bestTime == -1) {
						continue;
					}
					string viewName = "Locals/ItemTime.xml";
					if (m_gamemode == 1) { // Rounds
						viewName = "Locals/ItemPoints.xml";
					}
					xmlItems += GetView("Locals/ItemTime.xml",
						"y", (-3.5 * n).ToString(),
						"place", (n + 1).ToString(),
						"name", Utils.XmlEntities(player.NoLinkNickname),
						"time", Utils.TimeString(player.m_bestTime));
					n++;
				}
			}

			if (login == "") {
				SendView("Live/Widget.xml", "items", xmlItems);
			} else {
				SendViewToLogin(login, "Live/Widget.xml", "items", xmlItems);
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
