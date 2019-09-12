#pragma once

#include "Cloudsave.h"
#include "CloudsaveReader.h"

namespace Writer
{
	// Specialized cloud save writer
	//

	public ref class CloudsaveWriter : WriterBase
	{
	public:
		CloudsaveWriter(IWriter^ filewriter, ICallback^ callback);

		virtual ~CloudsaveWriter();
		!CloudsaveWriter();

		// Properties not handled by WriterBase
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		//virtual const __int64 Seek(__int64 offset, IWriter::Positioning pos) override;
		//=> For now, no seeking while writing
		
	protected:
		IWriter^  _writer;
		__int64   _offset;
		byte     *_write_buf;
		int       _write_buf_len;
		byte     *_chunk_buf;
		int       _chunk_buf_len;
	//	//vvvvv TEMP
	//	Writer::FileWriter^ _debug_writer;
	//	//^^^^^ TEMP
		
		// Write N bytes to underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) override;

		void _Init();

		void _Store();

	};

};
