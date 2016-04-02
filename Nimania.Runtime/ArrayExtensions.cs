using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public static class ArrayExtensions
	{
		public static T Get<T>(this T[] arr, int i, T def = default(T))
		{
			if (i < 0 || i >= arr.Length) {
				return def;
			}
			return arr[i];
		}
	}
}
