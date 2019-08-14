#pragma once


namespace Writer
{

	public interface class IWriter
	{
	public:
		// Properties
		property const __int64 Pos { const __int64 get() abstract; };
		property const __int64 PrevPos { const __int64 get() abstract; };
		property String^ Name { String^ get() abstract; };


		//// Enums
		//enum class Positioning { Start=SEEK_SET, Relative=SEEK_CUR, End=SEEK_END };
		//=> For now, no seeking while writing


		// Methods
		void Close();

		//const __int64 Seek(__int64 offset, Positioning pos);
		//=> For now, no seeking while writing

		void Write(bool val);
		void Write(bool* buff, const int count);

		void Write(byte val);
		void Write(byte* buff, const int count);

		void Write(__int16 val);
		void Write(__int16* buff, const int count);

		void Write(unsigned __int16 val);
		void Write(unsigned __int16* buff, const int count);

		void Write(__int32 val);
		void Write(__int32* buff, const int count);

		void Write(unsigned __int32 val);
		void Write(unsigned __int32* buff, const int count);

		void Write(__int64 val);
		void Write(__int64* buff, const int count);

		void Write(unsigned __int64 val);
		void Write(unsigned __int64* buff, const int count);

		void Write(float val);
		void Write(float* buff, const int count);

		void Write(double val);
		void Write(double* buff, const int count);

		void Write(str^ val);
		void Write(str^ val, const int length);

		void Write(ByteArray^ vals);

		void Write(array<__int16>^ vals);
		void Write(array<unsigned __int16>^ vals);

		void Write(array<__int32>^ vals);
		void Write(array<unsigned __int32>^ vals);

		void Write(array<__int64>^ vals);
		void Write(array<unsigned __int64>^ vals);

	};


	public ref class WriteException : Exception
	{
	public:
		WriteException(IWriter^ writer, int count)
			: Exception(String::Format("Writer({0}|{1}): Failed to write {2} bytes", 
				writer->Name, writer->PrevPos, count))
		{ }

		WriteException(IWriter^ writer, String^ msg)
			: Exception(String::Format("Writer({0}|{1}): %s",
				writer->Name, writer->PrevPos, msg))
		{ }

	};


