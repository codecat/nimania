using System;
using System.Collections.Generic;
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

	public struct DediPlayer
	{
		public string Login;
		public bool IsSpec;
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
	}

	public class Dedimania : Plugin
	{
		public class DediTime
		{
			string Login;
			string NickName;
			int Time;
		}

		public List<DediTime> m_dediTimes = new List<DediTime>();

		public IDedimaniaAPI m_api;
		public string m_apiSession = "";

		public override void Initialize()
		{
			string serverPath = m_remote.QueryWait("GetDetailedPlayerInfo", m_config["Server.Login"]).m_value.Get<string>("Path");
			string serverPackmask = m_remote.QueryWait("GetServerPackMask").m_value.Get<string>();

			string serverVersion = "";
			string serverBuild = "";

			m_remote.Query("GetVersion", (GbxResponse res) => {
				serverVersion = res.m_value.Get<string>("Version");
				serverBuild = res.m_value.Get<string>("Build");
			}).Wait();

			m_api = XmlRpcProxyGen.Create<IDedimaniaAPI>();
			m_api.NonStandard = XmlRpcNonStandard.AllowStringFaultCode;
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
			} catch {
				Console.WriteLine("Failed to open session with Dedimania!");
			}

			ReloadMapInfo();
		}

		public override void Uninitialize()
		{
		}

		public override void OnBeginChallenge()
		{
			ReloadMapInfo();
		}

		public void ReloadMapInfo()
		{
			if (m_apiSession == "") {
				Console.WriteLine("No Dedimania session!!");
				return;
			}

			var dmi = new DediMapInfo();

			m_remote.Query("GetCurrentChallengeInfo", (GbxResponse res) => {
				dmi.UId = res.m_value.Get<string>("UId");
				dmi.Name = res.m_value.Get<string>("Name");
				dmi.Environment = res.m_value.Get<string>("Environnement"); // !!!
				dmi.Author = res.m_value.Get<string>("Author");
				dmi.NbCheckpoints = res.m_value.Get<int>("NbCheckpoints");
				dmi.NbLaps = res.m_value.Get<int>("NbLaps");
			}).Wait();

			var dsi = new DediSrvInfo();
			dsi.SrvName = m_game.m_serverName;
			dsi.Comment = m_game.m_serverComment;
			dsi.Private = m_game.m_serverPrivate;
			dsi.NumPlayers = m_game.GetPlayerCount();
			dsi.MaxPlayers = m_game.m_serverMaxPlayers;
			dsi.NumSpecs = m_game.GetSpectatorCount();
			dsi.MaxSpecs = m_game.m_serverMaxSpecs;

			var plys = new List<DediPlayer>();
			lock (m_game.m_players) {
				foreach (var player in m_game.m_players) {
					plys.Add(new DediPlayer() {
						Login = player.m_login,
						IsSpec = player.m_spectating
					});
				}
			}

			var dediRes = m_api.GetChallengeRecords(m_apiSession, dmi, m_game.m_serverGameMode == 1 ? "Rounds" : "TA", dsi, plys.ToArray());
			SendChat("$f00" + dediRes.Records.Length + "$fff dedimania times on this map");
		}
	}
}
