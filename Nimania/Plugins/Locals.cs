using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Locals : Plugin
	{
		public override void Initialize()
		{
			m_remote.Query("GetCurrentMapInfo", (GbxResponse res) => {
				string uid = res.m_value.Get<string>("UId");
				LoadMapInfo(uid);
			});
			m_remote.AddCallback("TrackMania.BeginChallenge", (GbxCallback cb) => {
				string uid = cb.m_params[0].Get<string>("UId");
				LoadMapInfo(uid);
			});
		}

		public override void Uninitialize()
		{
		}

		public override void OnAction(string login, string action)
		{
		}

		public void LoadMapInfo(string uid)
		{

		}
	}
}
