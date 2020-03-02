#pragma once

#include "Cloudsave.h"

namespace Reader
{

	// Specialized cloud save reader
	public ref class CloudsaveReader : ReaderBase
	{
	public:
		CloudsaveReader(IReader^ filereader, ICallback^ callback);

		virtual ~CloudsaveReader();
		!CloudsaveReader();

		virtual property const __int64 Pos { const __int64 get() override; };
		virtual property const __int64 PrevPos { const __int64 get() override; void set(const __int64) override; };
		virtual property const __int64 Size { const __int64 get() override; };
		virtual property String^ Name { String^ get() override; };
		virtual property String^ Fullname { String^ get() override; };

		virtual void Close() override;

		virtual const __int64 Seek(__int64 offset, IReader::Positioning pos) override;

	protected:
		IReader^                    _reader;
		__int64                     _offset;     // relative to current chunk
		__int64                     _prev_offset;// absolute
		__int64                     _size;
		bool                        _eof;
		Cloudsave::ChunkList       *_chunks;
		Cloudsave::ChunkList::Node *_curr_chunk;
		byte                       *_chunk_buf;
		int                         _chunk_buf_len;
	//	//vvvvv TEMP
	//	Writer::FileWriter^ _debug_writer;
	//	//^^^^^ TEMP

		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) override;

		void _Init();

		bool _Prepare(__int64 offset);

	};

};
