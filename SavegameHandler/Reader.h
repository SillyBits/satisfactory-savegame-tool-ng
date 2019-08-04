#pragma once


namespace Reader
{

	public interface class IReader
	{
	public:
		// Properties
		property const long Pos { const long get() abstract; };
		property const long PrevPos { const long get() abstract; };
		property const long Size { const long get() abstract; };
		property String^ Name { String^ get() abstract; };


		// Methods
		void Close();

		const bool ReadBool();
		const int ReadBool(bool* buff, const int count);

		const byte ReadByte();
		const int ReadByte(byte* buff, const int count);

		const __int16 ReadShort();
		const int ReadShort(__int16* buff, const int count);

		const __int32 ReadInt();
		const int ReadInt(__int32* buff, const int count);

		const __int64 ReadLong();
		const int ReadLong(__int64* buff, const int count);

		const float ReadFloat();
		const int ReadFloat(float* buff, const int count);

		const double ReadDouble();
		const int ReadDouble(double* buff, const int count);

		//def readStruct(self, s:struct.Struct): return self.__read(s)
		//=> ReadByte(byte*,int)

		str^ ReadString();
		str^ ReadString(const int length);

	};


	public ref class ReadException : Exception
	{
	public:
		ReadException(IReader^ reader, int count)
			: Exception(String::Format("Reader({0}|{1}): Failed to read {2} bytes", 
				reader->Name, reader->PrevPos, count))
		{ }

		ReadException(IReader^ reader, String^ msg)
			: Exception(String::Format("Reader({0}|{1}): %s",
				reader->Name, reader->PrevPos, msg))
		{ }

	};


	// Code common for all readers
	public ref class ReaderBase abstract : IReader
	{
	public:
		ReaderBase(ICallback^ callback)
			: _callback(callback)
			, _pos(-1)
			, _prev_pos(-1)
		{
			//__Start();
		}

		// Properties
		virtual property const long Pos { const long get() { return _pos; } };
		virtual property const long PrevPos { const long get() { return _prev_pos; } };
		virtual property const long Size { const long get() abstract; };
		virtual property String^ Name { String^ get() abstract; };

		// Enums
		enum class Positioning { Start=SEEK_SET, Relative=SEEK_CUR, End=SEEK_END };

		// Methods
		virtual void Close() { _pos = _prev_pos = -1; }

		virtual const long Seek(long offset, Positioning pos) abstract;

		virtual const bool ReadBool() { return _Read<bool>(); }
		virtual const int ReadBool(bool* buff, const int count) { return _Read(buff, count); }

		virtual const byte ReadByte() { return _Read<byte>(); }
		virtual const int ReadByte(byte* buff, const int count) { return _Read(buff, count); }

		virtual const __int16 ReadShort() { return _Read<__int16>(); }
		virtual const int ReadShort(__int16* buff, const int count) { return _Read(buff, count); }

		virtual const __int32 ReadInt() { return _Read<__int32>(); }
		virtual const int ReadInt(__int32* buff, const int count) { return _Read(buff, count); }

		virtual const __int64 ReadLong() { return _Read<__int64>(); }
		virtual const int ReadLong(__int64* buff, const int count) { return _Read(buff, count); }

		virtual const float ReadFloat() { return _Read<float>(); }
		virtual const int ReadFloat(float* buff, const int count) { return _Read(buff, count); }

		virtual const double ReadDouble() { return _Read<double>(); }
		virtual const int ReadDouble(double* buff, const int count) { return _Read(buff, count); }

		// Reads a string, if length=0 it's assumed to be prefixed with its length.
		virtual str^ ReadString() { return ReadString(0); }
		virtual str^ ReadString(const int length)
		{
			long last = _prev_pos = _pos;

			int len = length ? length : ReadInt();

			if (len == 0)
				return nullptr;

			if (len < 0)
			{
				// Unicode string
				len = -len;

				str^ s = gcnew str(len, str::WCHAR);
				wchar_t *p = s->Wchar();

				ReadByte((byte*)p, len*2);
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
			if (p[len-1] != 0)
			{
				_prev_pos = last;
				throw gcnew ReadException(this, "Null-Terminator expected");
			}

			return s;
		}


	protected:
		ICallback^ _callback;
		long _pos;
		long _prev_pos;


		virtual void __Start() { _callback->Start(Size); }
		virtual void __Update() { _callback->Update(Pos); }
		virtual void __Stop() { _callback->Stop(); }


		// Read one value of type 'Type'
		template<typename Type>
		Type _Read()
		{
			Type val;
			_Read((byte*)&val, sizeof(Type));
			return val;
		}

		// Read N values of type 'Type', returning no. of items read
		template<typename Type>
		int _Read(Type* buff, const int count)
		{
			int read = _Read((byte*)buff, count * sizeof(Type));
			return read / sizeof(Type);
		}

		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) abstract;

	};


