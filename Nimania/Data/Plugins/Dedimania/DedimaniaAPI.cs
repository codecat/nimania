using System;
using CookComputing.XmlRpc;

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
		public string Path; // GetDetailedPlayerInfo
		public string Packmask; // GetServerPackMask
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
}
