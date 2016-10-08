using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public class DbFieldLengthAttribute : Attribute
	{
		public int m_length;

		public DbFieldLengthAttribute(int length)
		{
			m_length = length;
		}
	}
}
