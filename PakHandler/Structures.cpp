// Implementation for general UE structures
//

#include "stdafx.h"

namespace PakHandler
{
  namespace Structures
  {


// 
//

Footer::Footer()
	: base()
	, Version(PakVersion::None)
	, Sha1(nullptr)
{ }

bool Footer::Read(IReader^ reader)
{
	// UE 4.21
	if (_TryReadUE421(reader))
		return true;
	// UE 4.22
	if (_TryReadUE422(reader))
		return true;

	Log::Error("PakFooter: Unknown PAK format");
	return false;
}

bool Footer::_TryReadUE421(IReader^ reader)
{
	if (reader->Seek(-44, IReader::Positioning::End) < 0)
	{
		Log::Error("PakFooter: Failed to seek to END - 44");
		return false;
	}

	Magic = reader->ReadUInt();
	if (Magic != MAGIC)
	{
		Log::Error("PakFooter: Invalid magic {0:X8}, expected {1:X8}", Magic, MAGIC);
		return false;
	}

	int version = reader->ReadInt();
	if (version < (int)PakVersion::V1 || version >= (int)PakVersion::MAX)
	{
		Log::Error("PakFooter: Version V{0} not supported", version);
		return false;
	}
	Version = (PakVersion) version;

	IndexOffset = reader->ReadLong();
	IndexSize   = reader->ReadLong();
	if (IndexOffset < 0 || IndexSize < 0 || IndexOffset + IndexSize > reader->Size)
	{
		Log::Error("PakFooter: Invalid index offset/size");
		return false;
	}

	Sha1 = reader->ReadBytes(20);
	//Sha1 check skipped for now

	if (reader->Pos != reader->Size)
	{
		Log::Error("PakFooter: Not all bytes read");
		return false;
	}

	if (reader->Seek(0, IReader::Positioning::Start) < 0)
	{
		Log::Error("PakFooter: Failed to seek to START");
		return false;
	}

	return true;
}

bool Footer::_TryReadUE422(IReader^ reader)
{
	if (reader->Seek(-172, IReader::Positioning::End) < 0)
	{
		Log::Error("PakFooter: Failed to seek to END - 44");
		return false;
	}

	Magic = reader->ReadUInt();
	if (Magic != MAGIC)
	{
		Log::Error("PakFooter: Invalid magic {0:X8}, expected {1:X8}", Magic, MAGIC);
		return false;
	}

	int version = reader->ReadInt();
	if (version < (int)PakVersion::V1 || version >= (int)PakVersion::MAX)
	{
		Log::Error("PakFooter: Version V{0} not supported", version);
		return false;
	}
	Version = (PakVersion) version;

	IndexOffset = reader->ReadLong();
	IndexSize   = reader->ReadLong();
	if (IndexOffset < 0 || IndexSize < 0 || IndexOffset + IndexSize > reader->Size)
	{
		Log::Error("PakFooter: Invalid index offset/size");
		return false;
	}

	Sha1 = reader->ReadBytes(20);
	//Sha1 check skipped for now

	// A 4 compression names (each a 32 bytes) following
	array<byte>^ methods = reader->ReadBytes(4*32);


	if (reader->Pos != reader->Size)
	{
		Log::Error("PakFooter: Not all bytes read");
		return false;
	}

	if (reader->Seek(0, IReader::Positioning::Start) < 0)
	{
		Log::Error("PakFooter: Failed to seek to START");
		return false;
	}

	return true;
}

void Footer::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[Footer]");
	d->AddLine("- Magic      : " + Magic);
	d->AddLine("- Version    : " + ((int)Version).ToString());
	d->AddLine("- IndexOffset: 0x" + IndexOffset.ToString("X16"));
	d->AddLine("- IndexSize  : " + IndexSize);
	d->AddLine("- Sha1       : <skipped>");// + String::Join(", ", Sha1));
}


// 
//

IndexEntry::IndexEntry(PakVersion version)
	: base()
	, Version(version)
	, Filename(nullptr)
	, CompressionMethod(PakCompression::None)
	, Sha1(nullptr)
	, CompressedBlocks(nullptr)
{ }

IndexEntry^ IndexEntry::Create(IReader^ reader, PakVersion version)
{
	IndexEntry^ instance = gcnew IndexEntry(version);
	if (!instance->Read(reader))
		instance = nullptr;
	return instance;
}

bool IndexEntry::Read(IReader^ reader)
{
	Filename = reader->ReadString()->ToString();

	switch (Version)
	{
	case PakVersion::V1: return ReadV1(reader);
	case PakVersion::V2: return ReadV2(reader);
	case PakVersion::V3: return ReadV3(reader);
	case PakVersion::V4: return ReadV4(reader);
	case PakVersion::V5: return ReadV5(reader);
	case PakVersion::V6: return ReadV6(reader);
	case PakVersion::V7: return ReadV7(reader);
	case PakVersion::V8: return ReadV8(reader);
	}

	return false;
}

bool IndexEntry::ReadV1(IReader^ reader)
{
	Offset            = reader->ReadLong();
	CompressedSize    = reader->ReadLong();
	UncompressedSize  = reader->ReadLong();
	CompressionMethod = (PakCompression) reader->ReadInt();
	Timestamp         = reader->ReadLong(); // same as with Satis saves?
	Sha1              = reader->ReadBytes(20);

	return true;
}

bool IndexEntry::ReadV2(IReader^ reader)
{
	Offset            = reader->ReadLong();
	CompressedSize    = reader->ReadLong();
	UncompressedSize  = reader->ReadLong();
	if (Version < PakVersion::V8)
		CompressionMethod = (PakCompression) reader->ReadInt();
	else if (Version == PakVersion::V8)
		CompressionMethod = (PakCompression) reader->ReadByte(); // Looks like CSS changed this size
	else
	{
		Log::Error("IndexEntry: Unable to read entry");
		return false;
	}
	Sha1              = reader->ReadBytes(20);

	return true;
}

bool IndexEntry::ReadV3(IReader^ reader)
{
	if (!ReadV2(reader))
		return false;

	if (Compressed)
	{
		int blocks = reader->ReadInt();
		CompressedBlocks = reader->ReadLongs(blocks * 2);
	}

	Flags = reader->ReadByte();

	CompressedBlockSize = reader->ReadInt();

	return true;
}

bool IndexEntry::ReadV4(IReader^ reader)
{
	if (!ReadV3(reader))
		return false;

	//Unknown1 = reader->ReadInt();

	return true;
}

bool IndexEntry::ReadV5(IReader^ reader)
{
	if (!ReadV4(reader))
		return false;

	return true;
}

bool IndexEntry::ReadV6(IReader^ reader)
{
	if (!ReadV5(reader))
		return false;

	return true;
}

bool IndexEntry::ReadV7(IReader^ reader)
{
	if (!ReadV6(reader))
		return false;

	return true;
}

bool IndexEntry::ReadV8(IReader^ reader)
{
	if (!ReadV7(reader))
		return false;

	return true;
}

void IndexEntry::DumpTo(DumpToFileHelper^ d)
{
	//TODO:
	//PakVersion      Version;
	//String^         Filename;
	//__int64         Offset;                 // Points to a FPakEntry structure, NOT actual content directly!
	//__int64         CompressedSize;
	//__int64         UncompressedSize;
	//PakCompression  CompressionMethod;      // Up to V7, replaced by "CompressionMethodIndex" in V8
	//array<byte>^    Sha1;
	//__int64         Timestamp;              // V1 only
	//array<__int64>^ CompressedBlocks;       // V3++
	//__int32         CompressedBlockSize;    // V3++
	//byte            Flags;                  // V3++
	//byte            CompressionMethodIndex; // V8++
}


// 
//

IndexCollection::IndexCollection()
{ }

IndexEntry^ IndexCollection::Find(String^ filename)
{
	for each (IndexEntry^ entry in this)
	{
		if (filename->Equals(entry->Filename))
			return entry;
	}
	return nullptr;
}
	

// 
//

FPakEntry::FPakEntry(IndexEntry^ index)
	: Index(index)
{ }

FPakEntry^ FPakEntry::Create(IReader^ reader, IndexEntry^ index)
{
	FPakEntry^ instance = gcnew FPakEntry(index);
	if (!instance->Read(reader))
		instance = nullptr;
	return instance;
}

bool FPakEntry::Read(IReader^ reader)
{
	if (reader->Seek(Index->Offset, IReader::Positioning::Start) != Index->Offset)
	{
		Log::Error("PakEntry: Failed to seek to {0:#,#0}", Index->Offset);
		return false;
	}

	Offset = reader->ReadULong();
	if (Offset != 0)
	{
		Log::Info("PakEntry: Found an offset {0:#,#0}", Offset);
	}

	CompressedSize   = reader->ReadULong();
	UncompressedSize = reader->ReadULong();

	if (Index->Version <= PakVersion::V7)
	{
		CompressionMethod = (PakCompression) reader->ReadInt();
	}
	else
	{
		CompressionMethodIndex = reader->ReadByte();
		//TODO:
		CompressionMethod = (PakCompression) CompressionMethodIndex;
	}

	if (Index->Version < PakVersion::V2)
	{
		Timestamp = reader->ReadLong();
	}

	Sha1 = reader->ReadBytes(20);

	if (Index->Version >= PakVersion::V3)
	{
		//if (CompressionMethod != 0)
		//	CompressedBlocks[] = ...

		Flags = reader->ReadByte();

		CompressedBlockSize = reader->ReadInt();
	}

	RealOffset = reader->Pos + Offset;//????

	return true;
}

array<byte>^ FPakEntry::ReadData(IReader^ reader)
{
	if (CompressedSize > 0x7FFFFFFF)
	{
		Log::Error("PakEntry: File is too big, {0:#,#0} bytes but only {1:#,#0} supported)",
			CompressedSize, 0x7FFFFFFF);
		return nullptr;
	}

	if (reader->Seek(RealOffset, IReader::Positioning::Start) != RealOffset)
	{
		Log::Error("PakEntry: Failed to seek to {0:#,#0}", RealOffset);
		return nullptr;
	}

	return reader->ReadBytes((int)CompressedSize);
}

void FPakEntry::DumpTo(DumpToFileHelper^ d)
{
	//TODO:
	//IndexEntry^     Index;
	//qword           Offset;
	//qword           CompressedSize;
	//qword           UncompressedSize;
	//PakCompression  CompressionMethod;      // Up to V7, replaced by "CompressionMethodIndex" in V8
	//byte            CompressionMethodIndex; // V8++
	//__int64         Timestamp;              // V1 only
	//array<byte>^    Sha1;
	//array<__int64>^ CompressedBlocks;       // V3++
	//byte            Flags;                  // V3++
	//__int32         CompressedBlockSize;    // V3++
	//__int64         RealOffset;             // Computed: Pos after last header byte was read
}


// 
//

FGuid::FGuid()
	: base()
{
	A = B = C = D = -1;
}

FGuid::FGuid(dword a, dword b, dword c, dword d)
	: base()
{
	A = a;
	B = b;
	C = c;
	D = d;
}

bool FGuid::Read(IReader^ reader)
{
	A = reader->ReadUInt();
	B = reader->ReadUInt();
	C = reader->ReadUInt();
	D = reader->ReadUInt();

	return true;
}

void FGuid::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine(ToString());
}

