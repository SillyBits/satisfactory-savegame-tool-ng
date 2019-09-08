using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
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
		/// <param name="offset">Offset to where to start reading (defaults to start of file)</param>
		/// <param name="length">Length of data to read (defaults to all)</param>
		/// <returns>File contents</returns>
		public static byte[] GetFileContents(string filename, long offset = 0, long length = 0)
		{
			try
			{
				using (FileStream in_stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (MemoryStream mem = new MemoryStream())
					{
						if (length <= 0)
						{
							in_stream.CopyTo(mem);
						}
						else
						{
							if (offset < 0)
							{
								length += offset;
								offset = 0;
							}
							if (offset + length > in_stream.Length)
								length = in_stream.Length - offset;

							byte[] data = new byte[length];
							in_stream.Seek(offset, SeekOrigin.Begin);
							in_stream.Read(data, 0, (int)length);
							mem.Write(data, 0, data.Length);
						}

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
		/// <param name="rel_offset">Relative offset to use for printing (optional)</param>
		/// <returns>String containing dump</returns>
		public static string Hexdump(byte[] data, int width = 16, bool offsets = true, bool ascii = true, int indent = 1,
			long rel_offset = -1)
		{
			if (data == null)
				return "<empty>";

			StringBuilder sb = new StringBuilder();

			long ofs = 0;
			string ofs_fmt = null;
			if (offsets)
			{
				if (rel_offset > 0)
					ofs = rel_offset;
				long max = ofs + data.Length;
				if (max <= 0xFFFF)
					ofs_fmt = "{0:X4}";
				else if (max <= 0xFFFFFFFFL)
					ofs_fmt = "{0:X8}";
				else
					ofs_fmt = "{0:X12}";
			}

			int col = 0;
			int w = (width * 3) + (width >> 2);
			StringBuilder hex = new StringBuilder(w * 2);
			StringBuilder asc = ascii ? new StringBuilder(width * 2) : null;

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
					sb.Append('\t', indent);
					if (offsets)
						sb.AppendFormat(ofs_fmt, ofs);
					sb.Append(hex);
					if (hex.Length < w)
						sb.Append(' ', w - hex.Length);
					if (ascii)
						sb.Append(asc);

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
				sb.Append('\t', indent);
				if (offsets)
					sb.AppendFormat(ofs_fmt, ofs);
				sb.Append(hex);
				if (hex.Length < w)
					sb.Append(' ', 1 + w - hex.Length);
				if (ascii)
					sb.Append(asc);
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


		/// <summary>
		/// Pack given string, used to hide data from simple crawlers.
		/// </summary>
		/// <param name="unpacked">String to be cloaked</param>
		/// <returns>Cloaked version of string</returns>
		public static string Pack(string unpacked)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(unpacked);
			byte[] compressed = Compressor.Inflate(bytes);
			return Convert.ToBase64String(compressed);
		}

		/// <summary>
		/// Unpacks string created by <see cref="Pack"/> earlier.
		/// </summary>
		/// <param name="packed">Cloaked string to unpack</param>
		/// <returns>Unpacked string</returns>
		public static string Unpack(string packed)
		{
			byte[] bytes = Convert.FromBase64String(packed);
			byte[] uncompressed = Compressor.Deflate(bytes);
			return Encoding.ASCII.GetString(uncompressed);
		}


		/// <summary>
		/// Used to pick a public method from within a "container" class.
		/// Note that this approach does NOT distinguish between different return parameter types.
		/// Also note, when dealing with 'out' or 'ref', make sure to pass a 'ByRef' type (by calling Type.MakeByRefType).
		/// </summary>
		/// <param name="container">Container class to search</param>
		/// <param name="name">Method name</param>
		/// <param name="args">Method's parameters</param>
		/// <returns>Method info, or null if not found</returns>
		public static MethodInfo PickMethod(Type container, string name, params Type[] args)
		{
			return PickMethod(container, BindingFlags.Instance | BindingFlags.Public, name, args);
		}

		/// <summary>
		/// Used to pick a static method from within a "container" class.
		/// Note that this approach does NOT distinguish between different return parameter types.
		/// Also note, when dealing with 'out' or 'ref', make sure to pass a 'ByRef' type (by calling Type.MakeByRefType).
		/// </summary>
		/// <param name="container">Container class to search</param>
		/// <param name="name">Method name</param>
		/// <param name="args">Method's parameters</param>
		/// <returns>Method info, or null if not found</returns>
		public static MethodInfo PickStaticMethod(Type container, string name, params Type[] args)
		{
			return PickMethod(container, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, name, args);
		}

		/// <summary>
		/// Used to pick a method from within a "container" class.
		/// Note that this approach does NOT distinguish between different return parameter types.
		/// Also note, when dealing with 'out' or 'ref', make sure to pass a 'ByRef' type (by calling Type.MakeByRefType).
		/// </summary>
		/// <param name="container">Container class to search</param>
		/// <param name="bind_flags">Binding flags to use</param>
		/// <param name="name">Method name</param>
		/// <param name="args">Method's parameters</param>
		/// <returns>Method info, or null if not found</returns>
		public static MethodInfo PickMethod(Type container, BindingFlags bind_flags, string name, params Type[] args)
		{
			try
			{
				MethodInfo method = container.GetMethod(name, bind_flags, null, args, null);
				if (method != null)
					return method;
			}
			catch (AmbiguousMatchException)
			{ }

			// No exact match avail or ambiguous, traverse methods avail and try to pick one.
			// For this to work as expected, any generic method must be located after any if 
			// it's non-generic overloads.
			var methods = container
				.GetMethods(bind_flags)
				.Where(mi => mi.Name == name)
				;
			foreach (MethodInfo mi in methods)
			{
				var parms = mi.GetParameters();
				if (parms.Length == args.Length)
				{
					bool matches = true;
					for (int i = 0; i < args.Length; ++i)
						matches &= (parms[i].ParameterType == args[i]);
					if (matches)
						return mi;
				}
			}

			return null;
		}


		/// <summary>
		/// Allows for triggering dispatcher while waiting for some other task to complete
		/// </summary>
		/// <param name="dispatcher"></param>
		public static void TriggerDispatcher(Dispatcher dispatcher)
		{
			dispatcher.Invoke(() => { }, DispatcherPriority.Render);
			System.Threading.Thread.Yield();
			dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
		}
	}

}


