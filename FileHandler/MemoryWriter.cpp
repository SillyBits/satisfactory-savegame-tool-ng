#include "Stdafx.h"
#include "Writer.h"
#include "MemoryWriter.h"

namespace Writer
{

	// Specialized memory writer
	MemoryWriter::MemoryWriter(byte* buff, const int length, ICallback^ callback)
		: WriterBase(callback)
		, _buff(buff)
		, _size(length)
	{
		_pos = 0;

		//__Start();
	}

	// Properties not handled by WriterBase
	String^ MemoryWriter::Name::get() { return ToString(); }
	String^ MemoryWriter::Fullname::get() { return Name; };

	void MemoryWriter::Close()
	{
		_buff = nullptr;
		_size = -1;
		WriterBase::Close();
	}

	//const __int64 MemoryWriter::Seek(__int64 offset, IWriter::Positioning pos)
	//{
	//	__int64 new_pos;
	//
	//	switch (pos)
	//	{
	//	case IWriteer::Positioning::Start:
	//		new_pos = offset;
	//		break;
	//
	//	case IWriteer::Positioning::Relative:
	//		new_pos = _pos + offset;
	//		break;
	//
	//	case IWriteer::Positioning::End:
	//		new_pos = Size + offset;
	//		break;
	//	}
	//
	//	if (0 <= new_pos && new_pos <= Size)
	//	{
	//		_prev_pos = _pos;
	//		_pos = new_pos;
	//		return _pos;
	//	}
	//	return -1;
	//}
	//=> For now, no seeking while writing

	// Write N bytes to underlying 'data object', returning no. of bytes written
	void MemoryWriter::__Write(byte* buff, const int count)
	{
		_prev_pos = _pos;
		if (_pos + count > _size)
			throw gcnew WriteException(this, count);
		memcpy(_buff + _pos, buff, count);
		_pos += count;
	}

};