String^ FGuid::ToString()
{
	return String::Format("[FGuid] {0:X8}.{1:X8}.{2:X8}.{3:X8}", A, B, C, D);
}

bool FGuid::Equals(FGuid^ right)
{
	if (!right)
		return false;
	return (A == right->A) && (B == right->B) && (C == right->C) && (D == right->D);
}


// 
//

FGenerationInfo::FGenerationInfo()
	: base()
{
	ExportCount = NameCount = NetObjectCount = 0;
}

bool FGenerationInfo::Read(IReader^ reader)
{
	ExportCount = reader->ReadInt();
	NameCount   = reader->ReadInt();
	if (IsUE3)
		NetObjectCount = reader->ReadInt();

	return true;
}

void FGenerationInfo::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FGenerationInfo]");
	d->AddLine("- ExportCount   : " + ExportCount);
	d->AddLine("- NameCount     : " + NameCount);
	if (IsUE3)
		d->AddLine("- NetObjectCount: " + NetObjectCount);
}


// 
//

FEngineVersion::FEngineVersion()
	: base()
{
	Major = Minor = Patch = 0;
	Changelist = 0;
	Branch = nullptr;
}

bool FEngineVersion::Read(IReader^ reader)
{
	Major	   = reader->ReadUShort();
	Minor	   = reader->ReadUShort();
	Patch	   = reader->ReadUShort();
	Changelist = reader->ReadInt();
	Branch     = reader->ReadString()->ToString();

	return true;
}

void FEngineVersion::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FEngineVersion]");
	d->AddLine("- Major	    : " + Major);
	d->AddLine("- Minor	    : " + Minor);
	d->AddLine("- Patch	    : " + Patch);
	d->AddLine("- Changelist: " + Changelist);
	d->AddLine("- Branch    : " + Branch);
}


// 
//

FCompressedChunk::FCompressedChunk()
	: base()
{
}

bool FCompressedChunk::Read(IReader^ reader)
{
	UncompressedOffset = reader->ReadInt();
	UncompressedSize   = reader->ReadInt();
	CompressedOffset   = reader->ReadInt();
	CompressedSize     = reader->ReadInt();

	return true;
}

void FCompressedChunk::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FCompressedChunk]");
	d->AddLine("- UncompressedOffset: " + UncompressedOffset);
	d->AddLine("- UncompressedSize  : " + UncompressedSize);
	d->AddLine("- CompressedOffset  : " + CompressedOffset);
	d->AddLine("- CompressedSize    : " + CompressedSize);
}


// 
//

FObjectExport::FObjectExport()
	: base()
	, ObjectName(nullptr)
//	, NetObjects(nullptr)
	, Guid(nullptr)
{ }

bool FObjectExport::Read(IReader^ reader, array<String^>^ names)
{
	ClassIndex    = reader->ReadInt();
	SuperIndex    = reader->ReadInt();
	TemplateIndex = reader->ReadInt();
	PackageIndex  = reader->ReadInt();

	ObjectName    = FName<array<String^>>::Create(reader, names);

//	Architecture  = reader->ReadInt();

	ObjectFlags   = reader->ReadInt();

	SerialSize    = reader->ReadInt();
	SerialOffset  = reader->ReadInt();

	ForcedExport  = reader->ReadInt() != 0;
	NotForClients = reader->ReadInt() != 0;
	NotForServers = reader->ReadInt() != 0;

	int count = reader->ReadInt();
	if (count > 0)
	{
		//NetObjects = nullptr;
	}

	Guid          = FGuid::Create(reader);

	PackageFlags  = reader->ReadInt();

	NotForEditorGame = reader->ReadInt() != 0;
	IsAsset          = reader->ReadInt() != 0;

	FirstExportDependency                        = reader->ReadInt();
	SerializationBeforeSerializationDependencies = reader->ReadInt();
	CreateBeforeSerializationDependencies        = reader->ReadInt();
	SerializationBeforeCreateDependencies        = reader->ReadInt();
	CreateBeforeCreateDependencies               = reader->ReadInt();

	return true;
}

