#pragma once
#pragma warning(disable : 4091)

// Types used with save games
// (both unmanaged and managed ones)
//


#ifndef byte
typedef unsigned __int8 byte;
#endif


typedef array<byte> ByteArray;


// - Wrapper to reduce memory usage with ASCII strings
public ref class str
{
public:
	static const int UNKNOW = 0;
	static const int ASCII = 1;
	static const int WCHAR = 2;

	static ref class Statics
	{
	public:
		Statics()
		{
			empty = gcnew str((char*)"<empty>");
			EMPTY = empty->ToString();
		}

		static str^ empty;
		static String^ EMPTY;
	};


	str()
		: _ascii(nullptr)
		, _unicode(nullptr)
	{ }

	str(const int length, int type)
		: str()
	{
		_Reserve(length, type);
	}

	str(char *s) : str()
	{
		_Set(s);
	}

	str(String^ s) : str()
	{
		_Set(s);
	}


	String^ ToString() override
	{
		if (_ascii)
			return gcnew String(_ascii);
		if (_unicode)
			return gcnew String(_unicode);
		return Statics::EMPTY;
	}


	char *Ascii()
	{
		return _ascii;
	}

	wchar_t *Wchar()
	{
		return _unicode;
	}


	bool operator == (char *s)
	{
		if (!_ascii)
			throw "Empty string";
		return (strcmp(_ascii, s) == 0);
	}

	bool operator == (wchar_t *s)
	{
		if (!_unicode)
			throw "Empty string";
		return (wcscmp(_unicode, s) == 0);
	}


protected:
	char *_ascii;
	wchar_t *_unicode;


	void _Set(char *s)
	{
		_Free();

		int len = (int)strlen(s);
		_ascii = new char[len + 1];
		memcpy(_ascii, s, len + 1);
	}

	void _Set(String^ s)
	{
		_Free();

		int len = s->Length;
		pin_ptr<const wchar_t> p = PtrToStringChars(s);
		_unicode = new wchar_t[len + 1];
		memcpy(_unicode, p, len);
		_unicode[len] = 0;
	}
	
	void _Reserve(const int length, int type)
	{
		_Free();

		if (type == ASCII)
			_ascii = new char[length];
		else if (type == WCHAR)
			_unicode = new wchar_t[length];
		else
			throw "Parameter error";
	}

	void _Free()
	{
		if (_ascii)
			delete[] _ascii;
		_ascii = nullptr;

		if (_unicode)
			delete[] _unicode;
		_unicode = nullptr;
	}

};


