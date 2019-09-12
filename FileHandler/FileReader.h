#pragma once

using namespace FileHandler;

namespace Reader
{

	// Specialized file reader
	public ref class FileReader : ReaderBase
	{
	public:
		FileReader(String^ filename, ICallback^ callback);

		virtual ~FileReader();
		!FileReader();

		// Properties not handled by ReaderBase
		virtual property const __int64 Size { const __int64 get() override; };
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		virtual const __int64 Seek(__int64 offset, IReader::Positioning pos) override;

	protected:
		String^ _filename;
		__int64 _size;
		FILE *_handle;

		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) override;

	};

};
