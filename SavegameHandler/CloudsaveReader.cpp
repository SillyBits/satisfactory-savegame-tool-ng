#include "stdafx.h"
#include "CloudsaveReader.h"


namespace Reader 
{

	// Specialized cloud save reader
	CloudsaveReader::CloudsaveReader(IReader^ filereader, ICallback^ callback)
		: ReaderBase(callback)
		, _reader(filereader)
		, _offset(-1)
		, _prev_offset(-1)
		, _size(-1)
		, _eof(false)
		, _chunks(nullptr)
		, _curr_chunk(nullptr)
		, _chunk_buf(nullptr)
		, _chunk_buf_len(0)
	//	, _debug_writer(nullptr)
	{
		_Init();
	}

	CloudsaveReader::~CloudsaveReader()
	{
		Close();

		_reader = nullptr;

		if (_chunks)
			delete _chunks;
		_chunks = nullptr;

		_curr_chunk = nullptr;

		if (_chunk_buf)
			delete[] _chunk_buf;
		_chunk_buf = nullptr;
		_chunk_buf_len = 0;
	}

	CloudsaveReader::!CloudsaveReader()
	{
		//TODO: What to do here?
	}


	// Properties not handled by ReaderBase
	const __int64 CloudsaveReader::Pos::get() { return _curr_chunk->Offset + _offset; }
	const __int64 CloudsaveReader::PrevPos::get() { return _prev_offset; }
	const __int64 CloudsaveReader::Size::get() { return _size; }
	String^ CloudsaveReader::Name::get() { return _reader->Name; }
	String^ CloudsaveReader::Fullname::get() { return _reader->Fullname; }

	void CloudsaveReader::Close()
	{
		_offset = _prev_offset = _size = -1;
		ReaderBase::Close();

	//	//vvvvv TEMP
	//	if (_debug_writer)
	//		_debug_writer->Close();
	//	_debug_writer = nullptr;
	//	//^^^^^ TEMP
	}

	const __int64 CloudsaveReader::Seek(__int64 offset, IReader::Positioning pos)
	{
		if (!_curr_chunk)
			return -1;

		__int64 new_offset;

		switch (pos)
		{
		case IReader::Positioning::Start:
			new_offset = offset;
			break;

		case IReader::Positioning::Relative:
			new_offset = _curr_chunk->Offset + _offset + offset;
			break;

		case IReader::Positioning::End:
			new_offset = _size + offset;
			break;
		}

		if (new_offset < 0 || new_offset >= _size)
			return -1;

		_prev_offset = _curr_chunk->Offset + _offset;

		// Switch chunk if needed
		if (new_offset < _curr_chunk->Offset || new_offset >= _curr_chunk->Offset + _curr_chunk->Size)
			_Prepare(new_offset);

		_offset = new_offset - _curr_chunk->Offset;

		return new_offset;
	}

	// Read N bytes from underlying 'data object', returning no. of bytes read
	int CloudsaveReader::_Read(byte* buff, const int count)
	{
		if (!_curr_chunk)
			return -1;
		if (_eof)
			return 0;

		int read = 0;
		while (read < count)
		{
			// Read min(avail,count), get next chunk, reset
			__int64 remain = _curr_chunk->Size - _offset;
			int copy_amount = (remain >= (count - read)) ? count - read : (int)remain;

			memcpy(buff + read, _chunk_buf + _offset, copy_amount);
			read += copy_amount;
			_offset += copy_amount;

			if (_offset >= _curr_chunk->Size)
			{
				if (_Prepare(_curr_chunk->Offset + _curr_chunk->Size))
					_offset = 0;
			}
		}

		return read;
	}

	void CloudsaveReader::_Init()
	{
		_chunks = new Cloudsave::ChunkList();
		__int64 curr_offset = 0;
		while (_reader->Pos < _reader->Size)
		{
			__int64 last_pos = _reader->Pos;
		
			Cloudsave::ChunkInfo tag;
			tag.Read(_reader);
			if ((unsigned __int32)tag.CompressedSize != Cloudsave::CHUNK_MAGIC)
				throw gcnew Exception(String::Format("CloudsaveReader at pos {0:#,#0}: Invalid magic {1}", 
					last_pos, (unsigned __int32)tag.CompressedSize));
		
			Cloudsave::ChunkInfo summary;
			summary.Read(_reader);
			//Log::Debug("[Pos:{0:X8}]          New chunk with {1:X8} bytes will yield {2:X8} bytes", 
			//	curr_offset, summary.CompressedSize, summary.UncompressedSize);

			while (summary.UncompressedSize > 0)
			{
				__int64 curr_pos = _reader->Pos;
				Cloudsave::ChunkInfo chunk;
				chunk.Read(_reader);

				_chunks->Add(_reader->Pos, chunk.CompressedSize, curr_offset, chunk.UncompressedSize);
				//Log::Debug("[Pos:{0:X8}|Rem:{1:X8}] Chunk with {2:X8} bytes will yield {3:X8} bytes", 
				//	curr_offset, summary.UncompressedSize, chunk.CompressedSize, chunk.UncompressedSize);
				curr_offset += chunk.UncompressedSize;

				_reader->Seek(chunk.CompressedSize, IReader::Positioning::Relative);

				summary.CompressedSize   -= chunk.CompressedSize;
				summary.UncompressedSize -= chunk.UncompressedSize;
			}
			//TODO: Check summary.* for invalid params (e.g. < 0), throw if encountered using last_pos
		}

	//	//vvvvv TEMP
	//	_debug_writer = gcnew Writer::FileWriter(_reader->Fullname+".in.debug", nullptr);
	//	//^^^^^ TEMP

		// Prepare first chunk
		_Prepare(0);

		// First 4 bytes from this first chunk will also contain actual savegame size. 
		// -> This is to be reported by Size.
		_size = (*((unsigned __int32*)_chunk_buf)) + 4;
		_offset = 4;
	}

	bool CloudsaveReader::_Prepare(__int64 offset)
	{
		//String^ s = String::Format("[Pos:{0:X8}] Preparing for offset {1:X8} ... ", _debug_writer->Pos, offset);
		if (offset == _size)
		{
			_eof = true;
			//Log::Debug(s += "EOF");
			return false;
		}

		_curr_chunk = _chunks->FindCovering(offset);
		if (!_curr_chunk)
			throw gcnew ArgumentException("CloudsaveReader: No chunk found");

		if (_chunk_buf_len < _curr_chunk->Size)
		{
			if (_chunk_buf)
				delete[] _chunk_buf;
			_chunk_buf_len = (int)_curr_chunk->Size;
			_chunk_buf = new byte[_chunk_buf_len];
		}

		_chunks->Decompress(_reader, _curr_chunk, _chunk_buf, (int)_curr_chunk->Size);

	//	//vvvvv TEMP
	//	_debug_writer->Write(_chunk_buf, (int)_curr_chunk->Size);
	//	//^^^^^ TEMP

		//Log::Debug(s + "decompressed chunk into {0:X8} bytes", _curr_chunk->Size);

		return true;
	}


};
