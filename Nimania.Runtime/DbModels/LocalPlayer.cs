using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nimania.Runtime.DbModels
{
	public class LocalPlayer : DbModel
	{
		public static string Tablename { get { return "players"; } }

		public int ID;
		public string Login;
		public string Nickname;
		public LocalPlayerGroup Group;
		public int Visits;

		public string StrippedNickname { get { return Utils.StripFormatCodes(Nickname); } }
		public string NoLinkNickname { get { return Utils.StripLinkCodes(Nickname); } }
	}
}
