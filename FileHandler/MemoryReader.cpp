#include "stdafx.h"
#include "Reader.h"
#include "MemoryReader.h"

namespace Reader
{

	// Specialized memory reader
	MemoryReader::MemoryReader(byte* buff, const int length, ICallback^ callback)
		: ReaderBase(callback)
		, _buff(buff)
		, _size(length)
		, _owned(false)
	{
		_pos = 0;

		//__Start();
	}

	MemoryReader::MemoryReader(array<byte>^ buff, ICallback^ callback)
		: ReaderBase(callback)
	{
		_pos = 0;

		_size = buff->Length;

		_buff = new byte[_size];
		_owned = true;

		pin_ptr<byte> pinned = &buff[0];
		memcpy(_buff, pinned, _size);

		//__Start();
	}

	// Properties not handled by ReaderBase
	const __int64 MemoryReader::Size::get() { return _size; }
	String^ MemoryReader::Name::get() { return ToString(); }
	String^ MemoryReader::Fullname::get() { return Name; };

	void MemoryReader::Close()
	{
		if (_owned && _buff)
			delete[] _buff;
		_owned = false;
		_buff = nullptr;
		_size = -1;
		ReaderBase::Close();
	}

	const __int64 MemoryReader::Seek(__int64 offset, IReader::Positioning pos)
	{
		__int64 new_pos;

		switch (pos)
		{
		case IReader::Positioning::Start:
			new_pos = offset;
			break;

		case IReader::Positioning::Relative:
			new_pos = _pos + offset;
			break;

		case IReader::Positioning::End:
			new_pos = Size + offset;
			break;
		}

		if (0 <= new_pos && new_pos <= Size)
		{
			_prev_pos = _pos;
			_pos = new_pos;
			return _pos;
		}
		return -1;
	}

	// Read N bytes from underlying 'data object', returning no. of bytes read
	int MemoryReader::_Read(byte* buff, const int count)
	{
		_prev_pos = _pos;
		if (_pos + count > _size)
			throw gcnew ReadException(this, count);
		memcpy(buff, _buff + _pos, count);
		_pos += count;
		return count;
	}

};
