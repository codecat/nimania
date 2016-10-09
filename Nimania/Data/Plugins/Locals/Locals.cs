using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using Nimania.Runtime.DbModels;

namespace Nimania.Plugins
{
	public class Locals : Plugin
	{
		public List<LocalTime> m_localTimes = new List<LocalTime>();

		public override void Initialize()
		{
			m_game.m_userData.AddCategory("Locals");

			ReloadMapInfo();
		}

		private void SortTimes()
		{
			lock (m_localTimes) {
				m_localTimes.Sort((a, b) => {
					if (a.Time < b.Time)
						return -1;
					else if (a.Time > b.Time)
						return 1;
					return 0;
				});
			}
		}

		public override void OnBeginChallenge()
		{
			ReloadMapInfo();
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			SendWidget(player.m_login);
		}

		public override void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints)
		{
			bool updated = false;

			lock (m_localTimes) {
				bool hadTime = false;

				for (int i = 0; i < m_localTimes.Count; i++) {
					var localTime = m_localTimes[i];
					if (localTime.Player.ID == player.m_localPlayer.ID) {
						hadTime = true;
						if (time == localTime.Time) {
							SendChat(string.Format(m_config["Messages.Locals.TimeEqualed"], player.m_nickname, i + 1, Utils.TimeString(time)));
						} else if (time < localTime.Time) {
							int diff = localTime.Time - time;
							localTime.Time = time;
							localTime.Checkpoints = string.Join(",", checkpoints);
							localTime.Save();
							SortTimes(); //TODO: Get rid of this and move the element around ourselves
							int n = m_localTimes.IndexOf(localTime);
							if (n != i) {
								SendChat(string.Format(m_config["Messages.Locals.TimeImprovedGained"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
							} else {
								SendChat(string.Format(m_config["Messages.Locals.TimeImproved"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
							}

							player.m_userData.Set("Locals.PB", time);
							player.m_userData.Set("Locals.PB-CP", checkpoints);
							if (n == 0) {
								m_game.m_userData.Set("Locals.1st", time);
								m_game.m_userData.Set("Locals.1st-CP", checkpoints);
							}

							updated = true;
						}
						break;
					}
				}

				if (!hadTime) {
					int maxCount = m_config.GetInt("Plugin_Locals.MaxTimes");
					int insertBefore = -1;
					int count = Math.Min(m_localTimes.Count, maxCount);
					for (int i = 0; i < count; i++) {
						if (time < m_localTimes[i].Time) {
							insertBefore = i;
							break;
						}
					}

					if (insertBefore == -1 && count < maxCount) {
						insertBefore = m_localTimes.Count;
					}

					if (insertBefore != -1) {
						var newTime = m_database.Create<LocalTime>();
						newTime.Map = m_game.m_currentMap;
						newTime.Player = player.m_localPlayer;
						newTime.Time = time;
						newTime.Checkpoints = string.Join(",", checkpoints);
						newTime.Save();
						m_localTimes.Insert(insertBefore, newTime);
						SendChat(string.Format(m_config["Messages.Locals.TimeGained"], player.m_nickname, insertBefore + 1, Utils.TimeString(time)));

						if (m_localTimes.Count > maxCount) {
							m_localTimes.RemoveRange(maxCount, m_localTimes.Count - maxCount);
						}

						player.m_userData.Set("Locals.PB", time);
						player.m_userData.Set("Locals.PB-CP", checkpoints);
						if (insertBefore == 0) {
							m_game.m_userData.Set("Locals.1st", time);
							m_game.m_userData.Set("Locals.1st-CP", checkpoints);
						}

						updated = true;
					}
				}
			}

			if (updated) {
				SendWidget();
			}
		}

		public void ReloadMapInfo()
		{
			lock (m_localTimes) {
				m_localTimes.Clear();
				m_localTimes.AddRange(m_database.FindAllByAttributes<LocalTime>(new dynamic[] { "Map", m_game.m_currentMap.ID }, new DbQueryOptions() {
					m_range = true,
					m_rangeTo = m_config.GetInt("Plugin_Locals.MaxTimes"),

					m_sort = true,
					m_sortKey = "Time"
				}));
				foreach (var time in m_localTimes) {
					var player = m_game.GetPlayer(time.Player.Login);
					if (player != null) {
						player.m_userData.Set("Locals.PB", time.Time);
						player.m_userData.Set("Locals.PB-CP", Utils.ChecksToInt(time.Checkpoints));
					}
				}
				SendChat("$f00" + m_localTimes.Count + "$fff local times on this map");
			}

			if (m_localTimes.Count > 0) {
				m_game.m_userData.Set("Locals.1st", m_localTimes[0].Time);
				m_game.m_userData.Set("Locals.1st-CP", Utils.ChecksToInt(m_localTimes[0].Checkpoints));
			}

			SendWidget();
		}

		public void SendWidget(string login = "")
		{
			// sadly, we are forced to send the entire thing every time it updates. :(
			string xmlItems = "";
			string xmlArrows = "";
			lock (m_localTimes) {
				int ct = Math.Min(m_localTimes.Count, 25);
				for (int i = 0; i < ct; i++) {
					var time = m_localTimes[i];
					xmlItems += GetResource("Locals/Item.xml",
						"y", (-3.5 * i),
						"place", (i + 1),
						"name", Utils.XmlEntities(time.Player.NoLinkNickname),
						"login", Utils.XmlEntities(time.Player.Login),
						"time", Utils.TimeString(time.Time));

					var player = m_game.GetPlayer(time.Player.Login);
					if (player != null && player.m_connected) {
						xmlArrows += GetResource("ListArrows/ArrowPlayer.xml",
							"x", -45,
							"y", (-4.0 - i * 3.5),
							"login", Utils.XmlEntities(player.m_login));
					}
				}
			}

			var arrowLocal = GetResource("ListArrows/ArrowLocal.xml",
				"x", -45);

			if (login == "") {
				SendView("Locals/Widget.xml",
					"items", xmlItems,
					"arrowLocal", arrowLocal,
					"arrows", xmlArrows);
			} else {
				SendViewToLogin(login, "Locals/Widget.xml",
					"items", xmlItems,
					"arrowLocal", arrowLocal,
					"arrows", xmlArrows);
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