void FObjectExport::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FObjectExport]");
	d->AddLine("- ClassIndex                                  : " + ClassIndex);
	d->AddLine("- SuperIndex                                  : " + SuperIndex);
	d->AddLine("- TemplateIndex                               : " + TemplateIndex);
	d->AddLine("- PackageIndex                                : " + PackageIndex);
	d->AddLine("- ObjectName                                  : "); 
	d->Push();
	if (ObjectName)
		ObjectName->DumpTo(d);
	else
		d->AddLine("<NULL>");
	d->Pop();
//	d->AddLine("- Architecture                                : " + Architecture);
	d->AddLine("- ObjectFlags                                 : 0x" + ObjectFlags.ToString("X8"));
	d->AddLine("- SerialSize                                  : " + SerialSize);
	d->AddLine("- SerialOffset                                : 0x" + SerialOffset.ToString("X8"));
	d->AddLine("- ForcedExport                                : " + ForcedExport);
	d->AddLine("- NotForClients                               : " + NotForClients);
	d->AddLine("- NotForServers                               : " + NotForServers);
//	d->AddLine("- NetObjects                                  : " + NetObjects);
	d->AddLine("- Guid                                        : "); 
	d->Push();
	Guid->DumpTo(d);
	d->Pop();
	d->AddLine("- PackageFlags                                : 0x" + PackageFlags.ToString("X8"));
	d->AddLine("- NotForEditorGame                            : " + NotForEditorGame);
	d->AddLine("- IsAsset                                     : " + IsAsset);
	d->AddLine("- FirstExportDependency                       : " + FirstExportDependency);
	d->AddLine("- SerializationBeforeSerializationDependencies: " + SerializationBeforeSerializationDependencies);
	d->AddLine("- CreateBeforeSerializationDependencies       : " + CreateBeforeSerializationDependencies);
	d->AddLine("- SerializationBeforeCreateDependencies       : " + SerializationBeforeCreateDependencies);
	d->AddLine("- CreateBeforeCreateDependencies              : " + CreateBeforeCreateDependencies);
}


// 
//

FObjectImport::FObjectImport()
	: base()
	, ClassPackage(nullptr)
	, ClassName(nullptr)
	, ObjectName(nullptr)
{ }

bool FObjectImport::Read(IReader^ reader, array<String^>^ names)
{
	ClassPackage = FName<array<String^>>::Create(reader, names);
	ClassName    = FName<array<String^>>::Create(reader, names);
	PackageIndex = reader->ReadInt();
	ObjectName   = FName<array<String^>>::Create(reader, names);

	return true;
}

void FObjectImport::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FObjectImport]");
	d->AddLine(" - ClassPackage: "); 
	d->Push();
	ClassPackage->DumpTo(d);
	d->Pop();
	d->AddLine(" - ClassName   : "); 
	d->Push();
	ClassName->DumpTo(d);
	d->Pop();
	d->AddLine(" - PackageIndex: " + PackageIndex);
	d->AddLine(" - ObjectName  : "); 
	d->Push();
	ObjectName->DumpTo(d);
	d->Pop();
}


//TODO:
// 
//

FObjectDepends::FObjectDepends()
	: base()
	, Dependencies(nullptr)
{ }

bool FObjectDepends::Read(IReader^ reader)
{
	//TODO:
	return true;
}

void FObjectDepends::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FObjectDepends]: TODO!");
}


//TODO:
// 
//

FPreloadDepends::FPreloadDepends()
	: base()
//	, Dependencies(nullptr)
{ }

bool FPreloadDepends::Read(IReader^ reader, array<String^>^ names)
{
	//TODO:
	return true;
}

void FPreloadDepends::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FPreloadDepends]: TODO!");
}


// 
//

FPackageFileSummary::FPackageFileSummary()
	: base()
	, Engine(EngineVersion::None)
	, Group(nullptr)
	, Guid(nullptr)
	, Generations(nullptr)
	, EngineVersion(nullptr)
	, CompatibleEngineVersion(nullptr)
	, CompressedChunks(nullptr)
	, ChunkIDs(nullptr)
	, Names(nullptr)
	, Exports(nullptr)
	, Imports(nullptr)
	, Depends(nullptr)
//	, AssetRegistryData(nullptr)
	, PreloadDependencyIndices(nullptr)
	, PreloadDependencies(nullptr)
{ }

