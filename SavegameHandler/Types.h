#pragma once
#pragma warning(disable : 4091)

// Types used with save games
// (both unmanaged and managed ones)
//


#ifndef byte
typedef unsigned __int8 byte;
#endif


// Sort-of C#s 'is'
template<typename _Desired, typename _Passed>
inline bool IsInstance(_Passed inst_to_check)
{
	return (dynamic_cast<_Desired^>(inst_to_check) != nullptr);
}

template<typename _Desired, typename _Passed>
inline bool IsType(_Passed inst_to_check)
{
	return (inst_to_check->GetType() == _Desired::typeid);
}


typedef array<byte> ByteArray;

