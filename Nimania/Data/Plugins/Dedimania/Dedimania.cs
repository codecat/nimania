using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookComputing.XmlRpc;
using GbxRemoteNet;
using Nimania.Runtime;
using NLog;

// These dedimania people wanna know /all/ the things about our server..

namespace Nimania.Plugins
{
	public class DediTime
	{
		public string Login;
		public string NickName;
		public int Time;

		public bool Updated = false;
	}

	public class Dedimania : Plugin
	{
		public List<DediTime> m_dediTimes = new List<DediTime>();
		public int m_maxDedi = 0;
		public int m_currentTop1 = -1;

		public IDedimaniaAPI m_api;
		public string m_apiSession = "";

		public DateTime m_lastUpdate = DateTime.Now;

		public override void Initialize()
		{
			m_api = XmlRpcProxyGen.Create<IDedimaniaAPI>();
			m_api.NonStandard = XmlRpcNonStandard.AllowStringFaultCode;

			StartSession();
		}

		private void SortTimes()
		{
			lock (m_dediTimes) {
				m_dediTimes.Sort((a, b) => {
					if (a.Time < b.Time)
						return -1;
					else if (a.Time > b.Time)
						return 1;
					return 0;
				});
			}
		}

		public void StartSession()
		{
			Task.Factory.StartNew(() => {
				m_logger.Debug("Starting a new Dedimania session..");

				string serverPath = m_remote.QueryWait("GetDetailedPlayerInfo", m_config["Server.Login"]).Get<string>("Path");
				string serverPackmask = m_remote.QueryWait("GetServerPackMask").Get<string>();

				string serverVersion = "";
				string serverBuild = "";

				m_remote.Query("GetVersion", (GbxValue res) => {
					serverVersion = res.Get<string>("Version");
					serverBuild = res.Get<string>("Build");
				}).Wait();

				try {
					var resDedi = m_api.OpenSession(new DediSessionInfo() {
						Game = "TM2",
						Login = m_config["Server.Login"],
						Code = m_config["Plugin_Dedimania.Code"],
						Path = serverPath,
						Packmask = serverPackmask,
						ServerVersion = serverVersion,
						ServerBuild = serverBuild,
						Tool = "Nimania",
						Version = "1.000"
					});
					m_apiSession = resDedi.SessionId;
					m_logger.Debug("Dedimania session id: " + m_apiSession);
				} catch {
					m_logger.Error("Failed to open session with Dedimania!");
				}

				ReloadMapInfo();
			});
		}

		public override void Uninitialize()
		{
			SendDediTimes();
		}

		public override void EverySecond()
		{
			var diff = DateTime.Now - m_lastUpdate;
			if (diff.TotalMinutes >= 4.0) {
				m_lastUpdate = DateTime.Now;
				var dsi = GetDediSrvInfo();
				var dpu = new List<DediPlayerUpdate>();
				lock (m_game.m_players) {
					foreach (var player in m_game.m_players) {
						dpu.Add(new DediPlayerUpdate() {
							Login = player.m_login,
							IsSpec = player.m_spectating,
							Vote = -1
						});
					}
				}
				bool ok = m_api.UpdateServerPlayers(m_apiSession, dsi, new DediVotesInfo() {
					GameMode = GetGameModeID(),
					UId = m_game.m_currentMap.UId
				}, dpu.ToArray());
				if (!ok) {
					m_logger.Error("Unexpected Dedimania response on UpdateServerPlayers!");
				} else {
					m_logger.Debug("Dedimania heartbeat sent");
				}
			}
		}

		public override void OnBeginChallenge()
		{
			ReloadMapInfo();
		}

		public override void OnEndChallenge()
		{
			SendDediTimes();
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			SendWidget();
		}

