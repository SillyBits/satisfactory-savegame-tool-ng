#pragma once
#pragma warning(disable : 4091)

using namespace System::Text;

using namespace CoreLib;

using namespace Reader;

namespace PakHandler
{
  namespace Structures
  {

	enum class PakVersion //TODO: Rename to fit UE
	{
		None = 0,
		V1   = 1, // Initial version
		V2   = 2, // Same as V1, but no timestamp
		V3   = 3, // Introduced compression and encryption
		V4   = 4, // Now with possibility to encrypt index too
		V5   = 5, // Allows for relative chunk offsets (would guess related to compression)
		V6   = 6, // Allows for deleting records
		V7   = 7, // New encryption key strategies
		V8   = 8, // Name-based compression methods // Used in 'Exp. CL#109370'
		MAX
	};


	enum class EngineVersion //TODO: Rename to fit UE
	{
		None  = -1,
		V4_0  =  0,	// Only 4.x supported for now
		V4_7  =  7,	
		V4_11 = 11,
		V4_14 = 14,
	};


	enum class PakCompression //TODO: Rename to fit UE
	{
		None       = 0x00,
		Zlib       = 0x01,
		BiasMemory = 0x10,
		BiasSpeed  = 0x20,
	};


	enum class PakFlags //TODO: Rename to fit UE
	{
		None      = 0x00, // ./.
		Encrypted = 0x01, // V3++
		Deleted   = 0x02, // V6++
	};


	enum class EBulkDataFlags : int
	{
		None							= 0x0000, // 0,       // Empty flag set
		PayloadAtEndOfFile				= 0x0001, // 1 << 0,  // If set, payload is stored at the end of the file and not inline
		SerializeCompressed				= 0x0002, // 1 << 1,  // Flag to check if either compression mode is specified
		SerializeCompressedZLIB			= 0x0002, // 1 << 1,  // If set, payload should be [un]compressed using ZLIB during serialization
		ForceSingleElementSerialization	= 0x0004, // 1 << 2,  // Force usage of SerializeElement over bulk serialization
		SingleUse						= 0x0008, // 1 << 3,  // Bulk data is only used once at runtime in the game
		Unused							= 0x0020, // 1 << 5,  // Bulk data won't be used and doesn't need to be loaded
		ForceInlinePayload				= 0x0040, // 1 << 6,  // Forces the payload to be saved inline, regardless of its size
		ForceStreamPayload				= 0x0080, // 1 << 7,  // Forces the payload to be always streamed, regardless of its size 
		PayloadInSeperateFile			= 0x0100, // 1 << 8,  // If set, payload is stored in a .upack file alongside the uasset
		SerializeCompressedBitWindow	= 0x0200, // 1 << 9,  // DEPRECATED: If set, payload is compressed using platform specific bit window
		Force_NOT_InlinePayload			= 0x0400, // 1 << 10, // There is a new default to inline unless you opt out
		OptionalPayload					= 0x0800, // 1 << 11, // This payload is optional and may not be on device
		MemoryMappedPayload				= 0x1000, // 1 << 12, // This payload will be memory mapped, this requires alignment, no compression etc
		Size64Bit						= 0x2000, // 1 << 13, // Bulk data size is 64 bits long
	};


	public ref struct Size
	{
		int SizeX;
		int SizeY;
	};


	// Helpers to ease exitting
	static bool Error(String^ msg, ...array<Object^>^ params) { Log::Error(msg, params); return false; }
	static bool Warning(String^ msg, ...array<Object^>^ params) { Log::Warning(msg, params); return false; }


	// Helpers to ease loading
	inline static String^ SafeReadString(IReader^ reader)
	{
		str^ s = reader->ReadString();
		return s ? s->ToString() : nullptr;
	}


