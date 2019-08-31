using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace CoreLib.Attributes
{

	// Attributes
	//

	
	/// <summary>
	/// Basic attribute implementation which allows for checking 
	/// for existance and getting actual attribute value.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class AttrBase : Attribute
	{
		public object Value { get; protected set; }

		public static bool Has<_Attr>(Type type)
			where _Attr : AttrBase
		{
			return (type.GetCustomAttribute<_Attr>(false) != null);
		}

		public static _Type Get<_Attr,_Type>(Type type)
			where _Attr : AttrBase
		{
			_Attr attr = type.GetCustomAttribute<_Attr>(false);
			if (attr == null)
				return default(_Type);
			return (_Type)(attr.Value);
		}
	}


	/// <summary>
	/// A string attribute
	/// </summary>
	public class StringAttr : AttrBase
	{
		public static string Get<_Attr>(Type type)
			where _Attr : StringAttr
		{
			return Get<_Attr, string>(type);
		}
	}


	/// <summary>
	/// A numeric attribute
	/// </summary>
	public class LongAttr : AttrBase
	{
		public static long Get<_Attr>(Type type)
			where _Attr : LongAttr
		{
			return Get<_Attr, long>(type);
		}
	}


	/// <summary>
	/// An image attribute.
	/// (its value is a relative path to a resource embedded into assembly)
	/// </summary>
	public class ImageRefAttr : AttrBase
	{
		public ImageRefAttr(string icon)
		{
			Value = icon;
		}

		public static bool Has(Type type)
		{
			return Has<ImageRefAttr>(type);
		}

		public static ImageSource Get(Type type)
		{
			ImageSource image = null;

			try
			{
				string name = Get<ImageRefAttr, string>(type);
				if (name != null)
				{
					Assembly ass = Assembly.GetAssembly(type);
					if (ass != null)
					{
						string path = string.Format("pack://application:,,,/{0};component/{1}",
							ass.GetName(), name);
						Uri uri = new Uri(path);
						image = new BitmapImage(uri);
					}
				}
			}
			catch
			{
				image = null;
			}

			return image;
		}
	}

}
