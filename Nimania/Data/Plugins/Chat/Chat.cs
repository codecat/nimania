using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using NLog;

namespace Nimania.Plugins
{
	public class Chat : Plugin
	{
		public override void Initialize()
		{
			m_remote.AddCallback("TrackMania.PlayerChat", (GbxValue[] cb) => {
				int id = cb[0].Get<int>();
				string message = cb[2].Get<string>();
				bool command = cb[3].Get<bool>();

				if (m_config.GetBool("Server.Local") && message.StartsWith("/")) {
					command = true;
				}

				var player = m_game.GetPlayer(id);
				if (command) {
					m_logger.Info("Chat command: {0}: {1}", player.m_login, message);
					string[] parse = message.Split(' ');
					HandleCommand(player, parse[0], parse.Skip(1).ToArray());
				} else {
					m_logger.Info("Chat: {0}: {1}", player.m_login, message);
				}
			});
		}

		public void HandleCommand(PlayerInfo player, string command, string[] args)
		{
			switch (command) {
				case "/playtime":
					SendChatTo(player.m_id, "$fffYou have played for: $666" + Utils.TimeStringHMS((int)(DateTime.Now - player.m_joinTime).TotalSeconds));
					break;

				case "/ragequit":
				case "/fuckthis":
					m_remote.Execute("SendDisplayManialinkPageToLogin", player.m_login, "<manialink><quad image=\":\"/></manialink>", 0, false);
					break;

				case "/admin":
					if (!player.IsAdmin) {
						SendChatTo(player.m_id, "$fffYou are not an admin.");
						break;
					}
					if (args.Length == 0) {
						SendChatTo(player.m_id, "$fffUse like this: $666/admin <subcommand> [args]");
						break;
					}
					HandleAdminCommand(player, args[0], args.Skip(1).ToArray());
					break;

				case "/players":
					string xmlItems = "";
					lock (m_game.m_players) {
						for (int i = 0; i < m_game.m_players.Count; i++) {
							var ply = m_game.m_players[i];

							string login = "";
							if (ply.IsDeveloper) {
								login = "$a77";
							} else if (ply.IsAdmin) {
								login = "$7a7";
							} else {
								login = "$77a";
							}
							login += "(" + ply.m_login + ")";

							xmlItems += GetView("Chat/PlayersItem.xml",
								"y", (-3.5 * i).ToString(),
								"name", Utils.XmlEntities(ply.m_nickname),
								"login", Utils.XmlEntities(login));
						}
					}
					SendViewToLogin(player.m_login, "Chat/Players.xml", 0, true, "items", xmlItems);
					break;
			}
		}

		public void HandleAdminCommand(PlayerInfo player, string subCommand, string[] args)
		{
			if (!player.IsAdmin) {
				m_logger.Warn("Don't call HandleAdminCommand if player is not admin!");
				return;
			}
			switch (subCommand) {
				case "setgamemode":
					if (args.Length != 1) {
						SendChatTo(player.m_id, "$fffUse like this: $666/admin setgamemode <Script|Rounds|TA|Team|Laps|Cup|Stunts>");
						break;
					}
					int setTo = -1;
					string setToName = "";
					switch (args[0].ToLower()) {
						case "script": setTo = 0; setToName = "Script"; break;
						case "rounds": setTo = 1; setToName = "Rounds"; break;
						case "ta": setTo = 2; setToName = "Time Attack"; break;
						case "team": setTo = 3; setToName = "Team"; break;
						case "laps": setTo = 4; setToName = "Laps"; break;
						case "cup": setTo = 5; setToName = "Cup"; break;
						case "stunts": setTo = 6; setToName = ""; break;
					}
					if (setTo == -1) {
						SendChatTo(player.m_id, "$fffUnknown gamemode name.");
					} else {
						m_remote.Execute("SetGameMode", setTo);
						SendAdminSet(player, "gamemode", setToName);
					}
					break;

				case "rpoints":
					if (args.Length != 1) {
						SendChatTo(player.m_id, "$fffUse like this: $666/admin rpoints <Normal|MotoGP>");
						break;
					}
					int[] pointSystem = new int[0];
					bool foundSystem = false;
					switch (args[0].ToLower()) {
						case "normal":
							foundSystem = true;
							// 10, 6, 4, 3, 2, 1
							break;

						case "motogp":
							pointSystem = new int[] { 25, 20, 16, 13, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
							foundSystem = true;
							break;
					}
					m_remote.Execute("SetRoundCustomPoints", pointSystem, true);
					string points = "";
					for (int i = 0; i < pointSystem.Length; i++) {
						if (i > 0) {
							points += ",";
						}
						points += pointSystem[i];
					}
					SendAdminSet(player, "round points", points == "" ? "normal" : points);
					break;
			}
		}

		public void SendAdminSet(PlayerInfo admin, string thing, string what)
		{
			SendChat(string.Format(m_config["Messages.Admin.Set"], admin.m_localPlayer.Group.Name, admin.m_nickname, thing, what));
		}
	}
}
