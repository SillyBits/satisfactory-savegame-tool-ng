#pragma once


namespace PakHandler {

	public ref class PakLoader
	{
	public:
		String^ Filename;

		Structures::Footer^ Footer;
		String^ MountPoint;
		Structures::IndexCollection^ Index;

		PakLoader(String^ filename);

		bool Load(ICallback^ callback);

		array<byte>^ ReadRaw(String^ filename);
		array<byte>^ ReadRaw(Structures::IndexEntry^ index);
		
		array<byte>^ ReadAsset(String^ filename);
		array<byte>^ ReadAsset(String^ filename, ...array<String^>^ extensions);

		Structures::FObject^ ReadObject(String^ filename);
		Structures::FObject^ ReadObject(String^ filename, ...array<String^>^ extensions);

		Structures::FTexture2D^ ReadTexture(String^ filename);

		void Close();

	protected:
		ICallback^ _callback;
		FileReader^ _reader;
		array<String^>^ _asset_extensions;

		virtual ~PakLoader();

		void _cbStart(long count, String^ status, String^ info);
		void _cbUpdate(long index, String^ status, String^ info);
		void _cbStop(String^ status, String^ info);

		static void LogRedirect(String^ s);

		bool _Load(ICallback^ callback);

	};

}
