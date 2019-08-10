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
	}
}
