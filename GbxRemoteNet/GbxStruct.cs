using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GbxRemoteNet
{
	public abstract class GbxStruct { }

	public class GbxStructNameAttribute : Attribute
	{
		public string m_name;

		public GbxStructNameAttribute(string name)
		{
			m_name = name;
		}
	}
}
