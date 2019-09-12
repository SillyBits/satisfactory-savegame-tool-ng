#include "stdafx.h"
#include "Reader.h"
#include "FileReader.h"

namespace Reader 
{

	// Specialized file reader
	FileReader::FileReader(String^ filename, ICallback^ callback)
		: ReaderBase(callback)
		, _filename(filename)
		, _size(-1)
		, _handle(nullptr)
	{
		pin_ptr<const wchar_t> fname = PtrToStringChars(_filename);

		// Shareable by other processes, and optimized for sequential access
		_handle = _wfsopen(fname, L"rbS", _SH_DENYNO);
		if (!_handle)
			throw gcnew FileNotFoundException(_filename);

		if (_fseeki64(_handle, 0, SEEK_END) != 0)
		{
			int err = errno;
			throw gcnew Exception(
				String::Format("Unable to seek to end of file, error {0}", err));
		}
		_size = _ftelli64(_handle);
		if (_size == -1)
		{
			int err = errno;
			throw gcnew Exception(
				String::Format("Unable to retrieve file size, error {0}", err));
		}
		if (_fseeki64(_handle, 0, SEEK_SET) != 0)
		{
			int err = errno;
			throw gcnew Exception(
				String::Format("Unable to seek to beginning of file, error {0}", err));
		}

		_pos = 0;

		//__Start();
	}

	FileReader::~FileReader()
	{
		Close();
	}

	FileReader::!FileReader()
	{
		//TODO: What to do here?
	}

	// Properties not handled by ReaderBase
	const __int64 FileReader::Size::get() { return _size; }
	String^ FileReader::Name::get() { return System::IO::Path::GetFileName(_filename); }
	String^ FileReader::Fullname::get() { return _filename; };

	void FileReader::Close()
	{
		if (_handle)
			fclose(_handle);
		_handle = nullptr;
		_size = -1;
		ReaderBase::Close();
	}

	const __int64 FileReader::Seek(__int64 offset, IReader::Positioning pos)
	{
		if (_fseeki64(_handle, offset, (int)pos) == 0)
		{
			_prev_pos = _pos;
			_pos = _ftelli64(_handle);
			return _pos;
		}

		return -1;
	}

	// Read N bytes from underlying 'data object', returning no. of bytes read
	int FileReader::_Read(byte* buff, const int count)
	{
		_prev_pos = _pos;
		size_t read = fread(buff, 1, count, _handle);
		if (read != count)
			throw gcnew ReadException(this, count);
		_pos += count;
		return count;
	}

};
