#pragma once


namespace Cloudsave
{
	// Code required for working with new cloud save format
	//


	// zlib memory handlers
	void *zlibAlloc(void *opaque, unsigned int size, unsigned int num);
	void zlibFree(void *opaque, void *p);


	const unsigned __int32 CHUNK_MAGIC = 0x9E2A83C1;


	private class ChunkInfo
	{
	public:
		ChunkInfo();
		ChunkInfo(__int64 compressed, __int64 uncompressed);

		void Read(Reader::IReader^ reader);
		void Write(Writer::IWriter^ writer);
	
		__int64 CompressedSize;
		__int64 UncompressedSize;
	};


	private class ChunkList
	{
	public:

		class Node
		{
		public:
			__int64 Offset;// uncompressed synthetic!
			__int64 Size;  // uncompressed synthetic!

		private:
			friend class ChunkList;

			Node();

			__int64  _offset;// compressed file-wise!
			__int64  _size;  // compressed file-wise!
			Node    *_prev;
			Node    *_next;
		};

		ChunkList();
		virtual ~ChunkList();

		const int Count();
		void Add(__int64 file_ofs, __int64 file_size, __int64 dest_ofs, __int64 dest_size);
		Node *FindCovering(__int64 offset);

		void Decompress(Reader::IReader^ reader, Node *node, byte *pOutBuffer, int nOutBuffer);
		static int Compress(byte *pInBuffer, int nInBuffer, byte *pOutBuffer, int nOutBuffer);

	private:
		Node *_head;
		Node *_tail;
		int   _count;
		byte *_io_buff;
		int   _io_buff_len;

	};

};
