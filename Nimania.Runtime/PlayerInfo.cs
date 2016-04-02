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
		public bool m_connected = true;

		public string m_login;
		public string m_nickname;
		public int m_team;
		public bool m_spectating;
		public bool m_officialMode;
		public int m_ladder;

		public string StrippedNickname { get { return Utils.StripFormatCodes(m_nickname); } }
		public string NoLinkNickname { get { return Utils.StripLinkCodes(m_nickname); } }

		public List<int> m_checkpoints = new List<int>();
		public List<int> m_checkpointsAll = new List<int>();
		public List<int> m_bestCheckpoints = new List<int>();
		public List<int> m_bestCheckpointsLap = new List<int>();
		public int m_prevBestTime = -1;
		public int m_bestTime = -1;
		public int m_lastTime = -1;
		public int m_lastTimeLap = -1;

		public int m_score = 0;

		public DateTime m_joinTime;

		public LocalPlayer m_localPlayer;

		public bool IsAdmin
		{
			get
			{
				if (m_localPlayer.Group == null) {
					return false;
				}
				return m_localPlayer.Group.IsAdmin;
			}
		}

		public bool IsDeveloper
		{
			get
			{
				if (m_localPlayer.Group == null) {
					return false;
				}
				return m_localPlayer.Group.IsDeveloper;
			}
		}
	}
}
