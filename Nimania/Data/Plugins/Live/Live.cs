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
		const int MAX_SHOW_PLAYERS = 7; //TODO: This sucks.

		public int m_scoreLimit;

		public override bool Supports(GameType type, string script)
		{
			if (type == GameType.TrackMania) {
				return true;
			}

			return false;
		}

		public override void Initialize()
		{
			ReloadMapInfo();
		}

		public override void OnBeginChallenge()
		{
			ReloadMapInfo();
		}

		public override void OnEndRound()
		{
			SendWidget();
		}

		public void ReloadMapInfo()
		{
			m_remote.Query("GetRoundPointsLimit", (GbxValue res) => {
				m_scoreLimit = res.Get<int>("CurrentValue");
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
				int n = 0;
				for (int i = 0; i < players.Count; i++) {
					var player = players[i];

					// Filter players without times in TA and laps
					if ((m_game.m_serverGameMode == 2 || m_game.m_serverGameMode == 4) && player.m_bestTime == -1) {
						continue;
					}

					// Filter players without points in rounds
					if (m_game.m_serverGameMode == 1 && player.m_score == 0) {
						continue;
					}

					string viewName = "Live/ItemTime.xml";
					if (m_game.m_serverGameMode == 1) { // Rounds
						viewName = "Live/ItemPoints.xml";
					}

					xmlItems += GetResource(viewName,
						"y", (-3.5 * n),
						"place", (n + 1),
						"name", Utils.XmlEntities(player.NoLinkNickname),
						"login", Utils.XmlEntities(player.m_login),
						"time", Utils.TimeString(player.m_bestTime),
						"score", player.m_score,
						"scoreleft", (m_scoreLimit - player.m_score));

					if (++n >= MAX_SHOW_PLAYERS) {
						break;
					}
				}
			}

			var arrowLocal = GetResource("ListArrows/ArrowLocal.xml",
				"x", -45);

			if (login == "") {
				SendView("Live/Widget.xml",
					"items", xmlItems,
					"arrowLocal", arrowLocal);
			} else {
				SendViewToLogin(login, "Live/Widget.xml",
					"items", xmlItems,
					"arrowLocal", arrowLocal);
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
