using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;


namespace CoreLib
{
	/// <summary>
	/// Compression helpers
	/// </summary>
	public static class Compressor
	{

		/// <summary>
		/// Compresses given file into an archive, writing archive to disk.
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="filename">File to compress (with optional path)</param>
		/// <param name="path">Path to remove from filename, null to preserve full path</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, string filename, string path = null)
		{
			return CompressToFile(archive, new List<string> { filename }, path);
		}

		/// <summary>
		/// Compresses given files into an archive, writing archive to disk.
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="files">Files to compress as list</param>
		/// <param name="path">Path to remove from filenames, null to preserve full paths</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, List<string> files, string path = null)
		{
			Dictionary<string,Stream> buffers = null;
			try
			{
				if (path != null && !path.EndsWith("\\"))
					path += "\\";
				buffers = files.ToDictionary(file => (path == null) ? file : file.Substring(path.Length), 
											 file => (Stream)File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
				return CompressToFile(archive, buffers);
			}
			catch
			{
				return false;
			}
			finally
			{
				if (buffers != null)
					buffers.ToList().ForEach(pair => pair.Value.Dispose());
			}
		}

		/// <summary>
		/// Compresses given data into an archive, writing archive to disk.
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="data">Data to compress as dictionary</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, Dictionary<string,byte[]> data)
		{
			Dictionary<string,Stream> buffers = null;
			try
			{
				buffers = data.ToDictionary(pair => pair.Key,
											pair => (Stream)new MemoryStream(pair.Value));
				return CompressToFile(archive, buffers);
			}
			catch
			{
				return false;
			}
			finally
			{
				if (buffers != null)
					buffers.ToList().ForEach(pair => pair.Value.Dispose());
			}
		}

		/// <summary>
		/// Compresses given data into an archive, writing archive to disk.
		/// </summary>
		/// <param name="archive">Name of archive to create (with optional path)</param>
		/// <param name="data">Data to compress as dictionary</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Outcome</returns>
		public static bool CompressToFile(string archive, Dictionary<string,Stream> data)
		{
			try
			{
				using (FileStream out_stream = File.Create(archive))
					return CompressToStream(out_stream, data);
			}
			catch
			{
				return false;
			}
		}


		/// <summary>
		/// Compresses given file into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <param name="path">Path to remove from filename, null to preserve full path</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(string filename, string path = null)
		{
			try
			{
				if (path != null && !path.EndsWith("\\"))
					path += "\\";
				using (FileStream in_stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
					return CompressToArray(in_stream, (path == null) ? filename : filename.Substring(path.Length));
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(byte[] data, string filename)
		{
			try
			{
				using (MemoryStream mem = new MemoryStream(data))
					return CompressToArray(new Dictionary<string,Stream> { { filename, mem } });
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <param name="filename">Filename to assign (with optional path)</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(Stream stream, string filename)
		{
			return CompressToArray(new Dictionary<string,Stream> { { filename, stream } });
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress as dictionary</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(Dictionary<string,byte[]> data)
		{
			Dictionary<string,Stream> buffers = null;
			try
			{
				buffers = data.ToDictionary(pair => pair.Key,
											pair => (Stream)new MemoryStream(pair.Value));
				return CompressToArray(buffers);
			}
			catch
			{
				return null;
			}
			finally
			{
				if (buffers != null)
					buffers.ToList().ForEach(pair => pair.Value.Dispose());
			}
		}

		/// <summary>
		/// Compresses given data into an archive, returning archive as byte array.
		/// </summary>
		/// <param name="data">Data to compress as dictionary</param>
		/// <returns>Archive as byte array</returns>
		public static byte[] CompressToArray(Dictionary<string,Stream> data)
		{
			if (data == null || data.Count == 0)
				return null;

			try
			{
				using (MemoryStream mem = new MemoryStream())
				{
					if (CompressToStream(mem, data))
					{
						mem.Flush();
						return mem.ToArray();
					}
				}
			}
			catch { }
			return null;
		}


		/// <summary>
		/// Compresses given data into stream given.
		/// </summary>
		/// <param name="stream">Stream to write compressed data to</param>
		/// <param name="data">Data to compress as dictionary</param>
		/// <returns>True on success, else False</returns>
		public static bool CompressToStream(Stream stream, Dictionary<string,Stream> data)
		{
			if (data == null || data.Count == 0)
				return false;

			try
			{
				using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					foreach (var pair in data)
					{
						ZipArchiveEntry entry = zip.CreateEntry(pair.Key, CompressionLevel.Optimal);
						using (Stream strm = entry.Open())
						{
							pair.Value.CopyTo(strm);
							strm.Flush();
						}
					}
				}

				stream.Flush();
				return true;
			}
			catch
			{
				return false;
			}
		}


		/// <summary>
		/// Compresses data array, returning inflated byte array.
		/// </summary>
		/// <param name="data">Data to compress</param>
		/// <returns>Compressed data array</returns>
		public static byte[] Inflate(byte[] data)
		{
			using (MemoryStream mem = new MemoryStream())
			{
				using (DeflateStream inflater = new DeflateStream(mem, CompressionMode.Compress))
				{
					inflater.Write(data, 0, data.Length);
					inflater.Flush();
				}

				mem.Flush();
				return mem.ToArray();
			}
		}

		/// <summary>
		/// Decompresses data array, returning original data as array.
		/// </summary>
		/// <param name="data">Data to decompress</param>
		/// <returns>Decompressed data array</returns>
		public static byte[] Deflate(byte[] data)
		{
			using (MemoryStream out_stream = new MemoryStream())
			{
				using (MemoryStream in_stream = new MemoryStream(data))
				{
					using (DeflateStream deflater = new DeflateStream(in_stream, CompressionMode.Decompress))
					{
						deflater.CopyTo(out_stream);
					}
				}

				out_stream.Flush();
				return out_stream.ToArray();
			}
		}

	}

}
