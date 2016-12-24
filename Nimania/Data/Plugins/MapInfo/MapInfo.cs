using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class MapInfo : Plugin
	{
		public override void Initialize()
		{
			SendWidget();
		}

		public override void OnBeginChallenge()
		{
			SendWidget();
		}

		public override void OnPlayerConnect(PlayerInfo player)
		{
			SendWidget(player.m_login);
		}

		public void SendWidget(string login = "")
		{
			if (login == "") {
				SendView("MapInfo/Widget.xml",
					"name", Utils.XmlEntities(m_game.m_currentMap.Name),
					"author", Utils.XmlEntities(m_game.m_currentMap.Author),
					"time", Utils.TimeString(m_game.m_currentMap.m_timeAuthor));
			} else {
				SendViewToLogin(login, "MapInfo/Widget.xml",
					"name", Utils.XmlEntities(m_game.m_currentMap.Name),
					"author", Utils.XmlEntities(m_game.m_currentMap.Author),
					"time", Utils.TimeString(m_game.m_currentMap.m_timeAuthor));
			}
		}

		public override void SoftReload()
		{
			SendWidget();
		}
	}
}
