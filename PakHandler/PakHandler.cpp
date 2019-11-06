// This is the main DLL file.

#include "stdafx.h"

#include "PakHandler.h"

namespace PakHandler {

PakLoader::PakLoader(String^ filename)
	: Filename(filename)
	, Footer(gcnew Structures::Footer())
	, MountPoint("")
	, Index(gcnew Structures::IndexCollection())
{
}


bool PakLoader::Load(ICallback^ callback)
{
	return _Load(callback);
}


array<byte>^ PakLoader::ReadRaw(String^ filename)
{
	return ReadRaw(Index->Find(filename));
}

array<byte>^ PakLoader::ReadRaw(Structures::IndexEntry^ index)
{
	array<byte>^ data = nullptr;

	if (_reader != nullptr && index != nullptr)
	{
		if (index->Compressed)
			throw gcnew NotImplementedException("Pak: Compressed objects not yet supported");

		Structures::FPakEntry^ entry = Structures::FPakEntry::Create(_reader, index);
		if (entry == nullptr)
			return nullptr;

		data = entry->ReadData(_reader);

		//if (data != nullptr && entry->Compressed)
		//{
		//	//TODO: Decompress
		//}
	}

	return data;
}


array<byte>^ PakLoader::ReadAsset(String^ filename)
{
	if (filename == nullptr)
		return nullptr;

	// Read both files from Pak
	array<byte>^ uasset = ReadRaw(filename + ".uasset");
	if (uasset == nullptr || uasset->Length <= 0)
		return nullptr;
	array<byte>^ uexp   = ReadRaw(filename + ".uexp");
	if (uexp == nullptr || uexp->Length <= 0)
		return nullptr;

	// Combine into one blob
	array<byte>^ asset = gcnew array<byte>(uasset->Length + uexp->Length);
	if (asset == nullptr)
		return nullptr;
	uasset->CopyTo(asset, 0);
	uexp->CopyTo(asset, uasset->Length);
	uasset = uexp = nullptr;

	return asset;
}


Structures::FObject^ PakLoader::ReadObject(String^ filename)
{
	array<byte>^ asset = ReadAsset(filename);
	if (asset == nullptr)
		return nullptr;
	// ... and try to load as object
	Structures::FObject^ object = Structures::FObject::Create(asset);
	if (object == nullptr)
		Log::Error("Unable to read object from " + filename);
	return object;
}


Structures::FTexture2D^ PakLoader::ReadTexture(String^ filename)
{
	array<byte>^ asset = ReadAsset(filename);
	if (asset == nullptr)
		return nullptr;
	// ... and try to load as texture
	Structures::FTexture2D^ texture = Structures::FTexture2D::Create(asset);
	if (texture == nullptr)
		Log::Error("Unable to read texture from " + filename);
	return texture;
}


void PakLoader::Close()
{
	if (_reader != nullptr)
	{
		_reader->Close();
		_reader = nullptr;
	}
}


PakLoader::~PakLoader()
{
	Close();
}


void PakLoader::_cbStart(long count, String^ status, String^ info)
{
	if (_callback) 
		_callback->Start((__int64)count, status, info);
}

void PakLoader::_cbUpdate(long index, String^ status, String^ info)
{
	if (_callback) 
		_callback->Update((__int64)index, status, info);
}

void PakLoader::_cbStop(String^ status, String^ info)
{
	if (_callback) 
		_callback->Stop(status, info);
}


void PakLoader::LogRedirect(String^ s)
{
	Log::_(s, Logger::Level::Info, false, false);
}


bool PakLoader::_Load(ICallback^ callback)
{
	_callback = callback;

	_reader = gcnew FileReader(Filename, nullptr);
	//Log::Info("-> {0:#,#0} Bytes", _reader->Size);

	try
	{
		if (!Footer->Read(_reader))
			throw gcnew Exception(
				String::Format("Pak at pos {0:#,#0}: Failed to read header", _reader->Pos));

		if (_reader->Seek(Footer->IndexOffset, IReader::Positioning::Start) < 0)
			throw gcnew Exception(
				String::Format("Pak at pos 0: Failed to seek to Index at {0:#,#0}", Footer->IndexOffset));

		MountPoint = _reader->ReadString()->ToString();

		long count = _reader->ReadInt();
		_cbStart(count, "Loading file ...", "");

		for (long index = 0; index < count; ++index)
		{
			__int64 curr_pos = _reader->Pos;
			Structures::IndexEntry^ instance = Structures::IndexEntry::Create(_reader, Footer->Version);
			if (instance == nullptr)
				throw gcnew Exception(
					String::Format("Pak at pos {0:#,#0}: Failed to read index entry #{1:#,#0}", 
						curr_pos, index));
			Index->Add(instance);
			_cbUpdate(index, nullptr, instance->Filename);
		}

	}
	catch (Exception^)
	{
		MountPoint = "";
		Index->Clear();
		return false;
	}
	finally
	{
		_cbStop(nullptr, nullptr);
	}

	return true;
}

};//namespace PakHandler
