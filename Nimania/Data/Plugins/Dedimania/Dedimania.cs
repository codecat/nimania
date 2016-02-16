using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookComputing.XmlRpc;
using GbxRemoteNet;
using Nimania.Runtime;

// These dedimania people wanna know /all/ the things about our server..

namespace Nimania.Plugins
{
	public struct ResponseOpenSession
	{
		public string SessionId;
		public string Error;
	}

	public struct DediSessionInfo
	{
		public string Game;
		public string Login;
		public string Code;
		public string Path; // GetDetailedPlayerInfo(server_login)
		public string Packmask; // GetServerPackMask (ie 'Canyon', 'Stadium', 'Valley')
		public string ServerVersion; // GetVersion
		public string ServerBuild; // GetVersion
		public string Tool;
		public string Version;
		//public string ServerIP; // optional
		//public int ServerPort; // optional
		//public int XmlrpcPort; // optional, but why?
	}

	public struct ResponseChallengeRecord
	{
		public string Login;
		public string NickName;
		public int Best;
		public int Rank;
		public int MaxRank;
		public string Checks; // CP times
		public int Vote;
	}

	public struct ResponseChallengePlayer
	{
		public string Login;
		public string MaxRank;
	}

	public struct ResponseChallengeRecords
	{
		public string UId;
		public int ServerMaxRank;
		public string AllowedGameModes;
		public ResponseChallengeRecord[] Records;
		public ResponseChallengePlayer[] Players;
		public string TotalRaces;
		public string TotalPlayers;
	}

	public struct DediMapInfo // GetCurrentChallengeInfo
	{
		public string UId;
		public string Name;
		public string Environment;
		public string Author;
		public int NbCheckpoints;
		public int NbLaps;
	}

	public struct DediSrvInfo
	{
		public string SrvName;
		public string Comment;
		public bool Private;
		public int NumPlayers;
		public int MaxPlayers;
		public int NumSpecs;
		public int MaxSpecs;
	}

	public struct DediVotesInfo
	{
		public string UId;
		public string GameMode;
	}

	public struct DediPlayer
	{
		public string Login;
		public bool IsSpec;
	}

	public struct DediPlayerUpdate // just SLIGHTLY different than DediPlayer above.. grr.
	{
		public string Login;
		public bool IsSpec;
		public int Vote;
	}

	public struct ResponseSetChallengeTimesRecord
	{
		public string Login;
		public string NickName;
		public int Best;
		public int Rank;
		public int MaxRank;
		public string Checks;
		public bool NewBest;
	}

	public struct ResponseSetChallengeTimes
	{
		public string UId;
		public int ServerMaxRank;
		public string AllowedGameModes;
		public ResponseSetChallengeTimesRecord[] Records;
	}

	public struct DediPlayerTime
	{
		public string Login;
		public int Best;
		public string Checks;
	}

	public struct DediReplay
	{
		public byte[] VReplay;
		public string VReplayChecks;
		public byte[] Top1GReplay;
	}

	[XmlRpcUrl("http://dedimania.net:8082/Dedimania")]
	public interface IDedimaniaAPI : IXmlRpcProxy
	{
		[XmlRpcMethod("dedimania.OpenSession")]
		ResponseOpenSession OpenSession(DediSessionInfo info);

		[XmlRpcMethod("dedimania.CheckSession")]
		bool CheckSession(string session);

		[XmlRpcMethod("dedimania.GetChallengeRecords")]
		ResponseChallengeRecords GetChallengeRecords(string session, DediMapInfo mapInfo, string gameMode, DediSrvInfo srvInfo, DediPlayer[] players);

		[XmlRpcMethod("dedimania.SetChallengeTimes")]
		ResponseSetChallengeTimes SetChallengeTimes(string session, DediMapInfo mapInfo, string gameMode, DediPlayerTime[] times, DediReplay replays);

		[XmlRpcMethod("dedimania.UpdateServerPlayers")]
		bool UpdateServerPlayers(string session, DediSrvInfo srvInfo, DediVotesInfo votesInfo, DediPlayerUpdate[] players);
	}

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
				Console.WriteLine("Starting a new Dedimania session..");

				string serverPath = m_remote.QueryWait("GetDetailedPlayerInfo", m_config["Server.Login"]).m_value.Get<string>("Path");
				string serverPackmask = m_remote.QueryWait("GetServerPackMask").m_value.Get<string>();

				string serverVersion = "";
				string serverBuild = "";

				m_remote.Query("GetVersion", (GbxResponse res) => {
					serverVersion = res.m_value.Get<string>("Version");
					serverBuild = res.m_value.Get<string>("Build");
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
					Console.WriteLine("Dedimania session id: " + m_apiSession);
				} catch {
					Console.WriteLine("Failed to open session with Dedimania!");
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
					Console.WriteLine("Unexpected Dedimania response on UpdateServerPlayers!");
				} else {
					Console.WriteLine("Dedimania heartbeat sent");
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

			m_remote.Query("GetCurrentChallengeInfo", (GbxResponse res) => {
				dmi.UId = res.m_value.Get<string>("UId");
				dmi.Name = res.m_value.Get<string>("Name");
				dmi.Environment = res.m_value.Get<string>("Environnement"); // !!!
				dmi.Author = res.m_value.Get<string>("Author");
				dmi.NbCheckpoints = res.m_value.Get<int>("NbCheckpoints");
				dmi.NbLaps = res.m_value.Get<int>("NbLaps");
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
					string checks = string.Join(",", player.m_bestCheckpoints);
					var time = new DediPlayerTime() {
						Login = player.m_login,
						Best = player.m_bestTime,
						Checks = checks
					};
					times.Add(time);
					if (time.Best != -1 && (!bestTime.HasValue || time.Best < bestTime.Value.Best)) {
						bestTime = time;
					}
				}
			}

			if (!bestTime.HasValue) {
				Console.WriteLine("No best time, not sending anything to Dedimania");
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

			var vReplay = resVReplay.m_value.Get<byte[]>();

			try {
				var resDediSave = m_api.SetChallengeTimes(m_apiSession, dmi, GetGameModeID(), times.ToArray(), new DediReplay() {
					VReplay = vReplay,
					VReplayChecks = bestTime.Value.Checks, // TODO: Make this all checkpoints (in case of laps) or it won't validate!
					Top1GReplay = top1Replay
				});
			} catch {
				SendChat("$f00Dedimania failed to send :(");
			}
			return;
		}

		public void ReloadMapInfo()
		{
			m_dediTimes.Clear();
			SendWidget();

			if (m_apiSession == "") {
				Console.WriteLine("No Dedimania session!!");
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

			ResponseChallengeRecords? dediRes;
			try {
				dediRes = m_api.GetChallengeRecords(m_apiSession, GetDediMapInfo(), GetGameModeID(), GetDediSrvInfo(), plys.ToArray());
			} catch {
				SendChat("$f00Dedimania is down :(");
				return;
			}
			m_lastUpdate = DateTime.Now;
			m_maxDedi = dediRes.Value.ServerMaxRank;
			SendChat("$f00" + dediRes.Value.Records.Length + "$fff dedimania times on this map (max top " + m_maxDedi + ")");

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
						"name", Utils.XmlEntities(time.NickName),
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