bool FPackageFileSummary::Read(IReader^ reader)
{
	Magic = reader->ReadUInt();
	if (Magic != MAGIC)
	{
		Log::Error("Package: Invalid magic {0:X8}, expected {1:X8}", Magic, MAGIC);
		return false;
	}

	LegacyVersion = reader->ReadInt();
	if (LegacyVersion >= 0)
	{
		Log::Error("Package: Invalid version {0}, expected a negative value", LegacyVersion);
		return false;
	}
	switch (LegacyVersion)
	{
	case -3: Engine = EngineVersion::V4_0 ; break;
	case -4: Engine = EngineVersion::V4_7 ; break;
	case -5: Engine = EngineVersion::V4_7 ; break;
	case -6: Engine = EngineVersion::V4_11; break;
	case -7: Engine = EngineVersion::V4_14; break;
	default:
		Log::Error("Package: Version {0} not supported", LegacyVersion);
		return false;
	}

	if (LegacyVersion != -4)
		VersionUE3  = reader->ReadInt();
	Version         = reader->ReadInt() & 0x0000FFFF;
	LicenseeVersion = reader->ReadInt() & 0x0000FFFF;

	if (LegacyVersion <= -2)
	{
		// Custom versions array
		int count = reader->ReadInt();
		if (count > 0)
		{
			Log::Warning("Package: Custom version handling needed!");
			return false;
		}
	}

	HeaderSize = reader->ReadInt();
	if (HeaderSize <= 0)
	{
		Log::Error("Package: Invalid header size {0}!", HeaderSize);
		return false;
	}

	Group = reader->ReadString()->ToString();

	Flags = reader->ReadInt();

	NamesCount = reader->ReadInt();
	NamesOffset = reader->ReadInt();

	GatherableTextDataCount	 = reader->ReadInt();
	GatherableTextDataOffset = reader->ReadInt();

	ExportCount	  = reader->ReadInt();
	ExportOffset  = reader->ReadInt();

	ImportCount	  = reader->ReadInt();
	ImportOffset  = reader->ReadInt();

	DependsOffset = reader->ReadInt();

	StringAssetReferencesCount  = reader->ReadInt();
	StringAssetReferencesOffset = reader->ReadInt();

	SearchableNamesOffset = reader->ReadInt();

	ThumbnailTableOffset = reader->ReadInt();

	Guid = FGuid::Create(reader);
	if (Guid == nullptr)
	{
		Log::Error("Package: Unable to read guid");
		return false;
	}

	int GenerationsCount = reader->ReadInt();
	if (GenerationsCount > 0)
	{
		Generations = gcnew array<FGenerationInfo^>(GenerationsCount);
		for (int i=0; i<GenerationsCount; ++i)
		{
			FGenerationInfo^ info = FGenerationInfo::Create(reader);
			if (info == nullptr)
			{
				Log::Error("Package: Error reading generation {0}", i);
				return false;
			}
			Generations[i] = info;
		}
	}

	EngineVersion = FEngineVersion::Create(reader);
	if (EngineVersion == nullptr)
	{
		Log::Error("Package: Unable to read engine version struct");
		return false;
	}

	CompatibleEngineVersion = FEngineVersion::Create(reader);
	if (CompatibleEngineVersion == nullptr)
	{
		Log::Error("Package: Unable to read compatible engine version struct");
		return false;
	}

	CompressionFlags = reader->ReadInt();
	int count = reader->ReadInt();
	if (count > 0)
	{
		Log::Warning("Package: Compression chunks handling needed!");
		return false;
	}

	PackageSource = reader->ReadInt();

	count = reader->ReadInt();
	if (count > 0)
	{
		Log::Warning("Package: Additional package list handling needed!");
		return false;
	}

	AssetRegistryDataOffset = reader->ReadInt();

	BulkDataStartOffset = reader->ReadULong();

	WorldTileInfoDataOffset = reader->ReadInt();

	ChunkIDsCount = reader->ReadInt();
	if (ChunkIDsCount > 0)
		ChunkIDs = reader->ReadInts(ChunkIDsCount);

	PreloadDependencyCount = reader->ReadInt();
	PreloadDependencyOffset = reader->ReadInt();

	// ... there might be more data, but no info yet on how to handle
	// so we do check end magic now and go back afterwards
	__int64 last_pos = reader->Pos;

	if (reader->Seek(BulkDataStartOffset, IReader::Positioning::Start) != BulkDataStartOffset)
	{
		Log::Error("Package: Failed to seek to bulk start offset at {0:#,#0}", BulkDataStartOffset);
		return false;
	}
	dword end_magic = reader->ReadUInt();
	if (end_magic != Magic)
	{
		Log::Error("Package: Invalid end magic {0:X8}, expected {1:X8}", end_magic, Magic);
		return false;
	}

	// ... Continue loading
	if (reader->Seek(last_pos, IReader::Positioning::Start) != last_pos)
	{
		Log::Error("Package: Failed to seek back to {0:#,#0}", last_pos);
		return false;
	}

	//TODO: Evaluate which info is needed and which info can be skipped safely

	if (NamesCount > 0)
	{
		if (NamesOffset <= 0)
		{
			Log::Error("Package: Invalid NamesOffset {0:#,#0}", NamesOffset);
			return false;
		}

		if (reader->Seek(NamesOffset, IReader::Positioning::Start) != NamesOffset)
		{
			Log::Error("Package: Failed to seek to NamesOffset at {0:#,#0}", NamesOffset);
			return false;
		}

		Names = gcnew array<String^>(NamesCount);
		for (int i = 0; i < NamesCount; ++i)
		{
			__int64 last_pos = reader->Pos;
			String^ name = reader->ReadString()->ToString();
			if (name == nullptr)
			{
				Log::Error("Package: Failed to read name #{0} at {1:#,#0}", i, last_pos);
				return false;
			}
			Names[i] = name;

			// Hash is ignored (which seems to be a int16/int16 for upper/case-preserve 
			// hashes, which might explain my probs calculating those!)
			__int32 hash = reader->ReadInt();
		}
	}

	if (ExportCount > 0)
	{
		if (ExportOffset <= 0)
		{
			Log::Error("Package: Invalid ExportOffset {0:#,#0}", ExportOffset);
			return false;
		}

		if (reader->Seek(ExportOffset, IReader::Positioning::Start) != ExportOffset)
		{
			Log::Error("Package: Failed to seek to ExportOffset at {0:#,#0}", ExportOffset);
			return false;
		}

		Exports = gcnew array<FObjectExport^>(ExportCount);
		for (int i = 0; i < ExportCount; ++i)
		{
			__int64 last_pos = reader->Pos;
			FObjectExport^ instance = FObjectExport::Create(reader, Names);
			if (instance == nullptr)
			{
				Log::Error("Package: Failed to read export #{0} at {1:#,#0}", i, last_pos);
				return false;
			}
			Exports[i] = instance;
		}
	}

	if (ImportCount > 0)
	{
		if (ImportOffset <= 0)
		{
			Log::Error("Package: Invalid ImportOffset {0:#,#0}", ImportOffset);
			return false;
		}

		if (reader->Seek(ImportOffset, IReader::Positioning::Start) != ImportOffset)
		{
			Log::Error("Package: Failed to seek to ImportCount at {0:#,#0}", ImportOffset);
			return false;
		}

		Imports = gcnew array<FObjectImport^>(ImportCount);
		for (int i = 0; i < ImportCount; ++i)
		{
			__int64 last_pos = reader->Pos;
			FObjectImport^ instance = FObjectImport::Create(reader, Names);
			if (instance == nullptr)
			{
				Log::Error("Package: Failed to read import #{0} at {1:#,#0}", i, last_pos);
				return false;
			}
			Imports[i] = instance;
		}
	}

	//TODO: DependsOffset -> DependsMap

	//TODO: AssetRegistryDataOffset
	//if (AssetRegistryDataOffset > 0)
	//{
	//	if (reader->Seek(AssetRegistryDataOffset, IReader::Positioning::Start) != AssetRegistryDataOffset)
	//	{
	//		Log::Error("Package: Failed to seek to AssetRegistryDataOffset at {0:#,#0}", AssetRegistryDataOffset);
	//		return false;
	//	}
	//
	//	count = reader->ReadInt();
	//	if (count > 0)
	//	{
	//		Log::Warning("Package: Asset registry handling needed!");
	//		return false;
	//
	//		//AssetRegistryData = gcnew array< ? >(count);
	//		//...
	//	}
	//}

	//TODO: PreloadDependencies
	if (PreloadDependencyCount > 0)
	{
		if (PreloadDependencyOffset <= 0)
		{
			Log::Error("Package: Invalid PreloadDependencyOffset {0:#,#0}", PreloadDependencyOffset);
			return false;
		}
	
		if (reader->Seek(PreloadDependencyOffset, IReader::Positioning::Start) != PreloadDependencyOffset)
		{
			Log::Error("Package: Failed to seek to PreloadDependencyOffset at {0:#,#0}", PreloadDependencyOffset);
			return false;
		}

		__int64 last_pos = reader->Pos;
		PreloadDependencyIndices = reader->ReadInts(PreloadDependencyCount);
		if (PreloadDependencyIndices == nullptr)
		{
			Log::Error("Package: Failed to read preload dependency indices at {0:#,#0}", last_pos);
			return false;
		}

		//PreloadDependencies = gcnew array<FPreloadDepends^>(PreloadDependencyCount);
		//for (int i = 0; i < PreloadDependencyCount; ++i)
		//{
		//	__int64 last_pos = reader->Pos;
		//	FPreloadDepends^ instance = FPreloadDepends::Create(reader, Names);
		//	if (instance == nullptr)
		//	{
		//		Log::Error("Package: Failed to read preload dependency #{0} at {1:#,#0}", i, last_pos);
		//		return false;
		//	}
		//	PreloadDependencies[i] = instance;
		//}
	}


	return true;
}

