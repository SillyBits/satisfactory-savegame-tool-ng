using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib
{
	// Random helpers

	public static class Helpers
	{
		public static string Hexdump(byte[] data, int width = 16, bool offsets = true, bool ascii = true, int indent = 1)
		{
			if (data == null)
				return "<empty>";

			StringBuilder sb = new StringBuilder();

			int ofs = 0;
			int col = 0;
			int w = (width * 3) + (width >> 2);
			StringBuilder hex = new StringBuilder(w*2);
			StringBuilder asc = ascii ? new StringBuilder(width*2): null;

			hex.Append(' ');
			if (ascii)
				asc.Append("   ");
			foreach (byte b in data)
			{
				if (col % 4 == 0)
					hex.Append(' ');
				hex.Append(' ');
				hex.Append(b.ToString("X2"));
				if (ascii)
					asc.Append((32 <= b && b <= 127) ? (char)b : '.');

				col++;
				if (col >= width)
				{
					if (sb.Length != 0)
						sb.Append('\n');
					//if (indent > 0)
					//	sb.Append('\t', indent);
					sb.Append('\t', indent);
					if (offsets)
						sb.AppendFormat("{0:X4}", ofs);
					sb.Append(hex);
					if (hex.Length < w)
						sb.Append(' ', w - hex.Length);
					if (ascii)
						sb.Append(asc);
					//sb.Append('\n');

					ofs += width;
					hex.Clear();
					hex.Append(' ');
					if (ascii)
					{
						asc.Clear();
						asc.Append("   ");
					}
					col = 0;
				}
			}

			if (col > 0)
			{
				if (sb.Length != 0)
					sb.Append('\n');
				//if (indent > 0)
				//	sb.Append('\t', indent);
				sb.Append('\t', indent);
				if (offsets)
					sb.AppendFormat("{0:X4}", ofs);
				sb.Append(hex);
				if (hex.Length < w)
					sb.Append(' ', 1 + w - hex.Length);
				if (ascii)
					sb.Append(asc);
				//sb.Append('\n');
			}

			return sb.ToString();
		}

	}

}


