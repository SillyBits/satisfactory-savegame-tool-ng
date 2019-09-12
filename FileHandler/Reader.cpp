#include "stdafx.h"
#include "Reader.h"

namespace Reader
{

	ReadException::ReadException(IReader^ reader, int count)
		: Exception(String::Format("Reader({0}|{1}): Failed to read {2} bytes",
			reader->Name, reader->PrevPos, count))
	{ }

	ReadException::ReadException(IReader^ reader, String^ msg)
		: Exception(String::Format("Reader({0}|{1}): %s",
			reader->Name, reader->PrevPos, msg))
	{ }


	// Code common for all readers
	//

	ReaderBase::ReaderBase(ICallback^ callback)
		: _callback(callback)
		, _pos(-1)
		, _prev_pos(-1)
	{ }

	// Properties
	const __int64 ReaderBase::Pos::get() { return _pos; }
	const __int64 ReaderBase::PrevPos::get() { return _prev_pos; }

	// Methods
	void ReaderBase::Close() { _pos = _prev_pos = -1; }

	const bool ReaderBase::ReadBool() { return _Read<bool>(); }
	const int ReaderBase::ReadBool(bool* buff, const int count) { return _Read(buff, count); }

	const byte ReaderBase::ReadByte() { return _Read<byte>(); }
	const int ReaderBase::ReadByte(byte* buff, const int count) { return _Read(buff, count); }

	const __int16 ReaderBase::ReadShort() { return _Read<__int16>(); }
	const int ReaderBase::ReadShort(__int16* buff, const int count) { return _Read(buff, count); }

	const unsigned __int16 ReaderBase::ReadUShort() { return _Read<unsigned __int16>(); }
	const int ReaderBase::ReadUShort(unsigned __int16* buff, const int count) { return _Read(buff, count); }

	const __int32 ReaderBase::ReadInt() { return _Read<__int32>(); }
	const int ReaderBase::ReadInt(__int32* buff, const int count) { return _Read(buff, count); }

	const unsigned __int32 ReaderBase::ReadUInt() { return _Read<unsigned __int32>(); }
	const int ReaderBase::ReadUInt(unsigned __int32* buff, const int count) { return _Read(buff, count); }

	const __int64 ReaderBase::ReadLong() { return _Read<__int64>(); }
	const int ReaderBase::ReadLong(__int64* buff, const int count) { return _Read(buff, count); }

	const unsigned __int64 ReaderBase::ReadULong() { return _Read<unsigned __int64>(); }
	const int ReaderBase::ReadULong(unsigned __int64* buff, const int count) { return _Read(buff, count); }

	const float ReaderBase::ReadFloat() { return _Read<float>(); }
	const int ReaderBase::ReadFloat(float* buff, const int count) { return _Read(buff, count); }

	const double ReaderBase::ReadDouble() { return _Read<double>(); }
	const int ReaderBase::ReadDouble(double* buff, const int count) { return _Read(buff, count); }

	// Reads a string, if length=0 it's assumed to be prefixed with its length.
	str^ ReaderBase::ReadString() { return ReadString(0); }
	str^ ReaderBase::ReadString(const int length)
	{
		__int64 last = _prev_pos = _pos;

		int len = length ? length : ReadInt();

		if (len == 0)
			return gcnew str(true);

		if (len < 0)
		{
			// Unicode string
			len = -len;

			str^ s = gcnew str(len, str::WCHAR);
			wchar_t *p = s->Wchar();

			ReadByte((byte*)p, len * 2);
			if (p[len - 1] != 0)
			{
				_prev_pos = last;
				throw gcnew ReadException(this, "Null-Terminator expected");
			}

			return s;
		}

		// ASCII string
		str^ s = gcnew str(len, str::ASCII);
		char *p = s->Ascii();

		ReadByte((byte*)p, len);
		if (p[len - 1] != 0)
		{
			_prev_pos = last;
			throw gcnew ReadException(this, "Null-Terminator expected");
		}

		return s;
	}

	ByteArray^ ReaderBase::ReadBytes(int count) { return _Read<byte>(count); }

	array<__int16>^ ReaderBase::ReadShorts(int count) { return _Read<__int16>(count); }
	array<unsigned __int16>^ ReaderBase::ReadUShorts(int count) { return _Read<unsigned __int16>(count); }

	array<__int32>^ ReaderBase::ReadInts(int count) { return _Read<__int32>(count); }
	array<unsigned __int32>^ ReaderBase::ReadUInts(int count) { return _Read<unsigned __int32>(count); }

	array<__int64>^ ReaderBase::ReadLongs(int count) { return _Read<__int64>(count); }
	array<unsigned __int64>^ ReaderBase::ReadULongs(int count) { return _Read<unsigned __int64>(count); }
	
	void ReaderBase::__Start() { _callback->Start(Size); }
	void ReaderBase::__Update() { _callback->Update(Pos); }
	void ReaderBase::__Stop() { _callback->Stop(); }

};