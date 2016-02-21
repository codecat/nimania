using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GbxRemoteNet
{
	public class GbxRequest
	{
		public OnGbxResponse m_callback;
		public bool m_finished;
		public AutoResetEvent m_reset = new AutoResetEvent(false);

		public GbxRequest(OnGbxResponse callback)
		{
			m_callback = callback;
		}

		public void Wait()
		{
			if (m_finished) {
				return;
			}
			m_reset.WaitOne();
		}
	}
}