	// Specialized file reader
	public ref class FileReader : ReaderBase
	{
	public:
		FileReader(String^ filename, ICallback^ callback)
			: ReaderBase(callback)
			, _filename(filename)
			, _size(-1)
			, _handle(nullptr)
		{
			pin_ptr<const wchar_t> fname = PtrToStringChars(_filename);

			_handle = _wfopen(fname, L"rb");
			if (!_handle)
				throw gcnew FileNotFoundException(_filename);

			fseek(_handle, 0, SEEK_END);
			_size = ftell(_handle);
			fseek(_handle, 0, SEEK_SET);

			_pos = 0;

			//__Start();
		}

		virtual ~FileReader()
		{
			Close();
		}

		!FileReader()
		{
			//TODO: What to do here?
		}


		// Properties not handled by ReaderBase
		virtual property const long Size { const long get() override { return _size; } };
		virtual property String^ Name { String^ get() override { return System::IO::Path::GetFileName(_filename); } };

		virtual void Close() override
		{
			if (_handle)
				fclose(_handle);
			_handle = nullptr;
			_size = -1;
			ReaderBase::Close();
		}

		virtual const long Seek(long offset, Positioning pos) override
		{
			if (fseek(_handle, offset, (int)pos) == 0)
			{
				_prev_pos = _pos;
				_pos = ftell(_handle);
				return _pos;
			}

			return -1;
		}

	protected:
		String^ _filename;
		long _size;
		FILE *_handle;


		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) override
		{
			_prev_pos = _pos;
			size_t read = fread(buff, 1, count, _handle);
			if (read != count)
				throw gcnew ReadException(this, count);
			_pos += count;
			return count;
		}

	};


	// Specialized memory reader
	public ref class MemoryReader : ReaderBase
	{
	public:
		MemoryReader(byte* buff, const int length, ICallback^ callback)
			: ReaderBase(callback)
			, _buff(buff)
			, _size(length)
		{
			_pos = 0;

			//__Start();
		}


		// Properties not handled by ReaderBase
		virtual property const long Size { const long get() override { return _size; } };
		virtual property String^ Name { String^ get() override { return ToString(); } };

		virtual void Close() override
		{
			_buff = nullptr;
			_size = -1;
			ReaderBase::Close();
		}

		virtual const long Seek(long offset, Positioning pos) override
		{
			switch (pos)
			{
			case Positioning::Start:
				if (0 <= offset && offset <= Size)
				{
					_prev_pos = _pos;
					_pos = offset;
					return _pos;
				}
				break;

			case Positioning::Relative:
				long new_pos = _pos + offset;
				if (0 <= new_pos && new_pos <= Size)
				{
					_prev_pos = _pos;
					_pos = new_pos;
					return _pos;
				}
				break;

			case Positioning::End:
				long new_pos = Size + offset;
				if (0 <= new_pos && new_pos <= Size)
				{
					_prev_pos = _pos;
					_pos = new_pos;
					return _pos;
				}
				break;
			}

			return -1;
		}


	protected:
		byte* _buff;
		long _size;


		// Read N bytes from underlying 'data object', returning no. of bytes read
		virtual int _Read(byte* buff, const int count) override
		{
			_prev_pos = _pos;
			if (_pos + count > _size)
				throw gcnew ReadException(this, count);
			memcpy(buff, _buff + _pos, count);
			_pos += count;
			return count;
		}

	};


};

