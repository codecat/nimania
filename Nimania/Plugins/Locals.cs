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

		public override void Uninitialize()
		{
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
							localTime.Save();
							SortTimes(); //TODO: Get rid of this and move the element around ourselves
							int n = m_localTimes.IndexOf(localTime);
							if (n != i) {
								SendChat(string.Format(m_config["Messages.Locals.TimeImprovedGained"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
							} else {
								SendChat(string.Format(m_config["Messages.Locals.TimeImproved"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
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
						newTime.Checkpoints = "";
						newTime.Save();
						m_localTimes.Insert(insertBefore, newTime);
						SendChat(string.Format(m_config["Messages.Locals.TimeGained"], player.m_nickname, insertBefore + 1, Utils.TimeString(time)));

						if (m_localTimes.Count > maxCount) {
							m_localTimes.RemoveRange(maxCount, m_localTimes.Count - maxCount);
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
				SendChat("$f00" + m_localTimes.Count + "$fff local times on this map");
			}

			SendWidget();
		}

		public void SendWidget(string login = "")
		{
			// sadly, we are forced to send the entire thing every time it updates. :(
			string xmlItems = "";
			lock (m_localTimes) {
				int ct = Math.Min(m_localTimes.Count, 25);
				for (int i = 0; i < ct; i++) {
					var time = m_localTimes[i];
					xmlItems += GetView("Locals/Item.xml",
						"y", (-3.5 * i).ToString(),
						"place", (i + 1).ToString(),
						"name", Utils.XmlEntities(time.Player.Nickname),
						"time", Utils.TimeString(time.Time));
				}
			}

			if (login == "") {
				SendView("Locals/Widget.xml", "items", xmlItems);
			} else {
				SendViewToLogin(login, "Locals/Widget.xml", "items", xmlItems);
			}
		}
	}
}
