using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoreLib
{
	public class ImageHandler
	{

		/// <summary>
		/// Convert given image file to base64 notation.
		/// </summary>
		/// <param name="filename">Absolute path to image</param>
		/// <returns>Base64 representation</returns>
		public static string Image2Base64(string filename)
		{
			if (!File.Exists(filename))
				return null;

			byte[] bytes = File.ReadAllBytes(filename);
			return Convert.ToBase64String(bytes);
		}

		/// <summary>
		/// Convert given image to base64 notation.
		/// </summary>
		/// <param name="bitmap">Image to convert</param>
		/// <returns>Base64 representation</returns>
		public static string Image2Base64(BitmapSource bitmap)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				PngBitmapEncoder enc = new PngBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create(bitmap));
				enc.Save(stream);
				stream.Flush();
				bytes = stream.ToArray();
			}
			return Convert.ToBase64String(bytes);
		}


		/// <summary>
		/// Create image from raw bytes
		/// </summary>
		/// <param name="data">Pixels (row-major)</param>
		/// <param name="width">Width of image</param>
		/// <param name="height">Height of image</param>
		/// <param name="depth">Color depth</param>
		/// <returns>Bitmap created, or null if failed</returns>
		public static BitmapSource ImageFromBytes(byte[] data, int width = -1, int height = -1, int depth = -1)
		{
			if (width != -1 && height != -1)
				throw new ArgumentException("ImageFromBytes: Only quadratic images supported for now");
			if (depth == -1)
				throw new ArgumentException("ImageFromBytes: Depth must be passed in");
						
			double pixels = data.Length / depth;
			if ((int)pixels != pixels)
				throw new ArgumentException("ImageFromBytes: Depth given does NOT match");

			int w = (int) Math.Sqrt(pixels);
			if (w*w != pixels)
				throw new ArgumentException("IamgeFromBytes: Image is not of color depth 4");

			PixelFormat fmt = PixelFormats.Bgra32;
			BitmapSource bmp = BitmapSource.Create(w, w, 96,96, fmt, null, data, w*depth);

			return bmp;
		}

		/// <summary>
		/// Save image to file.
		/// </summary>
		/// <param name="bitmap">Image to save</param>
		/// <param name="filename">Absolute path to where to store image, incl. extension</param>
		/// <returns>Whether or not save operation was successful</returns>
		public static bool SaveImageToFile(BitmapSource bitmap, string filename)
		{
			if (bitmap == null || filename == null)
				return false;
			if (!Directory.Exists(Path.GetDirectoryName(filename)))
				return false;

			string ext = Path.GetExtension(filename).ToLower();

			BitmapEncoder enc = null;
			switch (ext)
			{
				case ".bmp":  enc = new BmpBitmapEncoder(); break;
				case ".gif":  enc = new GifBitmapEncoder(); break;
				case ".jpeg": enc = new JpegBitmapEncoder(); break;
				case ".jpg":  enc = new JpegBitmapEncoder(); break;
				case ".png":  enc = new PngBitmapEncoder(); break;
				case ".tiff": enc = new TiffBitmapEncoder(); break;
			}
			if (enc == null)
			{
				Log.Error("No suitable encoder found for file '{0}'", filename);
				return false;
			}

			try
			{
				enc.Frames.Add(BitmapFrame.Create(bitmap));

				using (Stream out_strm = File.Create(filename))
				{
					enc.Save(out_strm);
					out_strm.Flush();
				}
			}
			catch (Exception exc)
			{
				Log.Error("Error saving file '{0}': {1}", filename, exc);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Load an image from file.
		/// </summary>
		/// <param name="filename">Absolute path to image to load, incl. extension</param>
		/// <returns>Bitmap instance, or null if loading failed</returns>
		public static BitmapSource LoadImageFromFile(string filename)
		{
			if (filename == null)
				return null;
			if (!File.Exists(filename))
				return null;

			string ext = Path.GetExtension(filename).ToLower();

			BitmapSource bitmap = null;

			try
			{
				using (Stream in_strm = File.OpenRead(filename))
				{
					BitmapCreateOptions cr_option = BitmapCreateOptions.PreservePixelFormat;
					BitmapCacheOption   ca_option = BitmapCacheOption.OnLoad;
					BitmapDecoder dec = null;
					switch (ext)
					{
						case ".bmp":  dec = new BmpBitmapDecoder (in_strm, cr_option, ca_option); break;
						case ".gif":  dec = new GifBitmapDecoder (in_strm, cr_option, ca_option); break;
						case ".jpeg": dec = new JpegBitmapDecoder(in_strm, cr_option, ca_option); break;
						case ".jpg":  dec = new JpegBitmapDecoder(in_strm, cr_option, ca_option); break;
						case ".png":  dec = new PngBitmapDecoder (in_strm, cr_option, ca_option); break;
						case ".tiff": dec = new TiffBitmapDecoder(in_strm, cr_option, ca_option); break;
					}
					if (dec == null)
					{
						Log.Error("No suitable encoder found for file '{0}'", filename);
						return null;
					}

					bitmap = dec.Frames[0];
				}
			}
			catch (Exception exc)
			{
				Log.Error("Error loading file '{0}': {1}", filename, exc);
				bitmap = null;
			}

			return bitmap;
		}

	}
}
