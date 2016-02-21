using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GbxRemoteNet
{
	public static class BinaryStreamExtensions
	{
		public static void WriteStringBytes(this BinaryWriter writer, string str)
		{
			writer.WriteStringBytes(str, Encoding.UTF8);
		}

		public static void WriteStringBytes(this BinaryWriter writer, string str, Encoding encoding)
		{
			writer.Write(encoding.GetBytes(str));
		}

		public static string ReadString(this BinaryReader reader, uint n)
		{
			return reader.ReadString(n, Encoding.UTF8);
		}

		public static string ReadString(this BinaryReader reader, uint n, Encoding encoding)
		{
			if (n == 0) {
				return "";
			}
			var buffer = reader.ReadBytes((int)n);
			return encoding.GetString(buffer);
		}
	}
}
