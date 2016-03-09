using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public static class StringExtensions
	{
		public static string[] SplitCommandline(this string str, bool bUseLiterals = true)
		{
			List<string> ret = new List<string>();

			// test "test test"
			// [
			//   "test"
			//   "test test"
			// ]

			// test "test"
			// [
			//   "test"
			//   "test"
			// ]

			string buffer = "";
			bool inStr = false;

			for (int i = 0; i < str.Length; i++) {
				char c = str[i];

				// literals
				if (c == '\\') {
					if (bUseLiterals) {
						if (i + 1 < str.Length) {
							buffer += str[++i];
						}
					} else {
						buffer += '\\';
					}
				}

				// strings
				else if (c == '"') {
					if (inStr) {
						// string ends
						inStr = false;
						ret.Add(buffer);
						buffer = "";
						if (i + 1 < str.Length && str[i + 1] == ' ') {
							i++;
						}
					} else {
						// string starts
						inStr = true;
					}
				}

				// words
				else if (c == ' ') {
					if (inStr) {
						buffer += ' ';
					} else {
						ret.Add(buffer);
						buffer = "";
					}
				}

				// characters
				else {
					buffer += c;
				}
			}

			// last word
			if (buffer != "") {
				ret.Add(buffer);
			}

			return ret.ToArray();
		}
	}
}
