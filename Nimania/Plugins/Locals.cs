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
			m_remote.Query("GetCurrentMapInfo", (GbxResponse res) => {
				LoadMapInfo(res.m_value);
			});
			m_remote.AddCallback("TrackMania.BeginChallenge", (GbxCallback cb) => {
				LoadMapInfo(cb.m_params[0]);
			});
		}

		public override void Uninitialize()
		{
		}

		public override void OnAction(string login, string action)
		{
		}

		public void LoadMapInfo(GbxValue val)
		{
			string uid = val.Get<string>("UId");
			var map = m_database.FindByAttributes<Map>("UId", uid);
			if (map == null) {
				map = m_database.Create<Map>();
				map.UId = uid;
				map.Name = val.Get<string>("Name");
				map.Author = val.Get<string>("Author");
				map.FileName = val.Get<string>("FileName");
				map.Save();
      }
			SendView("Locals/Widget.xml");
		}
	}
}
