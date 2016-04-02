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
	public class CheckpointCompare : Plugin
	{
		public override void Initialize()
		{
		}

		public override void OnPlayerBegin(PlayerInfo player)
		{
			SendWidget(player, 0, -1);
		}

		public override void OnPlayerCheckpoint(PlayerInfo player, int n, int time)
		{
			n %= m_game.m_currentMap.m_nCheckpoints;
			SendWidget(player, n, time);
		}

		void SendWidget(PlayerInfo player, int n, int cpTime)
		{
			bool compareLocal = cpTime != -1 && m_game.m_userData.HasCategory("Locals");
			bool compareDedi = cpTime != -1 && m_game.m_userData.HasCategory("Dedimania");

			string compareLocalPB = "--";
			string compareLocal1st = "--";
			string compareDediPB = "--";
			string compareDedi1st = "--";

			if (compareLocal) {
				var pb = player.m_userData.Get<int[]>("Locals.PB-CP");
				if (pb != null && n < pb.Length) {
					int diff = cpTime - pb[n];
					if (diff <= 0) {
						compareLocalPB = "$22f" + Utils.TimeString(diff);
					} else {
						compareLocalPB = "$f22+" + Utils.TimeString(diff);
					}
				}

				var best = m_game.m_userData.Get<int[]>("Locals.1st-CP");
				if (best != null && n < best.Length) {
					int diff = cpTime - best[n];
					if (diff <= 0) {
						compareLocal1st = "$22f" + Utils.TimeString(diff);
					} else {
						compareLocal1st = "$f22+" + Utils.TimeString(diff);
					}
				}
			}

			if (compareDedi) {
				var pb = player.m_userData.Get<int[]>("Dedimania.PB-CP");
				if (pb != null && n < pb.Length) {
					int diff = cpTime - pb[n];
					if (diff <= 0) {
						compareDediPB = "$22f" + Utils.TimeString(diff);
					} else {
						compareDediPB = "$f22+" + Utils.TimeString(diff);
					}
				}

				var best = m_game.m_userData.Get<int[]>("Dedimania.1st-CP");
				if (best != null && n < best.Length) {
					int diff = cpTime - best[n];
					if (diff <= 0) {
						compareDedi1st = "$22f" + Utils.TimeString(diff);
					} else {
						compareDedi1st = "$f22+" + Utils.TimeString(diff);
					}
				}
			}

			SendViewToLogin(player.m_login, "CheckpointCompare/BestCompare.xml",
				"LOCAL", compareLocal,
				"DEDI", compareDedi,
				"n", n,
				"localPB", compareLocalPB,
				"local1st", compareLocal1st,
				"dediPB", compareDediPB,
				"dedi1st", compareDedi1st);
		}
	}
}
