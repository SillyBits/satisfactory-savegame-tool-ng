#include "stdafx.h"
#include "Writer.h"
#include "FileWriter.h"

namespace Writer
{

	// Specialized file writer
	FileWriter::FileWriter(String^ filename, ICallback^ callback)
		: WriterBase(callback)
		, _filename(filename)
		, _handle(nullptr)
	{
		pin_ptr<const wchar_t> fname = PtrToStringChars(_filename);

		_handle = _wfopen(fname, L"wb");
		if (!_handle)
			throw gcnew IOException(_filename);

		_pos = 0;

		//__Start();
	}

	FileWriter::~FileWriter()
	{
		Close();
	}

	FileWriter::!FileWriter()
	{
		//TODO: What to do here?
	}

	// Properties not handled by WriterBase
	String^ FileWriter::Name::get() { return System::IO::Path::GetFileName(_filename); }
	String^ FileWriter::Fullname::get() { return _filename; };

	void FileWriter::Close()
	{
		if (_handle)
		{
			fflush(_handle);
			fclose(_handle);
		}
		_handle = nullptr;
		WriterBase::Close();
	}

	//const __int64 FileWriter::Seek(__int64 offset, IWriter::Positioning pos)
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
	void FileWriter::__Write(byte* buff, const int count)
	{
		_prev_pos = _pos;
		size_t written = fwrite(buff, 1, count, _handle);
		if (written != count)
			throw gcnew WriteException(this, count);
		_pos += count;
	}

};