void FPackageFileSummary::DumpTo(DumpToFileHelper^ d)
{
	int index = 0;

	d->AddLine("[FPackageFileSummary]");
	d->AddLine(" - Magic                      : 0x" + Magic.ToString("X8"));
	d->AddLine(" - LegacyVersion              : " + LegacyVersion);
	d->AddLine(" - Engine                     : " + ((int)Engine).ToString());
	d->AddLine(" - VersionUE3                 : " + VersionUE3);
	d->AddLine(" - Version                    : " + Version);
	d->AddLine(" - LicenseeVersion            : " + LicenseeVersion);
//	d->AddLine(" - CustomVersions             : " + CustomVersions);
	d->AddLine(" - HeaderSize                 : " + HeaderSize);
	d->AddLine(" - Group                      : " + Group);
	d->AddLine(" - Flags                      : 0x" + Flags.ToString("X8"));
	d->AddLine(" - NamesCount                 : " + NamesCount);
	d->AddLine(" - NamesOffset                : 0x" + NamesOffset.ToString("X8"));
	d->AddLine(" - GatherableTextDataCount    : " + GatherableTextDataCount);
	d->AddLine(" - GatherableTextDataOffset   : 0x" + GatherableTextDataOffset.ToString("X8"));
	d->AddLine(" - ExportCount                : " + ExportCount);
	d->AddLine(" - ExportOffset               : 0x" + ExportOffset.ToString("X8"));
	d->AddLine(" - ImportCount                : " + ImportCount);
	d->AddLine(" - ImportOffset               : 0x" + ImportOffset.ToString("X8"));
	d->AddLine(" - DependsOffset              : 0x" + DependsOffset.ToString("X8"));
	d->AddLine(" - StringAssetReferencesCount : " + StringAssetReferencesCount);
	d->AddLine(" - StringAssetReferencesOffset: 0x" + StringAssetReferencesOffset.ToString("X8"));
	d->AddLine(" - SearchableNamesOffset      : 0x" + SearchableNamesOffset.ToString("X8"));
	d->AddLine(" - ThumbnailTableOffset       : 0x" + ThumbnailTableOffset.ToString("X8"));
	d->AddLine(" - Guid                       : "); 
	d->Push();
	Guid->DumpTo(d);
	d->Pop();
	d->AddLine(" - Generations                : " + (Generations != nullptr ? Generations->Length.ToString() : "-")); 
	if (Generations != nullptr)
	{
		d->Push();
		index = 0;
		for each (FGenerationInfo^ info in Generations)
		{
			d->AddLine("#" + index + ": ");
			++index;
			info->DumpTo(d);
		}
		d->Pop();
	}

	d->AddLine(" - EngineVersion              : ");
	d->Push();
	EngineVersion->DumpTo(d);
	d->Pop();
	d->AddLine(" - CompatibleEngineVersion    : "); 
	d->Push();
	CompatibleEngineVersion->DumpTo(d);
	d->Pop();
	d->AddLine(" - CompressionFlags           : 0x" + CompressionFlags.ToString("X8"));
	d->AddLine(" - CompressedChunks           : " + (CompressedChunks != nullptr ? CompressedChunks->Length.ToString() : "-"));
	if (CompressedChunks != nullptr)
	{
		d->Push();
		index = 0;
		for each (FCompressedChunk^ chunk in CompressedChunks)
		{
			d->AddLine("#" + index + ": ");
			++index;
			chunk->DumpTo(d);
		}
		d->Pop();
	}
	d->AddLine(" - PackageSource              : 0x" + PackageSource.ToString("X8"));
	d->AddLine(" - AssetRegistryDataOffset    : 0x" + AssetRegistryDataOffset.ToString("X8"));
	d->AddLine(" - BulkDataStartOffset        : 0x" + BulkDataStartOffset.ToString("X16"));
	d->AddLine(" - WorldTileInfoDataOffset    : 0x" + WorldTileInfoDataOffset.ToString("X8"));
	d->AddLine(" - ChunkIDsCount              : " + ChunkIDsCount);
	d->AddLine(" - ChunkIDs                   : <skipped>");
	//if (ChunkIDs != nullptr) //TODO:
	//{
	//	d->Push();
	//	...
	//	d->Pop();
	//}
	d->AddLine(" - PreloadDependencyCount     : " + PreloadDependencyCount);
	d->AddLine(" - PreloadDependencyOffset    : 0x" + PreloadDependencyOffset.ToString("X8"));

	d->AddLine(" ---");

	d->AddLine(" - Names                      : " + (Names != nullptr ? Names->Length.ToString() : "-"));
	if (Names != nullptr)
	{
		d->Push();
		index = 0;
		for each (String^ str in Names)
		{
			d->AddLine("#" + index + ": " + str);
			++index;
		}
		d->Pop();
	}
	d->AddLine(" - Exports                    : " + (Exports != nullptr ? Exports->Length.ToString() : "-"));
	if (Exports != nullptr)
	{
		d->Push();
		index = 0;
		for each (FObjectExport^ exp in Exports)
		{
			d->AddLine("#" + index + ": ");
			++index;
			exp->DumpTo(d);
		}
		d->Pop();
	}
	d->AddLine(" - Imports                    : " + (Imports != nullptr ? Imports->Length.ToString() : "-"));
	if (Imports != nullptr)
	{
		d->Push();
		index = 0;
		for each (FObjectImport^ imp in Imports)
		{
			d->AddLine("#" + index + ": ");
			++index;
			imp->DumpTo(d);
		}
		d->Pop();
	}
	d->AddLine(" - Depends                    : " + (Depends != nullptr ? Depends->Length.ToString() : "-"));
	if (Depends != nullptr)
	{
		d->Push();
		index = 0;
		for each (FObjectDepends^ dep in Depends)
		{
			d->AddLine("#" + index + ": ");
			++index;
			dep->DumpTo(d);
		}
		d->Pop();
	}
	//d->AddLine(" - PreloadDependencies        : " + (PreloadDependencies != nullptr ? PreloadDependencies->Length.ToString() : "-"));
	//if (PreloadDependencies != nullptr)
	//{
	//	d->Push();
	//	index = 0;
	//	for each (?^ dep in PreloadDependencies)
	//	{
	//		d->AddLine("#" + index + ": ");
	//		++index;
	//		dep->DumpTo(d);
	//	}
	//	d->Pop();
	//}
}


// 
//

FPropertyTag::FPropertyTag()
	: base()
	, Name(nullptr)
	, PropType(nullptr)
	, StructName(nullptr)
	, StructGuid(nullptr)
	, StructValue(nullptr)
	, EnumName(nullptr)
	, EnumValue(nullptr)
	, PropertyGuid(nullptr)
{ }

bool FPropertyTag::Read(IReader^ reader, array<String^>^ names)
{
	Name = FName<array<String^>>::Create(reader, names);
	if (Name == nullptr)
		return Error("PropertyTag: Unable to read Name");
	if (Name->Name == "None")
		return true;

	PropType = FName<array<String^>>::Create(reader, names);
	if (PropType == nullptr)
		return Error("PropertyTag: Unable to read PropType");

	DataSize = reader->ReadInt();
	ArrayIndex = reader->ReadInt();

	if (PropType->Name == "StructProperty")
	{
		StructName = FName<array<String^>>::Create(reader, names);
		if (StructName == nullptr)
			return Error("FPropertyTag: Unable to read StructName");
		StructGuid = FGuid::Create(reader);
		if (StructGuid == nullptr)
			return Error("FPropertyTag: Unable to read StructGuid");
	}
	else if (PropType->Name == "ByteProperty")
	{
		if (DataSize == 8)
		{
			EnumName = FName<array<String^>>::Create(reader, names);
			if (EnumName == nullptr)
				return Error("PropertyTag: Unable to read EnumName");
		}
		else
		{
			//TODO:
			return Warning("FPropertyTag: ByteProperty of length {0} not handled yet", DataSize);
		}
	}
	else if (PropType->Name == "BoolProperty")
	{
	}
	else
	{
		return Warning("FPropertyTag: Type {0} not handled yet", PropType->Name);
	}

	HasPropertyGuid = reader->ReadByte() != 0;
	if (HasPropertyGuid)
	{
		//TODO: Validate this is the correct type of Guid!
		PropertyGuid = FGuid::Create(reader);
		if (PropertyGuid == nullptr)
			return Error("FPropertyTag: Unable to read PropertyGuid");
	}

	if (PropType->Name == "StructProperty")
	{
		StructValue = reader->ReadBytes(DataSize);
		if (StructValue == nullptr)
			return Error("FPropertyTag: Unable to read StructValue");
	}
	else if (PropType->Name == "BoolProperty")
	{
		if (DataSize == 4)
		{
			BoolValue = reader->ReadInt();
		}
		else if (DataSize == 1 || DataSize == 0)
		{
			BoolValue = reader->ReadByte();
		}
		else
		{
			//TODO:
			return Warning("FPropertyTag: BoolProperty of length {0} not handled yet", DataSize);
		}
	}
	else if (PropType->Name == "ByteProperty")
	{
		if (DataSize == 8)
		{
			EnumValue = FName<array<String^>>::Create(reader, names);
			if (EnumValue == nullptr)
				return Error("FPropertyTag: Unable to read EnumValue");
		}
	}

	return true;
}

