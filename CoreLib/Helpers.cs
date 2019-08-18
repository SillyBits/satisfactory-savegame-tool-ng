using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;


namespace CoreLib
{

	// Random helpers
	public static class Helpers
	{

		/// <summary>
		/// Reads a file, returning it's content as byte array.
		/// Note that file will be read in binary mode, so there won't be any CRLF-conversion or such.
		/// </summary>
		/// <param name="filename">File to read</param>
		/// <returns>File contents</returns>
		public static byte[] GetFileContents(string filename)
		{
			try
			{
				using (FileStream in_stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (MemoryStream mem = new MemoryStream())
					{
						in_stream.CopyTo(mem);
						mem.Flush();
						return mem.ToArray();
					}
				}
			}
			catch
			{
				return null;
			}
		}


		/// <summary>
		/// Create a hexdump for data given.
		/// </summary>
		/// <param name="data">Data to dump</param>
		/// <param name="width">No. of bytes per row</param>
		/// <param name="offsets">Whether or not to include offset in string returned</param>
		/// <param name="ascii">Whether or not to include ASCII chars in string returned</param>
		/// <param name="indent">No. of tab chars to add in front of each row</param>
		/// <returns>String containing dump</returns>
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


		/// <summary>
		/// Open a subkey in Windows registry.
		/// </summary>
		/// <param name="root">Root key to start with (HKLM, HKCU, ...)</param>
		/// <param name="path">Path to open (separated by '/')</param>
		/// <param name="writable">Whether or not to open as writable</param>
		/// <returns>Registry key, or null if not found</returns>
		public static RegistryKey OpenRegKey(RegistryKey root, string path, bool writable = false)
		{
			if (root == null || path == null)
				return null;

			string[] subs = path.Split('/');
			return OpenRegKey(root, subs, writable);
		}

		/// <summary>
		/// Open a subkey in Windows registry.
		/// </summary>
		/// <param name="root">Root key to start with (HKLM, HKCU, ...)</param>
		/// <param name="path">Array of paths to open</param>
		/// <param name="writable">Whether or not to open as writable</param>
		/// <returns>Registry key, or null if not found</returns>
		public static RegistryKey OpenRegKey(RegistryKey root, string[] path, bool writable = false)
		{
			if (root == null || path == null)
				return null;

			RegistryKey key = root;
			try
			{
				RegistryKey new_key;
				foreach (string sub in path)
				{
					new_key = key.OpenSubKey(sub, writable);
					key.Dispose();
					key = new_key;
					if (key == null)
						break;
				}
			}
			catch
			{
				// Access failed, maybe due to missing access restrictions
				if (key != null)
				{
					key.Dispose();
					key = null;
				}
			}

			return key;
		}


		/// <summary>
		/// Build Uri based on calling assembly and resource path given.
		/// </summary>
		/// <param name="resourcepath">Relative path to resource</param>
		/// <returns>Uri describing resource location</returns>
		public static Uri GetResourceUri(string resourcepath)
		{
			return GetResourceUri(Assembly.GetCallingAssembly(), resourcepath);
		}

		/// <summary>
		/// Build Uri based on assembly and resource path given.
		/// </summary>
		/// <param name="assembly">Assembly to reference</param>
		/// <param name="resourcepath">Relative path to resource</param>
		/// <returns>Uri describing resource location</returns>
		public static Uri GetResourceUri(Assembly assembly, string resourcepath)
		{
			string path = string.Format("pack://application:,,,/{0};component/Resources/{1}",
										assembly.GetName(), resourcepath);
			return new Uri(path);
		}

	}

}


