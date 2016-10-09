using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime.DbModels
{
	public class Map : DbModel
	{
		public static string Tablename { get { return "maps"; } }

		public int ID;
		[DbFieldLength(27)]
		public string UId;
		public string Name;
		public string Author;
		public string FileName;
		public int Karma;
		public int KarmaVotes;

		public UserData m_userData = new UserData();

		public int m_nCheckpoints;

		public int m_timeBronze;
		public int m_timeSilver;
		public int m_timeGold;
		public int m_timeAuthor;
		public bool m_laps;
	}
}