	template<typename _Type>
	public ref class ObjectBaseT abstract 
	{
	protected:
		typedef ObjectBaseT<_Type> base;

	public:
		ObjectBaseT()
		{ }

		static _Type^ Create(array<byte>^ asset)
		{
			_Type^ instance = gcnew _Type();
			if (!instance->Read(asset))
				instance = nullptr;
			return instance;
		}

		static _Type^ Create(IReader^ reader)
		{
			_Type^ instance = gcnew _Type();
			if (!instance->Read(reader))
				instance = nullptr;
			return instance;
		}

		virtual bool Read(array<byte>^ asset)
		{
			// Get a memory reader on this blob
			pin_ptr<byte> pinned = &asset[0];
			MemoryReader^ reader = gcnew MemoryReader(pinned, asset->Length, nullptr);
			if (reader != nullptr)
			{
				// Try to form a valid object
				try
				{
					return Read(reader);
				}
				catch (Exception^ exc)
				{
					if (VERBOSITY)
					{
						Type^ type = _Type::typeid;
						Log::Error(String::Format("[PakHandler] Error reading object of type '{0}' ({1})", 
							type->Name, type->FullName), exc);
					}
				}
				finally
				{
					reader->Close();
					reader = nullptr;
				}
			}

			return false;
		}

		virtual bool Read(IReader^ reader) = 0;

		virtual void DumpTo(DumpToFileHelper^ d) = 0;

	protected:
		// Helpers to ease exitting
		property static String^ TypeName { String^ get() { return (_Type::typeid)->Name; } }
		static bool Error(String^ msg, ...array<Object^>^ params) { Log::Error(TypeName + ": " + msg, params); return false; }
		static bool Warning(String^ msg, ...array<Object^>^ params) { Log::Warning(TypeName + ": " + msg, params); return false; }

	};


	template<typename _Type, typename _Param>
	public ref class ObjectBaseParamT abstract 
	{
	protected:
		typedef ObjectBaseParamT<_Type, _Param> base;

	public:
		ObjectBaseParamT()
		{ }

		static _Type^ Create(IReader^ reader, _Param param)
		{
			_Type^ instance = gcnew _Type();
			if (!instance->Read(reader, param))
				instance = nullptr;
			return instance;
		}

		virtual bool Read(IReader^ reader, _Param param) = 0;

		virtual void DumpTo(DumpToFileHelper^ d) = 0;

	protected:
		// Helpers to ease exitting
		property static String^ TypeName { String^ get() { return (_Type::typeid)->Name; } }
		static bool Error(String^ msg, ...array<Object^>^ params) { Log::Error(TypeName + ": " + msg, params); return false; }
		static bool Warning(String^ msg, ...array<Object^>^ params) { Log::Warning(TypeName + ": " + msg, params); return false; }

	};


	public ref class Footer : public ObjectBaseT<Footer> //TODO: Rename to fit UE
	{
	public:
		const static dword MAGIC = 0x5A6F12E1;

		dword        Magic;
		PakVersion   Version;
		__int64      IndexOffset;
		__int64      IndexSize;
		array<byte>^ Sha1;


		Footer();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;

	protected:
		bool _TryReadUE421(IReader^ reader);
		bool _TryReadUE422(IReader^ reader);
	};


	public ref class IndexEntry : public ObjectBaseT<IndexEntry> //TODO: Rename to fit UE
	{
	public:
		PakVersion      Version;
		String^         Filename;
		__int64         Offset;                 // Points to a FPakEntry structure, NOT actual content directly!
		__int64         CompressedSize;
		__int64         UncompressedSize;
		PakCompression  CompressionMethod;      // Up to V7, replaced by "CompressionMethodIndex" in V8
		array<byte>^    Sha1;
		__int64         Timestamp;              // V1 only
		array<__int64>^ CompressedBlocks;       // V3++
		__int32         CompressedBlockSize;    // V3++
		byte            Flags;                  // V3++
		byte            CompressionMethodIndex; // V8++


		property bool Encrypted  { bool get() { return (Flags & (byte)(int)PakFlags::Encrypted) != 0; } }; // V3++
		property bool Deleted    { bool get() { return (Flags & (byte)(int)PakFlags::Deleted  ) != 0; } }; // V6++
		property bool Compressed { bool get() { return CompressionMethod != PakCompression::None; } }; // V3++


		IndexEntry(PakVersion version);

		static IndexEntry^ Create(IReader^ reader, PakVersion version);

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;

	protected:

		bool ReadV1(IReader^ reader);
		bool ReadV2(IReader^ reader);
		bool ReadV3(IReader^ reader);
		bool ReadV4(IReader^ reader);
		bool ReadV5(IReader^ reader);
		bool ReadV6(IReader^ reader);
		bool ReadV7(IReader^ reader);
		bool ReadV8(IReader^ reader);
	};


	public ref class IndexCollection : public List<IndexEntry^> //TODO: Rename to fit UE
	{
	public:
		IndexCollection();

		IndexEntry^ Find(String^ filename);
	};


