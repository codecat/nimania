using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Chat : Plugin
	{
		public override void Initialize()
		{
			m_remote.AddCallback("TrackMania.PlayerChat", (GbxCallback cb) => {
				int id = cb.m_params[0].Get<int>();
				string message = cb.m_params[2].Get<string>();
				bool command = cb.m_params[3].Get<bool>();

				if (m_config.GetBool("Server.Local") && message.StartsWith("/")) {
					command = true;
				}

				if (command) {
					string[] parse = message.Split(' ');
					var player = m_game.GetPlayer(id);
					HandleCommand(player, parse[0], parse.Skip(1).ToArray());
				}
			});
		}

		public override void Uninitialize()
		{
		}

		public void HandleCommand(PlayerInfo player, string command, string[] args)
		{
			switch (command) {
				case "/playtime":
					SendChatTo(player.m_id, "$fffYou have played for: $666" + Utils.TimeStringHMS((int)(DateTime.Now - player.m_joinTime).TotalSeconds));
					break;
			}
		}
	}
}
