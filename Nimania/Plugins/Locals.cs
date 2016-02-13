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
		public override void Initialize()
		{
			//TODO: Make an abstraction layer for checkpoints/finishing?
			m_remote.AddCallback("TrackMania.PlayerFinish", (GbxCallback cb) => {
				string login = cb.m_params[1].Get<string>();
				int time = cb.m_params[2].Get<int>();

				// time is 0 on respawning
				if (time == 0) {
					return;
				}

				m_remote.Execute("ChatSendServerMessage", "$fffFinish time for $<$f00" + login + "$>: " + Utils.TimeString(time));
			});

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
			// sadly, we are forced to send the entire thing every time it updates. :(
			string missTag = "$i$f39»$666Velox$f39|$fffMiss$f39..ノ";

			string xmlItems = "";
			for (int i = 0; i < 25; i++) {
				xmlItems += GetView("Locals/Item.xml",
					"y", (-3.5 * i).ToString(),
					"place", (i + 1).ToString(),
					"name", Utils.XmlEntities(missTag),
					"time", Utils.TimeString(1234 + 456 * i * i));
			}
			SendView("Locals/Widget.xml", "items", xmlItems);
		}
	}
}