	public ref class FPakEntry //TODO: Rename to fit UE
	{
	public:
		IndexEntry^     Index;
		qword           Offset;
		qword           CompressedSize;
		qword           UncompressedSize;
		PakCompression  CompressionMethod;      // Up to V7, replaced by "CompressionMethodIndex" in V8
		byte            CompressionMethodIndex; // V8++
		__int64         Timestamp;              // V1 only
		array<byte>^    Sha1;
		array<__int64>^ CompressedBlocks;       // V3++
		byte            Flags;                  // V3++
		__int32         CompressedBlockSize;    // V3++
		__int64         RealOffset;             // Computed: Pos after last header byte was read


		property bool Encrypted  { bool get() { return (Flags & (byte)(int)PakFlags::Encrypted) != 0; } }; // V3++
		property bool Deleted    { bool get() { return (Flags & (byte)(int)PakFlags::Deleted  ) != 0; } }; // V6++
		property bool Compressed { bool get() { return CompressionMethod != PakCompression::None; } }; // V3++


		FPakEntry(IndexEntry^ index);

		static FPakEntry^ Create(IReader^ reader, IndexEntry^ index);

		bool Read(IReader^ reader);

		array<byte>^ ReadData(IReader^ reader);

		void DumpTo(DumpToFileHelper^ d);
	};


	public ref class FGuid : public ObjectBaseT<FGuid>
	{
	public:
		dword A,B,C,D;


		FGuid();
		FGuid(dword a, dword b, dword c, dword d);

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;

		String^ ToString() override;

		bool Equals(FGuid^ right);
	};


	template<typename _StringMap>
	public ref class FName : public ObjectBaseParamT<FName<_StringMap>, _StringMap^>
	{
	public:
		__int32 Index;
		__int32 ExtraIndex;
		String^ Name;


		FName()
			: base()
			, Name(nullptr)
		{ }

		bool Read(IReader^ reader, _StringMap^ names) override
		{
			Index      = reader->ReadInt();
			ExtraIndex = reader->ReadInt();

			try
			{
				if (Index < 0)
				{
					//TODO: Find meaning for those negative indices
					if (ExtraIndex == 0)
						Name = names[-Index];//Name = String::Format("({0})", Index);
					else
						Name = String::Format("({0})_({1})", names[-Index]/*Index*/, ExtraIndex - 1);
				}
				else
				{
					if (ExtraIndex == 0)
						Name = names[Index];
					else
						Name = String::Format("{0}_{1}", names[Index], ExtraIndex - 1);
				}
			}
			catch (Exception^ exc)
			{
				Log::Error(String::Format("FName: Error reading {0}/{1} from names", Index, ExtraIndex), exc);
				return false;
			}

			return true;
		}

		void DumpTo(DumpToFileHelper^ d) override
		{
			d->AddLine("[FName]");
			d->AddLine("- Index     : " + Index);
			d->AddLine("- ExtraIndex: " + ExtraIndex);
			d->AddLine("- Name      : " + Name);
		}

		String^ ToString() override
		{
			return "[FName] " + Name;
		}
	};


