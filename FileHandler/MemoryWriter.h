#pragma once

using namespace FileHandler;

namespace Writer
{

	// Specialized memory writer
	public ref class MemoryWriter : WriterBase
	{
	public:
		MemoryWriter(byte* buff, const int length, ICallback^ callback);

		// Properties not handled by WriteerBase
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		//virtual const __int64 Seek(__int64 offset, IWriter::Positioning pos) override;
		//=> For now, no seeking while writing
		
	protected:
		byte* _buff;
		__int64 _size;
		
		// Write N bytes to underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) override;

	};

};
