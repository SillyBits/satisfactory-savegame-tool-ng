#pragma once

using namespace FileHandler;

namespace Reader
{

	// Reader interface
	public interface class IReader
	{
	public:
		// Properties
		property const __int64 Pos { const __int64 get() abstract; };
		property const __int64 PrevPos { const __int64 get() abstract; void set(const __int64) abstract; };
		property const __int64 Size { const __int64 get() abstract; };
		property String^ Name { String^ get() abstract; };
		property String^ Fullname { String^ get() abstract; };

		// Enums
		enum class Positioning { Start=SEEK_SET, Relative=SEEK_CUR, End=SEEK_END };

		// Methods
		void Close();

		const __int64 Seek(__int64 offset, Positioning pos);

		const bool ReadBool();
		const int ReadBool(bool* buff, const int count);

		const byte ReadByte();
		const int ReadByte(byte* buff, const int count);

		const __int16 ReadShort();
		const int ReadShort(__int16* buff, const int count);

		const unsigned __int16 ReadUShort();
		const int ReadUShort(unsigned __int16* buff, const int count);

		const __int32 ReadInt();
		const int ReadInt(__int32* buff, const int count);

		const unsigned __int32 ReadUInt();
		const int ReadUInt(unsigned __int32* buff, const int count);

		const __int64 ReadLong();
		const int ReadLong(__int64* buff, const int count);

		const unsigned __int64 ReadULong();
		const int ReadULong(unsigned __int64* buff, const int count);

		const float ReadFloat();
		const int ReadFloat(float* buff, const int count);

		const double ReadDouble();
		const int ReadDouble(double* buff, const int count);

		str^ ReadString();
		str^ ReadString(const int length);

		ByteArray^ ReadBytes(int count);

		array<__int16>^ ReadShorts(int count);
		array<unsigned __int16>^ ReadUShorts(int count);

		array<__int32>^ ReadInts(int count);
		array<unsigned __int32>^ ReadUInts( int count);

		array<__int64>^ ReadLongs(int count);
		array<unsigned __int64>^ ReadULongs(int count);

	};


	public ref class ReadException : Exception
	{
	public:
		ReadException(IReader^ reader, int count);
		ReadException(IReader^ reader, String^ msg);

		property IReader^ Reader  { IReader^ get() { return _Reader; } };
		property String^  Name    { String^ get() { return _Name; } };
		property __int64  PrevPos { __int64 get() { return _PrevPos; } };
		property __int64  Pos     { __int64 get() { return _Pos; } };

	private:
		IReader^ _Reader;
		String^  _Name;
		__int64  _PrevPos;
		__int64  _Pos;
	};


	// Code common for all readers
	public ref class ReaderBase abstract : IReader
	{
	public:
		ReaderBase(ICallback^ callback);

		// Properties
		virtual property const __int64 Pos { const __int64 get(); };
		virtual property const __int64 PrevPos { const __int64 get(); void set(const __int64); };
		virtual property const __int64 Size { const __int64 get() abstract; };
		virtual property String^ Name { String^ get() abstract; };
		virtual property String^ Fullname { String^ get() abstract; };

		// Methods
		virtual void Close();

		virtual const __int64 Seek(__int64 offset, IReader::Positioning pos) abstract;

		virtual const bool ReadBool();
		virtual const int ReadBool(bool* buff, const int count);

		virtual const byte ReadByte();
		virtual const int ReadByte(byte* buff, const int count);

		virtual const __int16 ReadShort();
		virtual const int ReadShort(__int16* buff, const int count);

		virtual const unsigned __int16 ReadUShort();
		virtual const int ReadUShort(unsigned __int16* buff, const int count);

		virtual const __int32 ReadInt();
		virtual const int ReadInt(__int32* buff, const int count);

		virtual const unsigned __int32 ReadUInt();
		virtual const int ReadUInt(unsigned __int32* buff, const int count);

		virtual const __int64 ReadLong();
		virtual const int ReadLong(__int64* buff, const int count);

		virtual const unsigned __int64 ReadULong();
		virtual const int ReadULong(unsigned __int64* buff, const int count);

		virtual const float ReadFloat();
		virtual const int ReadFloat(float* buff, const int count);

		virtual const double ReadDouble();
		virtual const int ReadDouble(double* buff, const int count);

		// Reads a string, if length=0 it's assumed to be prefixed with its length.
		virtual str^ ReadString();
		virtual str^ ReadString(const int length);

		virtual ByteArray^ ReadBytes(int count);

		virtual array<__int16>^ ReadShorts(int count);
		virtual array<unsigned __int16>^ ReadUShorts(int count);

		virtual array<__int32>^ ReadInts(int count);
		virtual array<unsigned __int32>^ ReadUInts(int count);

		virtual array<__int64>^ ReadLongs(int count);
		virtual array<unsigned __int64>^ ReadULongs(int count);

	protected:
		ICallback^ _callback;
		__int64 _pos;
		__int64 _prev_pos;

		virtual void __Start();
		virtual void __Update();
		virtual void __Stop();

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

		// Read N values of type 'Type', returning as managed array
		template<typename Type>
		array<Type>^ _Read(const int count)
		{
			array<Type>^ arr = gcnew array<Type>(count);
			pin_ptr<Type> p = &arr[0];
			_Read<Type>(p, count);
			return arr;
		}

	};

};
