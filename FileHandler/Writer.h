#pragma once

using namespace FileHandler;

namespace Writer
{

	// Writer interface
	public interface class IWriter
	{
	public:
		// Properties
		property const __int64 Pos { const __int64 get() abstract; };
		property const __int64 PrevPos { const __int64 get() abstract; };
		property String^ Name { String^ get() abstract; };
		property String^ Fullname { String^ get() abstract; };

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
		WriteException(IWriter^ writer, int count);
		WriteException(IWriter^ writer, String^ msg);
	};


	// Code common for all writers
	public ref class WriterBase abstract : IWriter
	{
	public:
		WriterBase(ICallback^ callback);

		// Properties
		virtual property const __int64 Pos { const __int64 get(); };
		virtual property const __int64 PrevPos { const __int64 get(); };
		virtual property String^ Name { String^ get() abstract; };
		virtual property String^ Fullname { String^ get() abstract; };

		// Methods
		virtual void Close();

		//virtual const __int64 Seek(__int64 offset, IWriteer::Positioning pos) abstract;
		//=> For now, no seeking while writing

		virtual void Write(bool val);
		virtual void Write(bool* buff, const int count);

		virtual void Write(byte val);
		virtual void Write(byte* buff, const int count);

		virtual void Write(__int16 val);
		virtual void Write(__int16* buff, const int count);

		virtual void Write(unsigned __int16 val);
		virtual void Write(unsigned __int16* buff, const int count);

		virtual void Write(__int32 val);
		virtual void Write(__int32* buff, const int count);

		virtual void Write(unsigned __int32 val);
		virtual void Write(unsigned __int32* buff, const int count);

		virtual void Write(__int64 val);
		virtual void Write(__int64* buff, const int count);

		virtual void Write(unsigned __int64 val);
		virtual void Write(unsigned __int64* buff, const int count);

		virtual void Write(float val);
		virtual void Write(float* buff, const int count);

		virtual void Write(double val);
		virtual void Write(double* buff, const int count);

		// Writes a string, if length=0 it's assumed to be prefixed with its length.
		virtual void Write(str^ val);
		virtual void Write(str^ val, const int length);

		virtual void Write(ByteArray^ vals);

		virtual void Write(array<__int16>^ vals);
		virtual void Write(array<unsigned __int16>^ vals);

		virtual void Write(array<__int32>^ vals);
		virtual void Write(array<unsigned __int32>^ vals);

		virtual void Write(array<__int64>^ vals);
		virtual void Write(array<unsigned __int64>^ vals);

	protected:
		ICallback^ _callback;
		__int64 _pos;
		__int64 _prev_pos;

		virtual void __Start();
		virtual void __Update();
		virtual void __Stop();

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

};

