#include "Stdafx.h"
#include "Cloudsave.h"


namespace Cloudsave
{
	// Code required for working with new cloud save format
	//


	ChunkInfo::ChunkInfo()
		: ChunkInfo(0, 0)
	{ }

	ChunkInfo::ChunkInfo(__int64 compressed, __int64 uncompressed)
		: CompressedSize(compressed)
		, UncompressedSize(uncompressed)
	{ }

	void ChunkInfo::Read(Reader::IReader^ reader)
	{
		CompressedSize = reader->ReadLong();
		UncompressedSize = reader->ReadLong();
	}

	void ChunkInfo::Write(Writer::IWriter^ writer)
	{
		writer->Write(CompressedSize);
		writer->Write(UncompressedSize);
	}


	ChunkList::Node::Node()
		: _prev(nullptr)
		, _next(nullptr)
	{ }


	ChunkList::ChunkList()
		: _head(nullptr)
		, _tail(nullptr)
		, _count(0)
		, _io_buff(nullptr)
		, _io_buff_len(0)
	{ }

	ChunkList::~ChunkList()
	{
		while (_head)
		{
			Node *next = _head->_next;
			delete _head;
			_head = next;
		}

		_tail = nullptr;
		_count = 0;

		if (_io_buff)
			delete[] _io_buff;
		_io_buff_len = 0;
	}


	const int ChunkList::Count() { return _count; }

	void ChunkList::Add(__int64 file_ofs, __int64 file_size, __int64 dest_ofs, __int64 dest_size)
	{
		if (file_ofs < 0 || file_size <= 0 || file_size > 0x7FFFFFFF ||
			dest_ofs < 0 || dest_size <= 0 || dest_size > 0x7FFFFFFF)
			throw gcnew ArgumentException("ChunkList: Invalid parameter(s)");
		if (_tail && (file_ofs < _tail->_offset || dest_ofs < _tail->Offset))
			throw gcnew ArgumentException("ChunkList: Invalid parameter(s)");

		Node *node = new Node();
		node->Offset = dest_ofs;
		node->Size = dest_size;
		node->_offset = file_ofs;
		node->_size = file_size;
		node->_prev = _tail;
		node->_next = nullptr;

		if (!_head)
			_head = node;
		if (_tail)
			_tail->_next = node;
		_tail = node;
		++_count;
	}

	ChunkList::Node *ChunkList::FindCovering(__int64 offset)
	{
		if (offset < 0)
			throw gcnew ArgumentException("ChunkList: Invalid offset");
		if (_tail && offset >= _tail->Offset + _tail->Size)
			throw gcnew ArgumentException("ChunkList: Invalid offset");

		Node *node = _head;
		while (node)
		{
			if (node->Offset <= offset && offset < node->Offset + node->Size)
				break;
			node = node->_next;
		}

		return node;
	}


	static void *zlibAlloc(void *opaque, unsigned int size, unsigned int num)
	{
		return new byte[size * num];
	}

	static void zlibFree(void *opaque, void *p)
	{
		delete[] p;
	}

	void ChunkList::Decompress(Reader::IReader^ reader, ChunkList::Node *node, byte *pOutBuffer, int nOutBuffer)
	{
		if (!reader || !node || !pOutBuffer || nOutBuffer <= 0)
			throw gcnew ArgumentException("ChunkList: Invalid parameter(s)");

		if (nOutBuffer < (int)node->Size)
			throw gcnew ArgumentException("ChunkList: Buffer too small");

		if (reader->Seek(node->_offset, IReader::Positioning::Start) != node->_offset)
			throw gcnew ReadException(reader, String::Format("ChunkList: Error seeking to node offset {0:#,#0}",
				node->_offset));

		if (_io_buff_len < node->_size)
		{
			if (_io_buff)
				delete[] _io_buff;
			_io_buff_len = (int)node->_size;
			_io_buff = new byte[_io_buff_len];
		}
		if (reader->ReadByte(_io_buff, (int)node->_size) != node->_size)
			throw gcnew ReadException(reader, "ChunkList: Error reading compressed data");

		// Decompress from _io_buff into pOutBuffer
		__int32 retval;
		z_stream z;
		z.zalloc = &zlibAlloc;
		z.zfree = &zlibFree;
		z.opaque = Z_NULL;
		z.next_in = _io_buff;
		z.avail_in = (uInt)(node->_size & 0x7FFFFFFF);
		z.next_out = pOutBuffer;
		z.avail_out = (uInt)(node->Size & 0x7FFFFFFF);

		retval = inflateInit2(&z, /*windowBits=*/15);
		if (retval != Z_OK)
			throw gcnew Exception(String::Format("ChunkList: Error inflateInit2: {0}", retval));

		retval = inflate(&z, Z_FINISH);
		if (retval != Z_STREAM_END)
			throw gcnew Exception(String::Format("ChunkList: Error inflate: {0}", retval));
		__int32 uncompr_result = z.total_out;

		retval = inflateEnd(&z);
		if (retval < Z_OK)
			throw gcnew Exception(String::Format("ChunkList: Error inflateEnd: {0}", retval));

		// Check size returned
		if (node->Size != uncompr_result)
			throw gcnew ArgumentException("ChunkList: Error decompressing chunk");
	}

	int ChunkList::Compress(byte *pInBuffer, int nInBuffer, byte *pOutBuffer, int nOutBuffer)
	{
		if (!pInBuffer || nInBuffer <= 0 || !pOutBuffer || nOutBuffer <= 0)
			throw gcnew ArgumentException("ChunkList: Invalid parameter(s)");

		// Compress from pInBuffer into pOutBuffer
		__int32 retval;
		z_stream z;
		z.zalloc = &zlibAlloc;
		z.zfree = &zlibFree;
		z.opaque = Z_NULL;
		z.next_in = pInBuffer;
		z.avail_in = (uInt)(nInBuffer & 0x7FFFFFFF);
		z.next_out = pOutBuffer;
		z.avail_out = (uInt)(nOutBuffer & 0x7FFFFFFF);

		retval = deflateInit2(&z, Z_DEFAULT_COMPRESSION, Z_DEFLATED, /*windowBits=*/15, MAX_MEM_LEVEL, Z_DEFAULT_STRATEGY);
		if (retval != Z_OK)
			throw gcnew Exception(String::Format("ChunkList: Error deflateInit2: {0}", retval));

		retval = deflate(&z, Z_FINISH);
		if (retval != Z_STREAM_END)
			throw gcnew Exception(String::Format("ChunkList: Error deflate: {0}", retval));
		__int32 compr_result = z.total_out;

		retval = deflateEnd(&z);
		if (retval < Z_OK)
			throw gcnew Exception(String::Format("ChunkList: Error deflateEnd: {0}", retval));

		return compr_result;
	}

};
