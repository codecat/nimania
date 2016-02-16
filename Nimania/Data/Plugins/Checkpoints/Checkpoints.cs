using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class BestCp
	{
		public PlayerInfo m_player;
		public int m_time;
	}

	public class Checkpoints : Plugin
	{
		public List<BestCp> m_cps = new List<BestCp>();
		public int m_cpCount = -1;

		public override void Initialize()
		{
			OnBeginChallenge();
		}

		public override void Uninitialize()
		{
		}

		public override void OnBeginChallenge()
		{
			m_cpCount = m_game.m_currentMap.m_nCheckpoints;
			m_cps.Clear();
			SendWidget();
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			SendWidget(player.m_login);
		}

		public override void OnPlayerCheckpoint(PlayerInfo player, int n, int time)
		{
			bool updated = false;

			if (n + 1 > m_cps.Count) {
				if (m_cps.Count == n && n != m_cpCount - 1) {
					m_cps.Add(new BestCp() {
						m_player = player,
						m_time = time
					});
					updated = true;
				}
			} else {
				var cp = m_cps[n];
				if (time < cp.m_time) {
					cp.m_player = player;
					cp.m_time = time;
					updated = true;
				}
			}

			if (updated) {
				SendWidget();
			}
		}

		void SendWidget(string login = "")
		{
			string xmlCps = "";
			for (int i = 0; i < m_cps.Count; i++) {
				var cp = m_cps[i];
				int x = (i % 7) * 31;
				int y = (i / 7) * -7;
				xmlCps += GetView("Checkpoints/Cp.xml",
					"x", x.ToString(),
					"y", y.ToString(),
					"n", (i + 1).ToString(),
					"time", Utils.TimeString(cp.m_time),
					"name", Utils.XmlEntities(cp.m_player.NoLinkNickname));
			}

			if (login == "") {
				SendView("Checkpoints/Widget.xml", "cps", xmlCps);
			} else {
				SendViewToLogin(login, "Checkpoints/Widget.xml", "cps", xmlCps);
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
