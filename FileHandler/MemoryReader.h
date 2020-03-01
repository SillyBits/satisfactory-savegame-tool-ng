#pragma once

using namespace FileHandler;

namespace Reader
{

	// Specialized memory reader
	public ref class MemoryReader : ReaderBase
	{
	public:
		MemoryReader(byte* buff, const int length, ICallback^ callback);
		MemoryReader(array<byte>^ buff, ICallback^ callback);

		// Properties not handled by ReaderBase
		virtual property const __int64 Size { const __int64 get() override; };
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		virtual const __int64 Seek(__int64 offset, IReader::Positioning pos) override;

	protected:
		byte* _buff;
		__int64 _size;
		bool _owned;

		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) override;

	};

};