		public override void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints)
		{
			bool updated = false;

			lock (m_dediTimes) {
				bool hadTime = false;

				//TODO: m_maxDedi is not consistent - players who donated can have 60 or 100
				int ct = Math.Min(m_dediTimes.Count, m_maxDedi);
				for (int i = 0; i < ct; i++) {
					var dediTime = m_dediTimes[i];
					if (dediTime.Login == player.m_login) {
						hadTime = true;
						if (dediTime.Time == time) {
							SendChat(string.Format(m_config["Messages.Dedimania.TimeEqualed"], player.m_nickname, i + 1, Utils.TimeString(time)));
						} else if (time < dediTime.Time) {
							int diff = dediTime.Time - time;
							dediTime.Time = time;
							SortTimes(); //TODO: Get rid of this and move the element around ourselves
							int n = m_dediTimes.IndexOf(dediTime);
							if (n != i) {
								SendChat(string.Format(m_config["Messages.Dedimania.TimeImprovedGained"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
							} else {
								SendChat(string.Format(m_config["Messages.Dedimania.TimeImproved"], player.m_nickname, n + 1, Utils.TimeString(time), Utils.TimeString(diff)));
							}
							updated = true;
						}
						break;
					}
				}

				if (!hadTime) {
					int insertBefore = -1;
					int count = Math.Min(m_dediTimes.Count, m_maxDedi);
					for (int i = 0; i < count; i++) {
						if (time < m_dediTimes[i].Time) {
							insertBefore = i;
							break;
						}
					}

					if (insertBefore == -1 && count < m_maxDedi) {
						insertBefore = m_dediTimes.Count;
					}

					if (insertBefore != -1) {
						var newTime = new DediTime() {
							Login = player.m_login,
							NickName = player.m_nickname,
							Time = time
						};
						m_dediTimes.Insert(insertBefore, newTime);
						SendChat(string.Format(m_config["Messages.Dedimania.TimeGained"], player.m_nickname, insertBefore + 1, Utils.TimeString(time)));

						updated = true;
					}
				}
			}

			if (updated) {
				SendWidget();
			}
		}

		public DediMapInfo GetDediMapInfo()
		{
			var dmi = new DediMapInfo();

			m_remote.Query("GetCurrentChallengeInfo", (GbxValue res) => {
				dmi.UId = res.Get<string>("UId");
				dmi.Name = res.Get<string>("Name");
				dmi.Environment = res.Get<string>("Environnement"); // !!!
				dmi.Author = res.Get<string>("Author");
				dmi.NbCheckpoints = res.Get<int>("NbCheckpoints");
				dmi.NbLaps = res.Get<int>("NbLaps");
			}).Wait();

			return dmi;
		}

		public DediSrvInfo GetDediSrvInfo()
		{
			var dsi = new DediSrvInfo();
			dsi.SrvName = m_game.m_serverName;
			dsi.Comment = m_game.m_serverComment;
			dsi.Private = m_game.m_serverPrivate;
			dsi.NumPlayers = m_game.GetPlayerCount();
			dsi.MaxPlayers = m_game.m_serverMaxPlayers;
			dsi.NumSpecs = m_game.GetSpectatorCount();
			dsi.MaxSpecs = m_game.m_serverMaxSpecs;
			return dsi;
		}

		public string GetGameModeID()
		{
			return m_game.m_serverGameMode == 1 ? "Rounds" : "TA";
		}

		public void SendDediTimes()
		{
			DediPlayerTime? bestTime = null;
			var times = new List<DediPlayerTime>();
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					if (player.m_bestTime == -1) {
						continue;
					}
					string checks = string.Join(",", player.m_bestCheckpointsLap);
					var time = new DediPlayerTime() {
						Login = player.m_login,
						Best = player.m_bestTime,
						Checks = checks
					};
					m_logger.Debug("Adding time: " + time.Login + ", " + time.Best + " (" + time.Checks + ")");
					times.Add(time);
					if (time.Best != -1 && (!bestTime.HasValue || time.Best < bestTime.Value.Best)) {
						bestTime = time;
					}
				}
			}

			if (!bestTime.HasValue) {
				m_logger.Info("No best time, not sending anything to Dedimania");
				return;
			}

			times.Sort((a, b) => {
				if (a.Best > b.Best) {
					return 1;
				} else if (a.Best < b.Best) {
					return -1;
				}
				return 0;
			});

			var dmi = GetDediMapInfo();

			var resVReplay = m_remote.QueryWait("GetValidationReplay", bestTime.Value.Login);

			var top1Replay = new byte[0];
			if (bestTime.Value.Best < m_currentTop1 || m_currentTop1 == -1) {
				string filename = "NimaniaReplays/" + dmi.UId + "_" + bestTime.Value.Login + "_" + bestTime.Value.Best;
				string path = m_config["Plugin_Dedimania.ReplaysPath"].Replace('\\', '/');
				if (!path.EndsWith("/")) {
					path += "/";
				}
				path += filename + ".Replay.Gbx";
				m_remote.QueryWait("SaveBestGhostsReplay", bestTime.Value.Login, filename);
				top1Replay = File.ReadAllBytes(path);
			}

			var vReplay = resVReplay.Get<byte[]>();

			bool sentOk = false;
			for (int i = 0; i < 3; i++) {
				try {
					var vChecks = "";
					if (m_game.m_serverGameMode == 4) {
						vChecks = string.Join(",", m_game.GetPlayer(bestTime.Value.Login).m_checkpointsAll);
					} else {
						vChecks = string.Join(",", m_game.GetPlayer(bestTime.Value.Login).m_bestCheckpoints);
					}
					/*
					var resDediSave = m_api.SetChallengeTimes(m_apiSession, dmi, GetGameModeID(), times.ToArray(), new DediReplay() {
						VReplay = vReplay,
						VReplayChecks = vChecks,
						Top1GReplay = top1Replay
					});
					*/
					m_api.MultiCall(new[] {
						new MultiCallEntry() {
							methodName = "dedimania.SetChallengeTimes",
							parameters = new object[] { m_apiSession, dmi, GetGameModeID(), times.ToArray(), new DediReplay() {
								VReplay = vReplay,
								VReplayChecks = vChecks,
								Top1GReplay = top1Replay
							} }
						},
						new MultiCallEntry() {
							methodName = "dedimania.WarningsAndTTR2",
							parameters = new object[0]
						}
					});
					m_logger.Info("Sent multicall to dedis");
					sentOk = true;
					break;
				} catch (Exception ex) {
					m_logger.Error(ex.Message);
					continue;
				}
			}
			if (!sentOk) {
				SendChat("$f00Failed $fffsending Dedmania records after 3 attempts!");
			}
			return;
		}

		public void ReloadMapInfo()
		{
			m_dediTimes.Clear();
			SendWidget();

			if (m_apiSession == "") {
				m_logger.Warn("No Dedimania session!");
				StartSession();
				return;
			}

			var plys = new List<DediPlayer>();
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					plys.Add(new DediPlayer() {
						Login = player.m_login,
						IsSpec = player.m_spectating
					});
				}
			}

