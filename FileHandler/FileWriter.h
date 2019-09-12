#pragma once

using namespace FileHandler;

namespace Writer
{

	// Specialized file writer
	public ref class FileWriter : WriterBase
	{
	public:
		FileWriter(String^ filename, ICallback^ callback);

		virtual ~FileWriter();
		!FileWriter();

		// Properties not handled by WriterBase
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		//virtual const __int64 Seek(__int64 offset, IWriter::Positioning pos) override;
		//=> For now, no seeking while writing

	protected:
		String^ _filename;
		FILE *_handle;

		// Write N bytes to underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) override;

	};

};