	public ref class FGenerationInfo : public ObjectBaseT<FGenerationInfo> //TODO: Rename to fit UE
	{
	public:
		static bool IsUE3 = false;

		__int32 ExportCount;
		__int32 NameCount;
		__int32 NetObjectCount; // UE3 only


		FGenerationInfo();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FEngineVersion : public ObjectBaseT<FEngineVersion>
	{
	public:
		word    Major;
		word    Minor;
		word    Patch;
		__int32 Changelist;
		String^ Branch;


		FEngineVersion();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FCompressedChunk : public ObjectBaseT<FCompressedChunk> //TODO: Rename to fit UE
	{
	public:
		__int32	UncompressedOffset;
		__int32	UncompressedSize;
		__int32	CompressedOffset;
		__int32	CompressedSize;


		FCompressedChunk();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FObjectExport : public ObjectBaseParamT<FObjectExport, array<String^>^> //TODO: Rename to fit UE
	{
	public:
		__int32                ClassIndex;
		__int32                SuperIndex;
		__int32                TemplateIndex;
		__int32                PackageIndex;
		FName<array<String^>>^ ObjectName;
	//	__int32                Architecture;//TODO:
		__int32                ObjectFlags;
		__int32                SerialSize;
		__int32                SerialOffset;
		bool                   ForcedExport;
		bool                   NotForClients;
		bool                   NotForServers;
	//	array<int>^            NetObjects;//TODO:
		FGuid^                 Guid;
		__int32                PackageFlags;
		bool                   NotForEditorGame;
		bool                   IsAsset;
		__int32                FirstExportDependency;
		__int32                SerializationBeforeSerializationDependencies;
		__int32                CreateBeforeSerializationDependencies;
		__int32                SerializationBeforeCreateDependencies;
		__int32                CreateBeforeCreateDependencies;


		FObjectExport();

		bool Read(IReader^ reader, array<String^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FObjectImport : public ObjectBaseParamT<FObjectImport, array<String^>^> //TODO: Rename to fit UE
	{
	public:
		FName<array<String^>>^ ClassPackage;
		FName<array<String^>>^ ClassName;
		__int32                PackageIndex;
		FName<array<String^>>^ ObjectName;


		FObjectImport();

		bool Read(IReader^ reader, array<String^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FObjectDepends : public ObjectBaseT<FObjectDepends> //TODO: Rename to fit UE
	{
	public:
		array<__int32>^ Dependencies;


		FObjectDepends();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FPreloadDepends : public ObjectBaseParamT<FPreloadDepends, array<String^>^> //TODO: Rename to fit UE
	{
	public:
	//	array<__int32>^ Dependencies;


		FPreloadDepends();

		bool Read(IReader^ reader, array<String^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FPackageFileSummary : public ObjectBaseT<FPackageFileSummary>
	{
	public:
		const static dword MAGIC = 0x9E2A83C1;

		dword                     Magic;
		__int32                   LegacyVersion;
		EngineVersion             Engine;
		__int32                   VersionUE3;      // Not used
		__int32                   Version;         // Only lower 16bits used
		__int32                   LicenseeVersion; // Only lower 16bits used
	//	array<FCustomVersion^>^   CustomVersions;  //TODO:
		__int32                   HeaderSize;
		String^                   Group;           // Group this package relates to
		__int32                   Flags;
		__int32                   NamesCount;
		__int32                   NamesOffset;
		__int32                   GatherableTextDataCount;
		__int32                   GatherableTextDataOffset;
		__int32                   ExportCount;
		__int32                   ExportOffset;
		__int32                   ImportCount;
		__int32                   ImportOffset;
		__int32                   DependsOffset;
		__int32                   StringAssetReferencesCount;
		__int32                   StringAssetReferencesOffset;
		__int32                   SearchableNamesOffset;
		__int32                   ThumbnailTableOffset;
		FGuid^                    Guid;
		array<FGenerationInfo^>^  Generations;
		FEngineVersion^           EngineVersion;
		FEngineVersion^           CompatibleEngineVersion;
		__int32                   CompressionFlags;
		array<FCompressedChunk^>^ CompressedChunks;
		dword                     PackageSource; // A 32bit hash? => FHash class?
		__int32                   AssetRegistryDataOffset;
		qword                     BulkDataStartOffset; // Offset to end magic
		__int32                   WorldTileInfoDataOffset;
		__int32                   ChunkIDsCount;
		array<__int32>^           ChunkIDs;
		__int32                   PreloadDependencyCount;
		__int32                   PreloadDependencyOffset;
		//^^^^^ end of serialized data
		array<String^>^           Names;
		array<FObjectExport^>^    Exports;
		array<FObjectImport^>^    Imports;
		array<FObjectDepends^>^   Depends;
	//	array<?>^                 AssetRegistryData;
		array<__int32>^           PreloadDependencyIndices; // <0: Import map, >0: Export map
		array<FPreloadDepends^>^  PreloadDependencies;


		property bool IsUnversioned { bool get() { return Version == 0 && LicenseeVersion == 0; } }


		FPackageFileSummary();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FPropertyTag : public ObjectBaseParamT<FPropertyTag, array<String^>^>
	{
	public:
		FName<array<String^>>^ Name;
		FName<array<String^>>^ PropType;
		__int32                DataSize;
		__int32                ArrayIndex;
		FName<array<String^>>^ StructName;
		FGuid^                 StructGuid;
		array<byte>^           StructValue;
		FName<array<String^>>^ EnumName;
		FName<array<String^>>^ EnumValue;
		int                    BoolValue;
		bool                   HasPropertyGuid;
		FGuid^                 PropertyGuid;


		property bool IsEmpty { bool get() { return Name == nullptr || Name->Name == "None"; } }


		FPropertyTag();

		bool Read(IReader^ reader, array<String^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FObject : public ObjectBaseT<FObject>
	{
	public:
		FPackageFileSummary^ Summary;
		List<FPropertyTag^>^ Properties;
		bool                 HasSerializedGuid;
		FGuid^               SerializedGuid;
		__int64              Length; // Computed: Length of all data read
		__int64              Offset; // Computed: Offset where data ends


		FObject();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FPlatformData : public ObjectBaseParamT<FPlatformData, array<String^>^>
	{
	public:
		FName<array<String^>>^ PixelFormat;
		__int64                SkippedOffset;
		__int32                SizeX;
		__int32                SizeY;
		__int32                NumSlices;
		String^                PixelFormatString;


		FPlatformData();

		bool Read(IReader^ reader, array<String^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FBulkData : public ObjectBaseParamT<FBulkData, __int64>
	{
	public:
		dword	     BulkDataFlags;
		__int64      ElementCount;
		__int64      SizeOnDisk;
		__int64      OffsetInFile;
		array<byte>^ Data;


		FBulkData();

		bool Read(IReader^ reader, __int64 offset) override;

		void DumpTo(DumpToFileHelper^ d) override;

		void ClearData();
	};


	ref class FTexture2D;

	public ref class FTexture2DMipMap : public ObjectBaseParamT<FTexture2DMipMap, FTexture2D^>
	{
	public:
		bool          IsCooked;
		FBulkData^    BulkData;
		__int32       SizeX;
		__int32       SizeY;
		__int32       SizeZ;
		BitmapSource^ Bitmap;


		FTexture2DMipMap();

		bool Read(IReader^ reader, FTexture2D^ texture) override;

		void DumpTo(DumpToFileHelper^ d) override;

	protected:
		bool _CreateBitmap(FTexture2D^ texture);

		array<byte>^ _GetPixelsB8G8R8A8();
		array<byte>^ _GetPixelsDXT5();
	};


	public ref class FTexture2D : public FObject //TODO: Rename to fit UE
	{
	public:
		bool                      IsCooking;
		FPlatformData^            PlatformData;
		__int32                   FirstMipToRead;
		array<FTexture2DMipMap^>^ Mips;
	//	FBulkData^                BulkData;


		FTexture2D();

		static FTexture2D^ Create(array<byte>^ asset);

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;

		array<Size^>^ GetDimensions();

		BitmapSource^ GetImage(int size_x, int size_y);

	protected:
		FTexture2DMipMap^ _FindMipWithDimension(int x, int y);
	};


	public ref class FStringHashed : public ObjectBaseT<FStringHashed>
	{
	public:
		String^ Value;
		dword   Hash;


		FStringHashed();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
		
		String^ ToString() override;

		operator String^();
	};


	public ref class FAssetDataTag : public ObjectBaseParamT<FAssetDataTag, array<FStringHashed^>^>
	{
	public:
		FName<array<FStringHashed^>>^ Name;
		String^                       StringValue;
		array<byte>^                  BinaryValue;


		FAssetDataTag();

		bool Read(IReader^ reader, array<FStringHashed^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class FAssetData : public ObjectBaseParamT<FAssetData, array<FStringHashed^>^>
	{
	public:
		FName<array<FStringHashed^>>^ ObjectPath;
		FName<array<FStringHashed^>>^ PackagePath;
		FName<array<FStringHashed^>>^ AssetClass;
		FName<array<FStringHashed^>>^ PackageName;
		FName<array<FStringHashed^>>^ AssetName;
		array<FAssetDataTag^>^        Tags;
		array<int>^                   ChunkIds;
		dword                         PackageFlags;


		FAssetData();

		bool Read(IReader^ reader, array<FStringHashed^>^ names) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};


	public ref class AssetRegistry : public ObjectBaseT<AssetRegistry>
	{
	public:
		static FGuid^ MAGIC = gcnew FGuid(0x717f9ee7, 0xe9b0493a, 0x88b39132, 0x1b388107);

		FGuid^                 Magic;
		__int32                Version;
		array<FStringHashed^>^ NameMap;
		array<FAssetData^>^    Assets;


		AssetRegistry();

		bool Read(IReader^ reader) override;

		void DumpTo(DumpToFileHelper^ d) override;
	};

  }
}
