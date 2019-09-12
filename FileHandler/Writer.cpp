#include "Stdafx.h"
#include "Writer.h"

namespace Writer
{

	WriteException::WriteException(IWriter^ writer, int count)
		: Exception(String::Format("Writer({0}|{1}): Failed to write {2} bytes",
			writer->Name, writer->PrevPos, count))
	{ }

	WriteException::WriteException(IWriter^ writer, String^ msg)
		: Exception(String::Format("Writer({0}|{1}): %s",
			writer->Name, writer->PrevPos, msg))
	{ }


	// Code common for all writers
	//

	WriterBase::WriterBase(ICallback^ callback)
		: _callback(callback)
		, _pos(-1)
		, _prev_pos(-1)
	{ }

	// Properties
	const __int64 WriterBase::Pos::get() { return _pos; }
	const __int64 WriterBase::PrevPos::get() { return _prev_pos; }

	// Methods
	void WriterBase::Close() { _pos = _prev_pos = -1; }

	//const __int64 WriterBase::Seek(__int64 offset, IWriter::Positioning pos) abstract;
	//=> For now, no seeking while writing

	void WriterBase::Write(bool val) { _Write(val); }
	void WriterBase::Write(bool* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(byte val) { _Write(val); }
	void WriterBase::Write(byte* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(__int16 val) { _Write(val); }
	void WriterBase::Write(__int16* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(unsigned __int16 val) { _Write(val); }
	void WriterBase::Write(unsigned __int16* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(__int32 val) { _Write(val); }
	void WriterBase::Write(__int32* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(unsigned __int32 val) { _Write(val); }
	void WriterBase::Write(unsigned __int32* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(__int64 val) { _Write(val); }
	void WriterBase::Write(__int64* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(unsigned __int64 val) { _Write(val); }
	void WriterBase::Write(unsigned __int64* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(float val) { _Write(val); }
	void WriterBase::Write(float* buff, const int count) { _Write(buff, count); }

	void WriterBase::Write(double val) { _Write(val); }
	void WriterBase::Write(double* buff, const int count) { _Write(buff, count); }

	// Writes a string, if length=0 it's assumed to be prefixed with its length.
	void WriterBase::Write(str^ val) { Write(val, 0); }
	void WriterBase::Write(str^ val, const int length)
	{
		if (str::IsNull(val))
			throw gcnew ArgumentNullException();

		__int64 last = _prev_pos = _pos;

		int len = length ? length : val->GetRawLength();
		if (val->Wchar() != nullptr)
		{
			// Unicode string
			// (length returned by GetRawLength already accounts for double-char)
			Write(-len / 2);
			Write((byte*)val->Wchar(), len);
		}
		else if (len > 0)
		{
			// ASCII string
			Write(len);
			Write((byte*)val->Ascii(), len);
		}
	}

	void WriterBase::Write(ByteArray^ vals) { _Write(vals); }

	void WriterBase::Write(array<__int16>^ vals) { _Write(vals); }
	void WriterBase::Write(array<unsigned __int16>^ vals) { _Write(vals); }

	void WriterBase::Write(array<__int32>^ vals) { _Write(vals); }
	void WriterBase::Write(array<unsigned __int32>^ vals) { _Write(vals); }

	void WriterBase::Write(array<__int64>^ vals) { _Write(vals); }
	void WriterBase::Write(array<unsigned __int64>^ vals) { _Write(vals); }

	void WriterBase::__Start()
	{
#pragma warning(disable : 4965)
		_callback->Start(__int64(0));
#pragma warning(default : 4965)
	}
	void WriterBase::__Update() { _callback->Update(Pos); }
	void WriterBase::__Stop() { _callback->Stop(); }

};