	// Code common for all writers
	public ref class WriterBase abstract : IWriter
	{
	public:
		WriterBase(ICallback^ callback)
			: _callback(callback)
			, _pos(-1)
			, _prev_pos(-1)
		{
			//__Start();
		}

		// Properties
		virtual property const __int64 Pos { const __int64 get() { return _pos; } };
		virtual property const __int64 PrevPos { const __int64 get() { return _prev_pos; } };
		virtual property String^ Name { String^ get() abstract; };

		// Methods
		virtual void Close() { _pos = _prev_pos = -1; }

		//virtual const __int64 Seek(__int64 offset, IWriteer::Positioning pos) abstract;
		//=> For now, no seeking while writing

		virtual void Write(bool val) { _Write(val); }
		virtual void Write(bool* buff, const int count) { _Write(buff, count); }

		virtual void Write(byte val) { _Write(val); }
		virtual void Write(byte* buff, const int count) { _Write(buff, count); }

		virtual void Write(__int16 val) { _Write(val); }
		virtual void Write(__int16* buff, const int count) { _Write(buff, count); }

		virtual void Write(unsigned __int16 val) { _Write(val); }
		virtual void Write(unsigned __int16* buff, const int count) { _Write(buff, count); }

		virtual void Write(__int32 val) { _Write(val); }
		virtual void Write(__int32* buff, const int count) { _Write(buff, count); }

		virtual void Write(unsigned __int32 val) { _Write(val); }
		virtual void Write(unsigned __int32* buff, const int count) { _Write(buff, count); }

		virtual void Write(__int64 val) { _Write(val); }
		virtual void Write(__int64* buff, const int count) { _Write(buff, count); }

		virtual void Write(unsigned __int64 val) { _Write(val); }
		virtual void Write(unsigned __int64* buff, const int count) { _Write(buff, count); }

		virtual void Write(float val) { _Write(val); }
		virtual void Write(float* buff, const int count) { _Write(buff, count); }

		virtual void Write(double val) { _Write(val); }
		virtual void Write(double* buff, const int count) { _Write(buff, count); }

		// Writes a string, if length=0 it's assumed to be prefixed with its length.
		virtual void Write(str^ val) { Write(val, 0); }
		virtual void Write(str^ val, const int length)
		{
			if (str::IsNull(val))
				throw gcnew ArgumentNullException();

			__int64 last = _prev_pos = _pos;

			int len = length ? length : val->GetRawLength();
			if (val->Wchar() != nullptr)
				len = -len;
			Write(len);

			if (len < 0)
			{
				// Unicode string
				// (length returned by GetRawLength already accounts for double-char)
				Write((byte*)val->Wchar(), -len);
			}
			else if (len > 0)
			{
				// ASCII string
				Write((byte*)val->Ascii(), len);
			}
		}

		virtual void Write(ByteArray^ vals) { _Write(vals); }

		virtual void Write(array<__int16>^ vals) { _Write(vals); }
		virtual void Write(array<unsigned __int16>^ vals) { _Write(vals); }

		virtual void Write(array<__int32>^ vals) { _Write(vals); }
		virtual void Write(array<unsigned __int32>^ vals) { _Write(vals); }

		virtual void Write(array<__int64>^ vals) { _Write(vals); }
		virtual void Write(array<unsigned __int64>^ vals) { _Write(vals); }


	protected:
		ICallback^ _callback;
		__int64 _pos;
		__int64 _prev_pos;


		virtual void __Start() 
		{ 
			#pragma warning(disable : 4965)
			_callback->Start(__int64(0)); 
			#pragma warning(default : 4965)
		}
		virtual void __Update() { _callback->Update(Pos); }
		virtual void __Stop() { _callback->Stop(); }

		// Write one value of type 'Type'
		template<typename Type>
		void _Write(const Type& val)
		{
			__Write((byte*)&val, sizeof(Type));
		}

		// Write N values of type 'Type'
		template<typename Type>
		void _Write(Type* buff, const int count)
		{
			__Write((byte*)buff, count * sizeof(Type));
		}

		// Write N values of type 'Type' given as managed array
		template<typename Type>
		void _Write(array<Type>^ vals)
		{
			pin_ptr<Type> p = &vals[0];
			__Write((byte*)p, vals->Length * sizeof(Type));
		}

		// Write N bytes from underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) abstract;

	};


	// Specialized file writer
	public ref class FileWriter : WriterBase
	{
	public:
		FileWriter(String^ filename, ICallback^ callback)
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

		virtual ~FileWriter()
		{
			Close();
		}

		!FileWriter()
		{
			//TODO: What to do here?
		}


		// Properties not handled by WriterBase
		virtual property String^ Name { String^ get() override { return System::IO::Path::GetFileName(_filename); } };

		virtual void Close() override
		{
			if (_handle)
			{
				fflush(_handle);
				fclose(_handle);
			}
			_handle = nullptr;
			WriterBase::Close();
		}

		//virtual const __int64 Seek(__int64 offset, IWriter::Positioning pos) override
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


	protected:
		String^ _filename;
		FILE *_handle;


		// Write N bytes to underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) override
		{
			_prev_pos = _pos;
			size_t written = fwrite(buff, 1, count, _handle);
			if (written != count)
				throw gcnew WriteException(this, count);
			_pos += count;
		}

	};


	// Specialized memory writer
	public ref class MemoryWriter : WriterBase
	{
	public:
		MemoryWriter(byte* buff, const int length, ICallback^ callback)
			: WriterBase(callback)
			, _buff(buff)
			, _size(length)
		{
			_pos = 0;

			//__Start();
		}


		// Properties not handled by WriteerBase
		virtual property String^ Name { String^ get() override { return ToString(); } };

		virtual void Close() override
		{
			_buff = nullptr;
			_size = -1;
			WriterBase::Close();
		}

		//virtual const __int64 Seek(__int64 offset, IWriter::Positioning pos) override
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


	protected:
		byte* _buff;
		__int64 _size;


		// Write N bytes to underlying 'data object', returning no. of bytes written
		virtual void __Write(byte* buff, const int count) override
		{
			_prev_pos = _pos;
			if (_pos + count > _size)
				throw gcnew WriteException(this, count);
			memcpy(_buff + _pos, buff, count);
			_pos += count;
		}

	};


};