void FPropertyTag::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FPropertyTag]");
	d->AddLine("- FName^       Name           : " + (Name != nullptr ? "": "-")); 
	if (Name != nullptr)
	{
		d->Push();
		Name->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- FName^       PropType       : " + (PropType != nullptr ? "": "-")); 
	if (PropType != nullptr)
	{
		d->Push();
		PropType->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- __int32      DataSize       : " + DataSize       );
	d->AddLine("- __int32      ArrayIndex     : " + ArrayIndex     );
	d->AddLine("- FName^       StructName     : " + (StructName != nullptr ? "": "-")); 
	if (StructName != nullptr) 
	{
		d->Push();
		StructName->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- FGuid^       StructGuid     : " + (StructGuid != nullptr ? "": "-")); 
	if (StructGuid != nullptr) 
	{
		d->Push();
		StructGuid->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- array<byte>^ StructValue    : " + (StructValue != nullptr ? "": "-")); 
	if (StructValue != nullptr)
		d->Add(Helpers::Hexdump(StructValue, 16, true, true, 1, 0), false);
	d->AddLine("- FName^       EnumName       : " + (EnumName != nullptr ? "": "-")); 
	if (EnumName != nullptr) 
	{
		d->Push();
		EnumName->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- FName^       EnumValue      : " + (EnumValue != nullptr ? "": "-")); 
	if (EnumValue != nullptr) 
	{
		d->Push();
		EnumValue->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- bool         HasPropertyGuid: " + HasPropertyGuid);
	d->AddLine("- FGuid^       PropertyGuid   : " + (PropertyGuid != nullptr ? "": "-")); 
	if (PropertyGuid != nullptr) 
	{
		d->Push();
		PropertyGuid->DumpTo(d);
		d->Pop();
	}
}


// 
//

FObject::FObject()
	: base()
	, Summary(nullptr)
	, Properties(nullptr)
	, SerializedGuid(nullptr)
{ }

bool FObject::Read(IReader^ reader)
{
	// Read package file summary
	Summary = FPackageFileSummary::Create(reader);
	if (Summary == nullptr)
		return Error("Unable to read package file summary");

	// Read properties
	if (reader->Seek(Summary->HeaderSize, IReader::Positioning::Start) != Summary->HeaderSize)
		return Error("Failed to seek to data at {0:#,#0}", Summary->HeaderSize);
	Properties = gcnew List<FPropertyTag^>();
	while (true)
	{
		__int64 last_pos = reader->Pos;
		FPropertyTag^ tag = FPropertyTag::Create(reader, Summary->Names);
		if (tag == nullptr)
			return Error("Error reading tag #{0} at pos {1:#,#0}", Properties->Count, last_pos);
		if (tag->IsEmpty)
			break;
		Properties->Add(tag);
	}

	HasSerializedGuid = reader->ReadInt() != 0;
	if (HasSerializedGuid)
	{
		SerializedGuid = FGuid::Create(reader);
		if (SerializedGuid == nullptr)
			return Error("Failed to load serialized guid");
	}

	Length = reader->Pos - Summary->HeaderSize;
	Offset = reader->Pos;

	return true;
}

void FObject::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FObject]");
	d->AddLine("- Summary          : ");
	d->Push();
	Summary->DumpTo(d);
	d->Pop();
	d->AddLine("- Properties       : " + (Properties != nullptr ? Properties->Count.ToString() : "-"));
	if (Properties != nullptr)
	{
		d->Push();
		int index = 0;
		for each (FPropertyTag^ prop in Properties)
		{
			d->AddLine("#" + index + ": ");
			++index;
			prop->DumpTo(d);
		}
		d->Pop();
	}
	d->AddLine("- HasSerializedGuid: " + HasSerializedGuid);
	d->AddLine("- SerializedGuid   : " + (SerializedGuid != nullptr ? "" : "-"));
	if (SerializedGuid != nullptr)
	{
		d->Push();
		SerializedGuid->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- Length           : 0x" + Length.ToString("X16"));
	d->AddLine("- Offset           : 0x" + Offset.ToString("X16"));
}


// 
//

FPlatformData::FPlatformData()
	: base()
	, PixelFormat(nullptr)
	, PixelFormatString(nullptr)
{ }

bool FPlatformData::Read(IReader^ reader, array<String^>^ names)
{
	PixelFormat = FName<array<String^>>::Create(reader, names);
	if (PixelFormat == nullptr)
		return Error("Unable to read PixelFormat");

	SkippedOffset = reader->ReadLong();

	SizeX     = reader->ReadInt();
	SizeY     = reader->ReadInt();
	NumSlices = reader->ReadInt();

	PixelFormatString = reader->ReadString()->ToString();

	return true;
}

void FPlatformData::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FPlatformData]");
	d->AddLine("- PixelFormat      : " + (PixelFormat != nullptr ? "" : "-"));
	if (PixelFormat != nullptr)			
	{
		d->Push();
		PixelFormat->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- SkippedOffset    : 0x" + SkippedOffset.ToString("X16"));
	d->AddLine("- SizeX            : " + SizeX);
	d->AddLine("- SizeY            : " + SizeY);
	d->AddLine("- NumSlices        : " + NumSlices);
	d->AddLine("- PixelFormatString: " + (PixelFormatString != nullptr ? PixelFormatString : "-"));
}


// 
//

FBulkData::FBulkData()
	: base()
	, Data(nullptr)
{ }

bool FBulkData::Read(IReader^ reader)
{
	__int64 last_pos = reader->Pos;

	BulkDataFlags = reader->ReadUInt();
	bool is64 = (BulkDataFlags & (dword)EBulkDataFlags::Size64Bit) != 0;

	ElementCount = is64 ? reader->ReadLong() : reader->ReadInt();
	SizeOnDisk   = is64 ? reader->ReadLong() : reader->ReadInt();

	OffsetInFile = reader->ReadLong();
	if (OffsetInFile > reader->Pos)
		return Warning("BulkDataOffsetInFile > 0, needs to be handled");

	Data = reader->ReadBytes((int)ElementCount);
	if (Data == nullptr)
		return Error("Error reading data blob");

	return true;
}

void FBulkData::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FBulkData]");
	d->AddLine("- BulkDataFlags: 0x" + BulkDataFlags.ToString("X8"));
	d->AddLine("- ElementCount : 0x" + ElementCount.ToString("X16"));
	d->AddLine("- SizeOnDisk   : 0x" + SizeOnDisk.ToString("X16"));
	d->AddLine("- OffsetInFile : 0x" + OffsetInFile.ToString("X16"));
	d->AddLine("- Data         : " + (Data != nullptr ? Data->Length.ToString() : "-"));
}

void FBulkData::ClearData()
{
	Data = nullptr;
}


// 
//

FTexture2DMipMap::FTexture2DMipMap()
	: base()
	, BulkData(nullptr)
	, Bitmap(nullptr)
{ }

bool FTexture2DMipMap::Read(IReader^ reader, FPlatformData^ data)
{
	IsCooked = reader->ReadInt() != 0;

	BulkData = FBulkData::Create(reader);
	if (BulkData == nullptr)
		return Error("Error reading bulk data");

	SizeX = reader->ReadInt();
	SizeY = reader->ReadInt();
	SizeZ = reader->ReadInt();
	if (SizeZ != 1)
		return Error("Invalid z size {0}", SizeZ);

	// Generate pixel data
	if (!_CreateBitmap(data))
		return Error("Unable to generated pixel data");

	// Free bulk data after successful conversion
	//TODO: BulkData->ClearData();

	return true;
}

void FTexture2DMipMap::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FTexture2DMipMap]");
	d->AddLine("- IsCooked: " + IsCooked);
	d->AddLine("- BulkData: " + (BulkData != nullptr ? "" : "-"));
	if (BulkData != nullptr)
	{
		d->Push();
		BulkData->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- SizeX   : " + SizeX);
	d->AddLine("- SizeY   : " + SizeY);
	d->AddLine("- SizeZ   : " + SizeZ);
	d->AddLine("- Bitmap  : " + (Bitmap != nullptr ? "valid" : "-"));
}

