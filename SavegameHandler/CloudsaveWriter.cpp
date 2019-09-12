#include "stdafx.h"
#include "CloudsaveWriter.h"


namespace Writer
{

	// Specialized cloud save writer
	CloudsaveWriter::CloudsaveWriter(IWriter^ filewriter, ICallback^ callback)
		: WriterBase(callback)
		, _writer(filewriter)
		, _offset(-1)
		, _write_buf(nullptr)
		, _write_buf_len(0)
		, _chunk_buf(nullptr)
		, _chunk_buf_len(0)
	//	, _debug_writer(nullptr)
	{
		_Init();
	}

	CloudsaveWriter::~CloudsaveWriter()
	{
		Close();

		_writer = nullptr;

		_offset = 0;

		if (_write_buf)
			delete[] _write_buf;
		_write_buf = nullptr;
		_write_buf_len = 0;

		if (_chunk_buf)
			delete[] _chunk_buf;
		_chunk_buf = nullptr;
		_chunk_buf_len = 0;
	}

	CloudsaveWriter::!CloudsaveWriter()
	{
		//TODO: What to do here?
	}


	// Properties not handled by WriterBase
	String^ CloudsaveWriter::Name::get() { return _writer->Name; }
	String^ CloudsaveWriter::Fullname::get() { return _writer->Fullname; }

	void CloudsaveWriter::Close()
	{
		if (_offset > 0)
			_Store();

		WriterBase::Close();

	//	//vvvvv TEMP
	//	if (_debug_writer)
	//		_debug_writer->Close();
	//	_debug_writer = nullptr;
	//	//^^^^^ TEMP
	}

	//const __int64 CloudsaveWriter::Seek(__int64 offset, IWriter::Positioning pos)
	//{
	//	if (_fseeki64(_handle, offset, (int)pos) == 0)
	//	{
	//		_prev_pos = _pos;
	//		_pos = _ftelli64(_handle);
	//		return _pos;
	//	}
	//
	//	return -1;
	//}
	//=> For now, no seeking while writing

	// Write N bytes to underlying 'data object', returning no. of bytes written
	void CloudsaveWriter::__Write(byte* buff, const int count)
	{
		_prev_pos = _pos;

		int written = 0;
		while (written < count)
		{
			// Write min(avail,count), compress, reset
			__int64 remain = _write_buf_len - _offset;
			int copy_amount = (remain > (count - written)) ? count - written : (int)remain;

			memcpy(_write_buf + _offset, buff + written, copy_amount);
			written += copy_amount;
			_offset += copy_amount;

			if (_offset >= _write_buf_len)
			{
				_Store();
				_offset -= _write_buf_len;
			}
		}

		_pos += count;
	}

	void CloudsaveWriter::_Init()
	{
		_pos = _prev_pos = _offset = 0;

		_write_buf_len = 0x20000;
		_write_buf = new byte[_write_buf_len];

		// Hard to guess compression ratio, so we rely on half the size of our write buffer
		_chunk_buf_len = _write_buf_len / 2;
		_chunk_buf = new byte[_chunk_buf_len];

	//	//vvvvv TEMP
	//	_debug_writer = gcnew Writer::FileWriter(_writer->Fullname+".out.debug", nullptr);
	//	//^^^^^ TEMP
	}

	void CloudsaveWriter::_Store()
	{
		int store_amount = (int)_offset; //(_offset == 0) ? _write_buf_len : (int)_offset;

	//	//vvvvv TEMP
	//	_debug_writer->Write(_write_buf, store_amount);
	//	//^^^^^ TEMP

		int compr_size = Cloudsave::ChunkList::Compress(_write_buf, store_amount, _chunk_buf, _chunk_buf_len);

		Cloudsave::ChunkInfo tag(Cloudsave::CHUNK_MAGIC, store_amount);
		tag.Write(_writer);

		tag.CompressedSize = compr_size;
		tag.Write(_writer);
		tag.Write(_writer);

		_writer->Write(_chunk_buf, compr_size);

		//Log::Debug("[Pos:{0:X8}] Received a {1:X8} bytes ... compressed down to {2:X8}", 
		//	_debug_writer->Pos - store_amount, _offset, compr_size);
	}

};