			ResponseChallengeRecords? dediRes = null;
			for (int i = 0; i < 3; i++) {
				try {
					dediRes = m_api.GetChallengeRecords(m_apiSession, GetDediMapInfo(), GetGameModeID(), GetDediSrvInfo(), plys.ToArray());
					break;
				} catch { continue; }
			}

			if (!dediRes.HasValue) {
				SendChat("$f00Failed $fffretrieving records from Dedimania after 3 attempts!");
				return;
			}

			m_lastUpdate = DateTime.Now;
			m_maxDedi = dediRes.Value.ServerMaxRank;
			SendChat("$f00" + dediRes.Value.Records.Length + "$fff dedimania times on this map");

			foreach (var dedi in dediRes.Value.Records) {
				m_dediTimes.Add(new DediTime() {
					Login = dedi.Login,
					NickName = dedi.NickName,
					Time = dedi.Best
				});
			}

			if (dediRes.Value.Records.Length > 0) {
				m_currentTop1 = dediRes.Value.Records[0].Best;
			} else {
				m_currentTop1 = -1;
			}

			SendWidget();
		}

		public void SendWidget(string login = "")
		{
			// sadly, we are forced to send the entire thing every time it updates. :(
			var xmlItems = "";
			lock (m_dediTimes) {
				int ct = Math.Min(m_dediTimes.Count, 25);
				for (int i = 0; i < ct; i++) {
					var time = m_dediTimes[i];
					xmlItems += GetView("Dedimania/Item.xml",
						"y", (-3.5 * i).ToString(),
						"place", (i + 1).ToString(),
						"name", Utils.XmlEntities(Utils.StripLinkCodes(time.NickName)),
						"time", Utils.TimeString(time.Time));
				}
			}

			if (login == "") {
				SendView("Dedimania/Widget.xml", "items", xmlItems);
			} else {
				SendViewToLogin(login, "Dedimania/Widget.xml", "items", xmlItems);
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
