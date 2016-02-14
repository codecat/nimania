using System;
using System.Collections.Generic;
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

		public override void Initialize()
		{
			m_remote.AddCallback("TrackMania.PlayerCheckpoint", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
				int time = cb.m_params[2].Get<int>();
				int n = cb.m_params[4].Get<int>();

				var player = m_game.GetPlayer(id);
				if (player == null) {
					// ???
					return;
				}

				if (n + 1 > m_cps.Count) {
					if (m_cps.Count == n) {
						m_cps.Add(new BestCp() {
							m_player = player,
							m_time = time
						});
					}
				} else {
					var cp = m_cps[n];
					if (time < cp.m_time) {
						cp.m_player = player;
						cp.m_time = time;
					}
				}

				SendWidget();
			});

			SendWidget();
		}

		public override void Uninitialize()
		{
		}

		public override void OnBeginChallenge()
		{
			m_cps.Clear();
			SendWidget();
		}

		public override void OnPlayerConnect(string login)
		{
			SendWidget(login);
		}

		void SendWidget(string login = "")
		{
			string xmlCps = "";
			for (int i = 0; i < m_cps.Count; i++) {
				var cp = m_cps[i];
				xmlCps += GetView("Checkpoints/Cp.xml",
					"x", (i * 31).ToString(),
					"y", "0",
					"n", (i + 1).ToString(),
					"time", Utils.TimeString(cp.m_time),
					"name", Utils.XmlEntities(cp.m_player.m_nickname));
			}

			if (login == "") {
				SendView("Checkpoints/Widget.xml", "cps", xmlCps);
			} else {
				SendViewToLogin(login, "Checkpoints/Widget.xml", "cps", xmlCps);
			}
		}
	}
}
