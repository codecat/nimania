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
		public string UId;
		public string Name;
		public string Author;
		public string FileName;

		public int m_nCheckpoints;
	}
}
