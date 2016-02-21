using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GbxRemoteNet
{
	public class GbxMultiCall : GbxStruct
	{
		[GbxStructName("methodName")]
		public string m_methodName;

		[GbxStructName("params")]
		public Array m_methodParams;
	}
}
