using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;


namespace CoreLib
{
	/// <summary>
	/// Compression helpers
	/// </summary>
	public static class Compressor
	{

		/// <summary>
		/// Compresses given file into an archive, writing archive to disk.
		/// Note that input path is preserved, incl. any drive letter!
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="filename">File to compress (with optional path)</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, string filename)
		{
			return CompressToFile(archive, new List<string> { filename });
		}

		/// <summary>
		/// Compresses given files into an archive, writing archive to disk.
		/// Note that input paths are preserved, incl. any drive letter!
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="filename">Files to compress as list</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, List<string> files)
		{
			try
			{
				Dictionary<string,byte[]> buffers = new Dictionary<string,byte[]>();
				foreach (var file in files)
					buffers.Add(file, Helpers.GetFileContents(file));

				return CompressToFile(archive, buffers);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Compresses given data into an archive, writing archive to disk.
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="data">Data to compress as dictionary</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, Dictionary<string,byte[]> data)
		{
			try
			{
				byte[] content = CompressToArray(data);
				if (content == null)
					return false;

				using (FileStream out_stream = File.Create(archive))
				{
					out_stream.Write(content, 0, content.Length);
					out_stream.Flush();
				}

				return true;
			}
			catch
			{
				return false;
			}
		}


		/// <summary>
		/// Compresses given file into an archive, returning archive as byte array.
		/// Note that, in contrast to <see cref="CompressToFile"/>, this does NOT prevent full path.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(string filename)
		{
			return CompressToArray(Helpers.GetFileContents(filename), Path.GetFileName(filename));
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(byte[] data, string filename)
		{
			return CompressToArray(new Dictionary<string,byte[]> { { filename, data } });
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress as dictionary</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(Dictionary<string,byte[]> data)
		{
			if (data == null || data.Count == 0)
				return null;

			try
			{
				using (MemoryStream mem = new MemoryStream())
				{
					using (ZipArchive zip = new ZipArchive(mem, ZipArchiveMode.Create))
					{
						foreach (var pair in data)
						{
							ZipArchiveEntry entry = zip.CreateEntry(pair.Key, CompressionLevel.Optimal);
							using (Stream strm = entry.Open())
							{
								strm.Write(pair.Value, 0, pair.Value.Length);
								strm.Flush();
							}
						}
					}

					mem.Flush();
					return mem.ToArray();
				}
			}
			catch
			{
				return null;
			}
		}

	}

}