bool FTexture2DMipMap::_CreateBitmap(FPlatformData^ data)
{
	array<byte>^ pixels = nullptr;
	if (data->PixelFormatString == "PF_B8G8R8A8")
		pixels = _GetPixelsB8G8R8A8();
	else if (data->PixelFormatString == "PF_DXT5")
		pixels = _GetPixelsDXT5();
	else
		return Error("Unsupported pixel format '{0}'", data->PixelFormatString);
	if (pixels == nullptr)
		return Error("Failed to get pixels");

	//Bitmap = ImageHandler::ImageFromBytes(pixels, SizeX, SizeY, 4);
	//=> Needs another specialisation
	Bitmap = BitmapSource::Create(SizeX, SizeY, 96, 96, PixelFormats::Bgra32, nullptr, pixels, SizeX*4);
	if (Bitmap == nullptr)
		return Error("Error creating bitmap object");

	return true;
}

array<byte>^ FTexture2DMipMap::_GetPixelsB8G8R8A8()
{
	if (SizeX * SizeY * 4 != BulkData->ElementCount)
	{
		Error("B8G8R8A8: Dimension {0}x{1}x4 does NOT match element count {2}", SizeX, SizeY, BulkData->ElementCount);
		return nullptr;
	}

	return BulkData->Data;
}

array<byte>^ FTexture2DMipMap::_GetPixelsDXT5()
{
	if (SizeX * SizeY != BulkData->ElementCount)
	{
		Error("DXT5: Dimension {0}x{1} does NOT match element count {2}", SizeX, SizeY, BulkData->ElementCount);
		return nullptr;
	}

	pin_ptr<byte> in_pixels = &(BulkData->Data[0]);

	// Decompressing using "detex"
	array<byte>^ pixels = gcnew array<byte>(SizeX * SizeY * 4);//32bpp target BGRA
	pin_ptr<byte> out_pixels = &(pixels[0]);

	detex::detexTexture in_texture;
	in_texture.format           = detex::DETEX_TEXTURE_FORMAT_BC3;
	in_texture.data             = in_pixels;
	in_texture.width            = SizeX;
	in_texture.height           = SizeY;
	in_texture.width_in_blocks  = SizeX / 4;
	in_texture.height_in_blocks = SizeY / 4;
	if (!detex::detexDecompressTextureLinear(&in_texture, out_pixels, detex::DETEX_PIXEL_FORMAT_BGRA8))
	{
		Error("Failed to decompress: {0}", gcnew String(detex::detexGetErrorMessage()));
		return nullptr;
	}

	return pixels;
}


// 
//

FTexture2D::FTexture2D()
	: PlatformData(nullptr)
	, Mips(nullptr)
{ }

FTexture2D^ FTexture2D::Create(array<byte>^ asset)
{
	FTexture2D^ instance = gcnew FTexture2D();
	if (!instance->Read(asset))
		instance = nullptr;
	return instance;
}

bool FTexture2D::Read(IReader^ reader)
{
	if (!FObject::Read(reader))
		return Error("Texture2D: Unable to read object");
	// So far, so good, package summary and object with properties loaded without issues.
	// Now the hard part, decipher texture data -.-
	//

	// Strip flags - what to do with those?
	// - UTexture
	byte GlobalStripFlags = reader->ReadByte();
	byte ClassStripFlags  = reader->ReadByte();
	// - UTexture2
	GlobalStripFlags = reader->ReadByte();
	ClassStripFlags  = reader->ReadByte();

	IsCooking = reader->ReadInt() != 0;

	PlatformData = FPlatformData::Create(reader, Summary->Names);
	if (PlatformData == nullptr)
		return Error("Unable to read platform data");

	FirstMipToRead = reader->ReadInt();

	int count = reader->ReadInt();
	Mips = gcnew array<FTexture2DMipMap^>(count);
	for (int i = 0; i < count; ++i)
	{
		__int64 last_pos = reader->Pos;
		FTexture2DMipMap^ mip = FTexture2DMipMap::Create(reader, PlatformData);
		if (mip == nullptr)
			return Error("Unable to read mip map #{0} at {1:#,#0}", i, last_pos);
		Mips[i] = mip;
	}


	return true;
}

void FTexture2D::DumpTo(DumpToFileHelper^ d)
{
	FObject::DumpTo(d);

	d->AddLine("[FTexture2D]");
	d->AddLine("- IsCooking     : " + IsCooking);
	d->AddLine("- PlatformData  : " + (PlatformData != nullptr ? "" : "-"));
	if (PlatformData != nullptr)
	{
		d->Push();
		PlatformData->DumpTo(d);
		d->Pop();
	}
	d->AddLine("- FirstMipToRead: " + FirstMipToRead);
	d->AddLine("- Mips          : " + (Mips != nullptr ? Mips->Length.ToString() : "-"));
	if (Mips != nullptr)
	{
		d->Push();
		int index = 0;
		for each (FTexture2DMipMap^ mip in Mips)
		{
			d->AddLine("#" + index + ": ");
			++index;
			mip->DumpTo(d);
		}
		d->Pop();
	}
}

array<Size^>^ FTexture2D::GetDimensions()
{
	array<Size^>^ sizes = nullptr;

	if (Mips != nullptr)
	{
		sizes = gcnew array<Size^>(Mips->Length);

		int index = 0;
		for each (FTexture2DMipMap^ mip in Mips)
		{
			Size^ s = gcnew Size();
			s->SizeX = mip->SizeX;
			s->SizeY = mip->SizeY;
			sizes[index] = s;
			++index;
		}
	}

	return sizes;
}

BitmapSource^ FTexture2D::GetImage(int size_x, int size_y)
{
	FTexture2DMipMap^ mip = _FindMipWithDimension(size_x, size_y);
	if (mip != nullptr)
		return mip->Bitmap;
	return nullptr;
}

FTexture2DMipMap^ FTexture2D::_FindMipWithDimension(int x, int y)
{
	if (Mips != nullptr)
	{
		for each (FTexture2DMipMap^ mip in Mips)
		{
			if (mip->SizeX == x && mip->SizeY == y)
				return mip;
		}
	}

	return nullptr;
}


// 
//

FStringHashed::FStringHashed::FStringHashed()
	: Value(nullptr)
	, Hash(0)
{ }

bool FStringHashed::Read(IReader^ reader)
{
	Value = SafeReadString(reader);
	if (!Value)
		Error("FStringHashed: Error reading string value");
	Hash = reader->ReadUInt();

	return true;
}

void FStringHashed::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine(ToString());
}
		
String^ FStringHashed::ToString()
{
	//if (VERBOSITY)
	//	return String::Format("[FStringHashed] ({0:X8}) {1}", Hash, Value);
	//else
		return Value;
}

FStringHashed::operator String^()
{
	return Value;
}


// 
//

FAssetDataTag::FAssetDataTag()
	: Name(nullptr)
	, StringValue(nullptr)
	, BinaryValue(nullptr)
{ }

