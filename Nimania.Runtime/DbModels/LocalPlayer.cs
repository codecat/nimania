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
		public int Visits;

		public string StrippedNickname
		{
			get
			{
				return Regex.Replace(Nickname, "\\$([0-9a-f]{3}|[ibnwsz<>]|[lh](\\[[^\\]]+\\])?)", "");
			}
		}

		public string NoLinkNickname
		{
			get
			{
				return Regex.Replace(Nickname, "\\$([lh](\\[[^\\]]+\\])?)", "");
			}
		}
	}
}
