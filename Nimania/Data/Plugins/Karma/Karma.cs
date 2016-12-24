using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;
using Nimania.Runtime.DbModels;

namespace Nimania.Plugins
{
	public class Karma : Plugin
	{
		public override void Initialize()
		{
			m_remote.AddCallback("TrackMania.PlayerChat", (GbxValue[] cb) => {
				string message = cb[2].Get<string>();

				int voteValue = 0;
				if (message == "++") {
					voteValue = 1;
				} else if (message == "--") {
					voteValue = -1;
				}

				if (voteValue == 0) {
					return;
				}

				int id = cb[0].Get<int>();
				var player = m_game.GetPlayer(id);

				var vote = m_database.FindByAttributes<MapVote>(
					"Login", player.m_login,
					"Map", m_game.m_currentMap.ID);

				lock (m_game.m_currentMap) {
					int newValue = m_game.m_currentMap.Karma;

					if (vote == null) {
						vote = m_database.Create<MapVote>();
						vote.Login = player.m_login;
						vote.Map = m_game.m_currentMap;
						vote.Value = voteValue;
						vote.Save();

						newValue += voteValue;

						m_game.m_currentMap.KarmaVotes++;
					} else {
						if (vote.Value == voteValue) {
							SendChatTo(id, "$f00You have already voted for this map.");
							return;
						}
						// Undo the previous vote
						newValue -= vote.Value;
						// Do the new vote
						newValue += voteValue;

						vote.Value = voteValue;
						vote.Save();
					}

					m_game.m_currentMap.Karma = newValue;
					m_game.m_currentMap.Save();

					string verb = "good";
					if (voteValue == -1) {
						verb = "bad";
					}

					string mapValue = "";
					if (newValue == 0) {
						mapValue = "$6660";
					} else if (newValue < 0) {
						mapValue = "$a77" + newValue;
					} else if (newValue > 0) {
						mapValue = "$7a7" + newValue;
					}

					SendChat(string.Format(m_config["Messages.Karma.Vote"], player.m_nickname, verb, mapValue));
				}
			});
		}
	}
}
