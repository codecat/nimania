using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime.DbModels;

namespace Nimania.Runtime
{
	public class PlayerInfo
	{
		public int m_id;
		public string m_login;
		public string m_nickname;
		public int m_team;
		public bool m_spectating;
		public bool m_officialMode;
		public int m_ladder;

		public LocalPlayer m_localPlayer;
	}
}
