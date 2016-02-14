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

		public List<int> m_checkpoints = new List<int>();
		public int m_prevBestTime = -1;
		public int m_bestTime = -1;
		public int m_lastTime = -1;

		public int m_score = 0;

		public DateTime m_joinTime;

		public LocalPlayer m_localPlayer;
	}
}