bool FAssetDataTag::Read(IReader^ reader, array<FStringHashed^>^ names)
{
	Name = FName<array<FStringHashed^>>::Create(reader, names);

	int size = reader->ReadInt();
	if (size >= 0)
	{
		reader->Seek(-4, IReader::Positioning::Relative);
		StringValue = SafeReadString(reader);
	}
	else
	{
		// Seems like UTF-16 being stored, but with some extra control-alike chars
		// (analysis postponed, real origin isn't important for now)
		BinaryValue = reader->ReadBytes((-size)*2);
	}

	return true;
}

void FAssetDataTag::DumpTo(DumpToFileHelper^ d)
{
	//if (VERBOSITY)
	//{
	//	d->AddLine("[FAssetDataTag]");
	//	d->AddLine(" - Name       : " + Name);
	//	if (StringValue) 
	//		d->AddLine(" - StringValue: " + StringValue);
	//	else if (BinaryValue) 
	//		d->AddLine(" - BinaryValue: " + BinaryValue->Length.ToString() + " bytes");
	//}
	//else
	{
		String^ s = String::Format("- {0,-30} : ", Name->Name);
		if (StringValue)
			s += StringValue;
		else if (BinaryValue) 
			s += "[Binary:" + BinaryValue->Length.ToString() + "]";
		else
			s += "NO DATA!";
		d->AddLine(s);
	}
}


// 
//

FAssetData::FAssetData()
	: ObjectPath(nullptr)
	, PackagePath(nullptr)
	, AssetClass(nullptr)
	, PackageName(nullptr)
	, AssetName(nullptr)
	, Tags(nullptr)
	, ChunkIds(nullptr)
{ }

bool FAssetData::Read(IReader^ reader, array<FStringHashed^>^ names)
{
	ObjectPath  = FName<array<FStringHashed^>>::Create(reader, names);
	PackagePath = FName<array<FStringHashed^>>::Create(reader, names);
	AssetClass  = FName<array<FStringHashed^>>::Create(reader, names);
	PackageName = FName<array<FStringHashed^>>::Create(reader, names);
	AssetName   = FName<array<FStringHashed^>>::Create(reader, names);

	// FAssetDataTagMap
	int count = reader->ReadInt();
	if (count < 0)
		Error("FAssetData: Invalid count {0} for name map", count);
	Tags = gcnew array<FAssetDataTag^>(count);
	for (int i = 0; i < count; ++i)
	{
		Tags[i] = FAssetDataTag::Create(reader, names);
		if (!Tags[i])
			Error("FAssetData: Tags: Error reading tag #{0}", i);
	}

	// Chunk id's
	count = reader->ReadInt();
	if (count < 0)
		Error("FAssetData: Invalid count {0} for chunk id map", count);
	ChunkIds = reader->ReadInts(count);
	if (!ChunkIds)
		Error("FAssetData: Error reading chunk id array");

	PackageFlags = reader->ReadUInt();

	return true;
}

void FAssetData::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[FAssetData]");
	d->AddLine(" - ObjectPath  : " + ObjectPath->Name);
	d->AddLine(" - PackagePath : " + PackagePath->Name);
	d->AddLine(" - AssetClass  : " + AssetClass->Name);
	d->AddLine(" - PackageName : " + PackageName->Name);
	d->AddLine(" - AssetName   : " + AssetName->Name);

	// FAssetDataTagMap
	d->AddLine(" - Tags        : " + Tags->Length.ToString() + " entries");
	d->Push();
	for each (FAssetDataTag^ tag in Tags)
		tag->DumpTo(d);
	d->Pop();

	// Chunk id's
	d->AddLine(" - ChunkIds    : " + ChunkIds->Length.ToString() + " entries");
	d->Add("   - ", false);
	for each (int id in ChunkIds)
		d->Add(id.ToString() + ",", false);
	d->AddLine("");

	d->AddLine(" - PackageFlags: 0x" + PackageFlags.ToString("X8"));
}


// 
//

AssetRegistry::AssetRegistry()
	: Magic(nullptr)
	, NameMap(nullptr)
	, Assets(nullptr)
{ }

bool AssetRegistry::Read(IReader^ reader)
{
	Magic = FGuid::Create(reader);
	if (!Magic)
		Error("AssetRegistry: Unable to read magic");
	if (!MAGIC->Equals(Magic))
		Error("AssetRegistry: Invalid magic {0}, expected {1}", Magic->ToString(), MAGIC->ToString());

	Version = reader->ReadInt();
	if (Version > 6)
		Error("AssetRegistry: Found version {0} but supported only up to version 6", Version);

	// Get name map
	__int64 offset = reader->ReadLong();
	if (offset < 0 || offset >= reader->Size)
		Error("AssetRegistry: Invalid offset to name map: {0:X16}", offset);

	__int64 last_pos = reader->Pos;
	if (reader->Seek(offset, IReader::Positioning::Start) != offset)
		Error("AssetRegistry: Failed to seek to name table at {0:X16}", offset);

	int count = reader->ReadInt();
	if (count < 0)
		Error("AssetRegistry: Invalid count {0} for name map", count);
	NameMap = gcnew array<FStringHashed^>(count);
	for (int i = 0; i < count; ++i)
	{
		NameMap[i] = FStringHashed::Create(reader);
		if (!NameMap[i])
			Error("AssetRegistry: NameMap: Error reading string #{0}", i);
	}

	if (reader->Seek(last_pos, IReader::Positioning::Start) != last_pos)
		Error("AssetRegistry: Failed to seek back to last position {0:X16}", last_pos);

	// Get assets
	count = reader->ReadInt();
	if (count < 0)
		Error("AssetRegistry: Invalid count {0} for assets", count);
	Assets = gcnew array<FAssetData^>(count);
	for (int i = 0; i < count; ++i)
	{
		Assets[i] = FAssetData::Create(reader, NameMap);
		if (!Assets[i])
			Error("AssetRegistry: Assets: Error reading asset #{0}", i);
	}

	// Dependencies
	// F:\Epic Games\UE_4.22\Engine\Source\Runtime\AssetRegistry\Private\AssetRegistryState.cpp - line 815+
	count = reader->ReadInt();
	if (count < 0)
		Error("AssetRegistry: Invalid count {0} for dependencies", count);
	if (count > 0)
		Error("AssetRegistry: Unhandled dependencies found at {0:X16}", reader->Pos);

	// Package data
	// F:\Epic Games\UE_4.22\Engine\Source\Runtime\AssetRegistry\Private\AssetRegistryState.cpp - line 894+
	count = reader->ReadInt();
	if (count < 0)
		Error("AssetRegistry: Invalid count {0} for package data", count);
	if (count > 0)
		Error("AssetRegistry: Unhandled package data found at {0:X16}", reader->Pos);

	return true;
}

void AssetRegistry::DumpTo(DumpToFileHelper^ d)
{
	d->AddLine("[AssetRegistry]");
	d->AddLine(" - Magic  : " + Magic->ToString());
	d->AddLine(" - Version: " + Version.ToString());

	d->AddLine(" - NameMap: " + NameMap->Length.ToString() + " entries");
	//if (VERBOSITY)
	//	for each (FStringHashed^ name in NameMap)
	//		d->AddLine("   - " + name->ToString());

	d->AddLine(" - Assets : " + Assets->Length.ToString() + " entries");
	d->Push();
	for each (FAssetData^ asset in Assets)
		asset->DumpTo(d);
	d->Pop();

	d->AddLine(" - Dependencies: n/a");
	//d->AddLine(" - Dependencies: " + Dependencies->Length.ToString() + " entries");
	//d->Push();
	//for each (F...^ dep in Dependencies)
	//	dep->DumpTo(d);
	//d->Pop();

	d->AddLine(" - PackageData: n/a");
	//d->AddLine(" - PackageData: " + PackageData->Length.ToString() + " entries");
	//d->Push();
	//for each (F...^ data in PackageData)
	//	data->DumpTo(d);
	//d->Pop();
}


  }//namespace Structures
}//namespace PakHandler