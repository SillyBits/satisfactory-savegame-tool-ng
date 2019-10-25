#pragma once
#pragma warning(disable : 4091)

using namespace System::Reflection;
using namespace System::Text;

using namespace CoreLib;

using namespace Reader;
using namespace Writer;


/*
 * TODO:
 *
 * - Eliminate shadows like _Inner, _Missing and _EntityLength, else will
 *   need quite some efforts in regards to adding new elements
 *   - _Inner: "Type" actually stored in .Value, but what if empty? (e.g. lists)
 *
 */

//#define VALIDATE_SIZE
//#define VALIDATE_LENGTH


namespace Savegame
{
  namespace Properties
  {

	// Forward declarations needed
	ref class Property;
	ref class ValueProperty;
#ifdef EXPERIMENTAL
	template<typename _Parent>
	static Property^ ReadPrivateData(_Parent^ parent, [out] int UsedUp);
#endif


	// Our class factory to generate instances based on content read from save game
	static public ref class PropertyFactory
	{
	public:
		static Property^ Construct(str^ type_name, Property^ parent)
		{
			return Construct(type_name->ToString(), parent);
		}
		static Property^ Construct(String^ type_name, Property^ parent)
		{
			if (Cons.Count == 0)
				Create();

			if (!Cons.ContainsKey(type_name))
				return nullptr;

			ConstructorInfo^ ci = Cons[type_name];
			array<Object^>^ params = { parent };
			return (Property^) ci->Invoke(params);
		}

		static bool IsKnown(str^ type_name)
		{
			return IsKnown(type_name->ToString());
		}
		static bool IsKnown(String^ type_name)
		{
			return Cons.ContainsKey(type_name);
		}

	protected:
		// Caches all Property-based classes in assembly and allows for constructing those with .Invoke(Object[])
		static void Create()
		{
			if (VERBOSITY)
				Log::Debug("Discovering all available property types ...");

			Assembly^ ass = Assembly::GetCallingAssembly();
			if (VERBOSITY)
				Log::Debug("(in " + ass->FullName + ")");

			// Signature of constructor were looking for
			//array<Type^>^ cons = { Property::typeid, str::typeid, int::typeid, int::typeid, Object::typeid };
			array<Type^>^ cons = { Property::typeid };

			IEnumerable<TypeInfo^>^ types = ass->DefinedTypes;
			IEnumerator<TypeInfo^>^ iter = types->GetEnumerator();

			while (iter->MoveNext())
			{
				TypeInfo^ ti = iter->Current;

				if (ti->IsSubclassOf(Property::typeid))
				{
					ConstructorInfo^ ci = ti->GetConstructor(cons);
					if (ci)
					{
						Cons.Add(ti->Name, ci);
						if (VERBOSITY)
							Log::Debug("- {0} ({1})", ti->Name, ti->FullName);
					}
				}
			}

			if (VERBOSITY)
				Log::Debug("... discovered a total of {0} property types", Cons.Count);
		}

		static Dictionary<String^, ConstructorInfo^> Cons;
	};


	// Used to mark all members which are to be 'published' by 'Keys'
	public ref class Publish : Attribute
	{
	public:
		Publish() { }

		// Used to retrieve a list of all published members
		typedef Dictionary<String^, MemberInfo^> Published;
		static Published^ Retrieve(Object^ prop)
		{
			return Retrieve(prop->GetType());
		}
		static Published^ Retrieve(Type^ t)
		{
			if (VERBOSITY)
				Log::Debug("Discovering all published members for '{0}' ...", t);

			Published^ published = gcnew Published();

			array<MemberInfo^>^ members = t->GetMembers();

			for (int index=0; index < members->Length; ++index)
			{
				MemberInfo^ mi = (MemberInfo^) members->GetValue(index);
				IEnumerable<CustomAttributeData^>^ attrs = mi->CustomAttributes;
				IEnumerator<CustomAttributeData^>^ iter = attrs->GetEnumerator();

				while (iter->MoveNext())
				{
					CustomAttributeData^ cad = iter->Current;

					if (cad->AttributeType == Publish::typeid)
					{
						if (!published->ContainsKey(mi->Name))
						{
							if (VERBOSITY)
								Log::Debug("- " + mi);
							published->Add(mi->Name, mi);
						}
						else
						{
							// Member with this name added already 
							// (mostly on .Value with its type changing)
							// So we do replace the inherited one,
							// or skip this if its the inherited
							if (published[mi->Name]->DeclaringType != t)
							{
								// Inherited was stored earlier -> replace
								published[mi->Name] = mi;
							}
						}
					}
				}

			}

			if (VERBOSITY)
				Log::Debug("... discovered a total of {0} property types", published->Count);

			return published;
		}
	};


	public ref class UnknownPropertyException : Exception
	{
	public:
		String^ PropertyType;
		__int64 ErrorPos;

		UnknownPropertyException(String^ msg, String^ type_name, __int64 error_pos)
			: Exception(msg)
			, PropertyType(type_name)
			, ErrorPos(error_pos)
		{ }
	};


	// The base accessor, all properties must be subclasses!
	public ref class Property abstract
	{
	public:
		Property^ Parent;

		/*[Publish]*/ String^ TypeName;//TODO: Use a property instead to reduce memory usage
		/*[Publish]*/ List<String^>^ Errors = nullptr; //TODO: Place this somewhere else


		Property(Property^ parent)
			: Parent(parent)
		{
			TypeName = this->GetType()->Name;
		}


		typedef Publish::Published::KeyCollection Keys;
		virtual Keys^ GetKeys()
		{
			return _GetKeys(nullptr);
		}

		typedef Dictionary<String^, Object^> Childs;
		virtual Childs^ GetChilds()
		{
			return _GetChilds(nullptr);
		}


		// Denotes overall size of property as stored in save
		virtual int GetSize() abstract;

		// Denotes a property's length stored in save, used with ValueProperty::Length
		virtual int GetLength() abstract;


		virtual Property^ Read(IReader^ reader) abstract;


		virtual void Write(IWriter^ writer) abstract;


		static void CheckNullByte(IReader^ reader)
		{
			byte b = reader->ReadByte();
			if (b != 0)
				throw gcnew ReadException(reader, String::Format("NULL byte expected but found {0}", b));
		}

		static void CheckNullInt(IReader^ reader)
		{
			int i = reader->ReadInt();
			if (i != 0)
				throw gcnew ReadException(reader, String::Format("NULL int expected but found {0}", i));
		}


		// Walk up the chain returning top-most property
		property Property^ Root { Property^ get() {
			Property^ curr = this;
			while (curr->Parent)
				curr = curr->Parent;
			return curr;
		} }


		// Used to store error info discovered by validator
		//TODO: Move those somewhere else?
		property bool HasErrors 
		{ 
			bool get() { return (Errors != nullptr && Errors->Count > 0); }
		}
		void AddError(String^ err) 
		{ 
			if (Errors == nullptr)
				Errors = gcnew List<String^>();
			Errors->Add(err); 
		}


		property static bool DeepAnalysis 
		{ 
			bool get() { return _DeepAnalysis; }
			void set(bool enable) { _DeepAnalysis = enable; }
		}


		String^ ToString() override { return "[" + TypeName + "]"; }


		ref class ReadException : Exception
		{
		public:
			ReadException(IReader^ reader, String^ msg)
				: Exception(String::Format("Reader({0}|{1}): {2}", reader->Name, reader->PrevPos, msg))
			{ }
		};

		ref class WriteException : Exception
		{
		public:
			WriteException(IWriter^ writer, String^ msg)
				: Exception(String::Format("Writer({0}|{1}): {2}", writer->Name, writer->PrevPos, msg))
			{ }
		};


	protected:
		static bool _DeepAnalysis = false;

		static Dictionary<String^, Publish::Published^> _keys;

		Keys^ _GetKeys(List<String^>^ excludeList)
		{
			Publish::Published^ published;
			if (!_keys.ContainsKey(TypeName))
			{
				published = Publish::Retrieve(this);
				_keys.Add(TypeName, published);
			}
			else
			{
				published = _keys[TypeName];
			}

			// Normal case is to have no exclusions
			if (excludeList == nullptr)
				return published->Keys;

			// Build a different list
			Publish::Published^ new_published = gcnew Publish::Published();

			Publish::Published::Enumerator^ e = published->GetEnumerator();
			while (e->MoveNext())
			{
				KeyValuePair<String^, MemberInfo^>^ kv = e->Current;
				if (!excludeList->Contains(kv->Key))
					new_published->Add(kv->Key, kv->Value);
			}

			return new_published->Keys;
		}

		virtual Childs^ _GetChilds(List<String^>^ excludeList)
		{
			Publish::Published^ published;
			if (!_keys.ContainsKey(TypeName))
			{
				published = Publish::Retrieve(this);
				_keys.Add(TypeName, published);
			}
			else
			{
				published = _keys[TypeName];
			}

			Childs^ childs = gcnew Childs();

			Publish::Published::Enumerator^ e = published->GetEnumerator();
			while (e->MoveNext())
			{
				KeyValuePair<String^, MemberInfo^>^ kv = e->Current;

				if (excludeList != nullptr && excludeList->Contains(kv->Key))
					continue;
				// Normal case is to have no exclusions

				Object^ value;
				switch (kv->Value->MemberType)
				{
				case MemberTypes::Field:
					value = ((FieldInfo^)kv->Value)->GetValue(this);
					break;
				case MemberTypes::Property:
					value = ((PropertyInfo^)kv->Value)->GetValue(this);
					break;
				case MemberTypes::Method:
					value = ((MethodInfo^)kv->Value)->Invoke(this, nullptr);
					break;
				default:
					throw gcnew ArgumentOutOfRangeException(
						String::Format("{0}: Invalid type '{1}' for member {2}", 
							this->GetType(), kv->Value->MemberType, kv->Key));
				}

				childs->Add(kv->Key, value);
			}

			return childs;
		}


#if defined(VALIDATE_SIZE) || defined(VALIDATE_LENGTH)
	public:
		void    __Begin(IReader^ reader) { __StartPos = reader->Pos; }
		void    __End  (IReader^ reader) { __EndPos   = reader->Pos; __Length = (int)(__EndPos - __StartPos); }
		__int64 __StartPos;
		__int64 __EndPos;
		int     __Length;
	#define BEGIN_READ	Property::__Begin(reader);
	#define END_READ	Property::__End(reader);
#else
	#define BEGIN_READ
	#define END_READ
#endif
	};


	// Macros to ease with implementing all those types
	#define CLS_(name,base)		public ref class name : base { public: name(Property^ parent) : base(parent) { }
	#define CLS(name)			CLS_(name,Property)
	#define CLS_END				};

	// For publishing a properties member so they will show up in GetKeys/GetChilds
	#define PUB(name,type)		[Publish] type name;
	#define PUB_b(name)			PUB(name,byte)
	#define PUB_i(name)			PUB(name,__int32)
	#define PUB_l(name)			PUB(name,__int64)
	#define PUB_f(name)			PUB(name,float)
	#define PUB_s(name)			PUB(name,str^)
	#define PUB_o(name)			PUB(name,Object^)
	#define PUB_p(name)			PUB(name,ValueProperty^)
	#define PUB_ab(name)		PUB(name,ByteArray^)

	// To calculate a properties size in save
	#define SIZE				int GetSize() override { int size = 0;
	#define SIZE_(base)			int GetSize() override { int size = base::GetSize();
#ifdef VALIDATE_SIZE
	#define SIZE_CHECK_(a,b)	{ if (a) SizeCheck(#a, a, #b, b, this, __StartPos); }
	#define SIZE_CHECK			SIZE_CHECK_(__Length,size)
	static void SizeCheck(String^ a, int A, String^ b, int B, Object^ obj, __int64 p)
	{
		if (A < B)
			Log::Warning("{0:X8} | {1}:{2:X8} << {3}:{4:X8} in {5}", p, a, A, b, B, obj->ToString());
		else if (A > B)
			Log::Error  ("{0:X8} | {1}:{2:X8} >> {3}:{4:X8} in {5}", p, a, A, b, B, obj->ToString());
	}
#else
	#define SIZE_CHECK_(a,b)
	#define SIZE_CHECK
#endif
	#define SIZE_END			return size; }

	// To calculate a properties stored length, used with ValueProperty::Length
	#define LENGTH				int GetLength() override { int size = 0;
	#define LENGTH_(base)		int GetLength() override { int size = base::GetLength();
	#define LENGTH_END			return size; }

	// Both calc. size and length above do share same macros
	// (besides [SIZE|LEN]_p as those have to call different methods)
	#define ADD(s)              { size += (s); }
	#define ADD_b(name)			ADD(sizeof(byte))
	#define ADD_i(name)			ADD(sizeof(__int32))
	#define ADD_l(name)			ADD(sizeof(__int64))
	#define ADD_f(name)			ADD(sizeof(float))
	#define ADD_s(name)			ADD(str::GetRawLength(name))
	#define SIZE_p(name)		{ if (name) ADD((name)->GetSize()) }
	#define LEN_p(name)			{ if (name) ADD((name)->GetLength()) }
	#define ADD_a(name,type)	{ if (name) ADD(((array<type>^)name)->Length*sizeof(type)) }
	#define ADD_ab(name)		{ if (name) ADD((name)->Length) }

	// Handling for reading/writing a property
	#define READ				Property^ Read(IReader^ reader) override { BEGIN_READ
	#define READ_END			END_READ; return this; }
	#define WRITE				void Write(IWriter^ writer) override {
	#define WRITE_END			}

	// Human-readable string conversion (e.g. for exporting)
	#define STR(s)				String^ ToString() override { return s; };
	#define STR_(s)				STR("[" + TypeName + "] " + s)


	// Most basic (valued) property of all
	public ref class ValueProperty : Property
	{
	public:
		PUB_s(Name)
		PUB_i(Length)
		PUB_i(Index)
		PUB_o(Value)

		ValueProperty(Property^ parent)
			: ValueProperty(parent, nullptr, 0, 0, nullptr)
		{ }

		ValueProperty(Property^ parent, str^ name, int length, int index, Object^ value)
			: Property(parent)
			, Name(name)
			, Length(length)
			, Index(index)
			, Value(value)
		{ }


		static int GetSize(ValueProperty^ prop)
		{
			int size = 0;
			ADD_s(prop->Name)
			ADD(4 + prop->TypeName->Length + 1)
			ADD_i(prop->Length)
			ADD_i(prop->Index)
			//ADD_o(prop->Value) -> Done by derived class
			return size;
		}

		SIZE
			ADD(GetSize(this))
		SIZE_END


		static int GetLength(ValueProperty^ prop) 
		{
#ifdef VALIDATE_LENGTH
			Log::Warning("{0:X8} | Length override missing for {1}", prop->__StartPos, prop->ToString());
#endif
			return 0;
		}

		LENGTH
			ADD(GetLength(this))
		LENGTH_END


		static ValueProperty^ Read(IReader^ reader, Property^ parent)
		{
			__int64 last = reader->Pos;

			str^ name = reader->ReadString();
			if (name == "None")
				return nullptr;

			str^ type = reader->ReadString();
			int length = reader->ReadInt();
			int index = reader->ReadInt();

			//Accessor^ prop = AccessorFactory::ConstructProperty(type, parent, name, length, index, nullptr);
			Property^ acc = PropertyFactory::Construct(type, parent);
			if (acc == nullptr)
			{
				String^ msg = String::Format("Unknown type '{0}' at pos {1}", type, last);
				if (!str::IsNullOrEmpty(type))
					throw gcnew UnknownPropertyException(msg, type->ToString(), last);
				else
					throw gcnew ReadException(reader, msg);
			}

			ValueProperty^ prop = (ValueProperty^)acc;

			prop->Name = name;
			prop->Length = length;
			prop->Index = index;

			return (ValueProperty^) prop->Read(reader);
		}

		READ
			Read(reader, this);
		READ_END


		static void Write(IWriter^ writer, ValueProperty^ prop)
		{
			__int64 last = writer->Pos;

			if (prop == nullptr)
				throw gcnew WriteException(writer, "Empty property");

			writer->Write(prop->Name);

			// TypeName is always Ascii, so this inline-conversion will do,
			// no need to bother IWriter in regards to this special case.
			ByteArray^ chars = Encoding::ASCII->GetBytes(prop->TypeName);
			writer->Write((int)(chars->Length + 1));
			writer->Write(chars);
			writer->Write((byte)0);

#ifdef VALIDATE_LENGTH
			int new_length = prop->GetLength();
			if (new_length != prop->Length)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in {3}", 
					prop->__StartPos, prop->Length, new_length, prop->ToString());
			prop->Length = new_length;
#else
			prop->Length = prop->GetLength();
#endif
			writer->Write(prop->Length);

			writer->Write(prop->Index);

			prop->Write(writer);
		}

		WRITE
			Write(writer, this);
		WRITE_END


		String^ ToString() override { return "[" + TypeName + "] " + (Name ? Name : str::Statics::empty); };


		static str^ NONE = gcnew str((char*)"None");

	};

	public ref class Properties : List<Property^> { };


	#pragma region PropertyList
	// Multiple properties as array
	CLS(PropertyList)
		PUB(Value,Properties^)
		SIZE
			ADD(PropertyList::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			for each (ValueProperty^ prop in Value)
			{
				ADD(ValueProperty::GetSize(prop))
				SIZE_p(prop)
			}
			ADD_s(ValueProperty::NONE)
		LENGTH_END
		READ
			Value = gcnew Properties;
			while (true)
			{
				ValueProperty^ prop = ValueProperty::Read(reader, this);
				if (prop == nullptr)
					break;
				Value->Add(prop);
			}
		READ_END
		WRITE
			for each (ValueProperty^ prop in Value)
				ValueProperty::Write(writer, prop);
			writer->Write(ValueProperty::NONE);
		WRITE_END
		STR(String::Format("[{0}].Value[{1}]", TypeName, Value->Count))
	CLS_END
	#pragma endregion

	#pragma region Simple types

	CLS_(BoolProperty, ValueProperty)
		SIZE
			ADD_b(Value)
			ADD_b(NullByte)
			SIZE_CHECK
		SIZE_END
		LENGTH
			// Always a 0 bytes
		LENGTH_END
		READ
			Value = reader->ReadByte();
			CheckNullByte(reader);
		READ_END
		WRITE
			writer->Write((byte)Value);
			writer->Write((byte)0);
		WRITE_END
	CLS_END

	CLS_(ByteProperty, ValueProperty)
		PUB(Unknown,str^)
		SIZE
			ADD_s(Unknown)
			ADD_b(NullByte)
			ADD(ByteProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			if (*Unknown == "None")
				ADD_b(Value)
			else
				ADD_s((str^)Value)
		LENGTH_END
		READ
			Unknown = reader->ReadString();
			CheckNullByte(reader);
			if (*Unknown == "None")
				Value = reader->ReadByte();
			else
				Value = reader->ReadString();
		READ_END
		WRITE
			writer->Write(Unknown);
			writer->Write((byte)0);
			if (*Unknown == "None")
				writer->Write((byte)Value);
			else
				writer->Write((str^)Value);
		WRITE_END
	CLS_END
	
	CLS_(IntProperty, ValueProperty)
		SIZE
			ADD_b(NullByte)
			ADD(IntProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_i(Value)// Always a 4 bytes
		LENGTH_END
		READ
			CheckNullByte(reader);
			Value = reader->ReadInt();
		READ_END
		WRITE
			writer->Write((byte)0);
			writer->Write((__int32)Value);
		WRITE_END
	CLS_END

	CLS_(FloatProperty, ValueProperty)
		SIZE
			ADD_b(NullByte)
			ADD(FloatProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_f(Value)// Always a 4 bytes
		LENGTH_END
		READ
			CheckNullByte(reader);
			Value = reader->ReadFloat();
		READ_END
		WRITE
			writer->Write((byte)0);
			writer->Write((float)Value);
		WRITE_END
	CLS_END
	
	CLS_(StrProperty, ValueProperty)
		SIZE
			ADD_b(NullByte)
			ADD(StrProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_s((str^)Value)
		LENGTH_END
		READ
			CheckNullByte(reader);
			Value = reader->ReadString();
		READ_END
		WRITE
			writer->Write((byte)0);
			writer->Write((str^)Value);
		WRITE_END
	CLS_END

	#pragma endregion

	#pragma region Complex types

	#pragma region Header
	CLS(Header)
		PUB_i(Type)
		PUB_i(SaveVersion)
		PUB_i(BuildVersion)
		PUB_s(MapName)
		PUB_s(MapOptions)
		PUB_s(SessionName)
		PUB_i(PlayDuration)
		PUB_l(SaveDateTime)
		PUB_b(Visibility)
		SIZE
			ADD(Header::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_i(Type)
			ADD_i(SaveVersion)
			ADD_i(BuildVersion)
			ADD_s(MapName)
			ADD_s(MapOptions)
			ADD_s(SessionName)
			ADD_i(PlayDuration)
			ADD_l(SaveDateTime)
			ADD_b(Visibility)
		LENGTH_END
		READ
			Type = reader->ReadInt();
			SaveVersion = reader->ReadInt();
			BuildVersion = reader->ReadInt();
			MapName = reader->ReadString();
			MapOptions = reader->ReadString();
			SessionName = reader->ReadString();
			PlayDuration = reader->ReadInt();// in seconds
			SaveDateTime = reader->ReadLong();
			/*
			to convert SaveDateTime to a unix timestamp use:
				saveDateSeconds = SaveDateTime / 10000000
				print(saveDateSeconds-62135596800)
			see https://stackoverflow.com/a/1628018
			*/
			// According to Goz3rr's loader, this byte is avail only with Version>=5?
			Visibility = reader->ReadByte();
		READ_END	
		WRITE
			writer->Write(Type);
			writer->Write(SaveVersion);
			writer->Write(BuildVersion);
			writer->Write(MapName);
			writer->Write(MapOptions);
			writer->Write(SessionName);
			writer->Write(PlayDuration);// in seconds
			writer->Write(SaveDateTime);
			// According to Goz3rr's loader, this byte is avail only with Version>=5?
			writer->Write(Visibility);
		WRITE_END	
	CLS_END
	#pragma endregion

	#pragma region Collected
	CLS(Collected) 
		//TODO: Find correct name, if any
		PUB_s(LevelName)
		PUB_s(PathName)
		SIZE
			ADD_s(LevelName)
			ADD_s(PathName)
			SIZE_CHECK
		SIZE_END
		LENGTH
			throw gcnew InvalidOperationException();
		LENGTH_END
		READ
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
		READ_END
		WRITE
			writer->Write(LevelName);
			writer->Write(PathName);
		WRITE_END
		STR_(PathName)
	CLS_END
	#pragma endregion

	#pragma region StructProperty
	CLS_(StructProperty,ValueProperty)
		PUB_ab(Unknown)
		bool IsArray;
		SIZE
			ADD_s(_Inner)
			ADD(17)//Unknown
			if (!IsArray)
			{
				SIZE_p((Property^)Value)
			}
			else
			{
				for each (Property^ prop in (Properties^)Value)
					SIZE_p(prop)
			}
			SIZE_CHECK
		SIZE_END
		LENGTH
			if (!IsArray)
			{
				LEN_p((Property^)Value)
			}
			else
			{
				for each (Property^ prop in (Properties^)Value)
					LEN_p(prop)
			}
		LENGTH_END
		READ
			__int64 last = reader->Pos;
			IsArray = false;
			_Inner = reader->ReadString();
			Property^ acc = PropertyFactory::Construct(_Inner, this);
			if (acc == nullptr)
			{
				String^ msg = String::Format("Unknown inner structure type '{0}' at pos {1}", _Inner, last);
				if (!str::IsNullOrEmpty(_Inner))
					throw gcnew UnknownPropertyException(msg, _Inner->ToString(), last);
				else
					throw gcnew ReadException(reader, msg);
			}
			Unknown = reader->ReadBytes(17);
			Value = acc->Read(reader);
		READ_END
		void ReadAsArray(IReader^ reader, int count)
		{
			IsArray = true;
			_Inner = reader->ReadString();
			Unknown = reader->ReadBytes(17);
			Properties^ props = gcnew Properties;
			for (int i = 0; i < count; ++i)
			{
				__int64 last = reader->Pos;
				Property^ acc = PropertyFactory::Construct(_Inner, this);
				if (acc == nullptr)
				{
					String^ msg = String::Format("Unknown inner structure type '{0}' at pos {1}", _Inner, last);
					if (!str::IsNullOrEmpty(_Inner))
						throw gcnew UnknownPropertyException(msg, _Inner->ToString(), last);
					else
						throw gcnew ReadException(reader, msg);
				}
				props->Add(acc->Read(reader));
			}
			Value = props;
		}
		WRITE
			if (IsArray)
				throw gcnew WriteException(writer, String::Format(
					"Expected IsArray=false while saving structure type '{0}'", _Inner));
#ifdef VALIDATE_LENGTH
			int new_length = GetLength();
			if (new_length != Length)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in struct {3}", 
					__StartPos, Length, new_length, ToString());
			Length = new_length;
#else
			Length = GetLength();
#endif
			writer->Write(_Inner);
			writer->Write(Unknown);
			dynamic_cast<Property^>(Value)->Write(writer);
		WRITE_END
		void WriteAsArray(IWriter^ writer)
		{
			if (!IsArray)
				throw gcnew WriteException(writer, String::Format(
					"Expected IsArray=true while saving structure type '{0}'", _Inner));
#ifdef VALIDATE_LENGTH
			int new_length = GetLength();
			if (new_length != Length)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in struct (as array) {3}", 
					__StartPos, Length, new_length, ToString());
			Length = new_length;
#else
			Length = GetLength();
#endif
			writer->Write(_Inner);
			writer->Write(Unknown);
			Properties^ props = (Properties^) Value;
			for each (Property^ prop in props)
				prop->Write(writer);
		}
	protected:
		str^ _Inner;
	CLS_END
	#pragma endregion

	#pragma region Vector
	CLS(Vector)
		PUB_f(X)
		PUB_f(Y)
		PUB_f(Z)
		SIZE
			ADD(Vector::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(sizeof(float)*3)
		LENGTH_END
		READ
			X = reader->ReadFloat();
			Y = reader->ReadFloat();
			Z = reader->ReadFloat();
		READ_END
		WRITE
			writer->Write(X);
			writer->Write(Y);
			writer->Write(Z);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region Rotator
	CLS_(Rotator,Vector)
	CLS_END
	#pragma endregion

	#pragma region Scale
	// 'Scale' is a pseudo-class and not contained, added for the 
	// validation step as different set of bounds must be used
	CLS_(Scale,Vector)
		static Scale^ FromVector(Vector^ v)
		{
			Savegame::Properties::Scale^ scale = gcnew Savegame::Properties::Scale(v->Parent);
			scale->X = v->X;
			scale->Y = v->Y;
			scale->Z = v->Z;
			return scale;
		}
	CLS_END
	#pragma endregion

	#pragma region Box
	CLS(Box)
		PUB_f(MinY)
		PUB_f(MinX)
		PUB_f(MinZ)
		PUB_f(MaxX)
		PUB_f(MaxY)
		PUB_f(MaxZ)
		PUB_b(IsValid)
		SIZE
			ADD(Box::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(sizeof(float)*6)
			ADD_b(IsValid)
		LENGTH_END
		READ
			MinX = reader->ReadFloat();
			MinY = reader->ReadFloat();
			MinZ = reader->ReadFloat();
			MaxX = reader->ReadFloat();
			MaxY = reader->ReadFloat();
			MaxZ = reader->ReadFloat();
			IsValid = reader->ReadByte();
		READ_END
		WRITE
			writer->Write(MinX);
			writer->Write(MinY);
			writer->Write(MinZ);
			writer->Write(MaxX);
			writer->Write(MaxY);
			writer->Write(MaxZ);
			writer->Write(IsValid);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region Color
	CLS(Color)
		// Even if stored as BGRA, we'll keep RGBA order here
		PUB_b(R)
		PUB_b(G)
		PUB_b(B)
		PUB_b(A)
		SIZE
			ADD(Color::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(sizeof(byte)*4)
		LENGTH_END
		READ
			B = reader->ReadByte();
			G = reader->ReadByte();
			R = reader->ReadByte();
			A = reader->ReadByte();
		READ_END
		WRITE
			writer->Write(B);
			writer->Write(G);
			writer->Write(R);
			writer->Write(A);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region LinearColor
	CLS(LinearColor)
		PUB_f(R)
		PUB_f(G)
		PUB_f(B)
		PUB_f(A)
		SIZE
			ADD(LinearColor::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(sizeof(float)*4)
		LENGTH_END
		READ
			R = reader->ReadFloat();
			G = reader->ReadFloat();
			B = reader->ReadFloat();
			A = reader->ReadFloat();
		READ_END
		WRITE
			writer->Write(R);
			writer->Write(G);
			writer->Write(B);
			writer->Write(A);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region Transform
	CLS_(Transform,PropertyList)
		Property^ Read(IReader^ reader) override
		{
			PropertyList::Read(reader);
			for (int i = 0; i < Value->Count; ++i)
			{
				ValueProperty^ prop = (ValueProperty^) Value[i];
				if (*(prop->Name) == "Scale3D")
				{
					prop->Value = Savegame::Properties::Scale::FromVector((Vector^)prop->Value);
					break;
				}
			}
			return this;
		}
	CLS_END
	#pragma endregion

	#pragma region Quat
	CLS(Quat)
		PUB_f(A)
		PUB_f(B)
		PUB_f(C)
		PUB_f(D)
		SIZE
			ADD(Quat::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(sizeof(float)*4)
		LENGTH_END
		READ
			A = reader->ReadFloat();
			B = reader->ReadFloat();
			C = reader->ReadFloat();
			D = reader->ReadFloat();
		READ_END
		WRITE
			writer->Write(A);
			writer->Write(B);
			writer->Write(C);
			writer->Write(D);
		WRITE_END
	CLS_END
	#pragma endregion

	CLS(Guid)
		PUB_ab(Value)
		SIZE
			ADD(16)
		SIZE_END
		LENGTH
			ADD(16)
		LENGTH_END
		READ
			Value = reader->ReadBytes(16);
		READ_END
		WRITE
			writer->Write(Value);
		WRITE_END
	CLS_END

	#pragma region InventoryItem
	CLS(InventoryItem)
		PUB_s(Unknown)
		PUB_s(ItemName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_p(Value)
		SIZE
			ADD(InventoryItem::GetLength())
			SIZE_p((ValueProperty^)Value)
			if (Value) ADD(ValueProperty::GetSize(Value))
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_s(Unknown)
			ADD_s(ItemName)
			ADD_s(LevelName)
			ADD_s(PathName)
		LENGTH_END
		READ
			Unknown = reader->ReadString();
			ItemName = reader->ReadString();
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			Value = ValueProperty::Read(reader, this);
		READ_END
		WRITE
			writer->Write(Unknown);
			writer->Write(ItemName);
			writer->Write(LevelName);
			writer->Write(PathName);
			ValueProperty::Write(writer, Value);
		WRITE_END
		STR_(ItemName)
	CLS_END
	#pragma endregion

	#pragma region ObjectProperty
	// Note that ObjectProperty is somewhat special with having
	// two different faces: w/ .Name + .Value and w/o those.
	// (depending on its 'context' when loaded)
	public ref class ObjectProperty : ValueProperty
	{
	public:
		ObjectProperty(Property^ parent) 
			: ObjectProperty(parent, true)
		{ }

		ObjectProperty(Property^ parent, bool has_nullbyte) 
			: ValueProperty(parent)
			, _has_nullbyte(has_nullbyte)
		{ }

		PUB_s(LevelName)
		PUB_s(PathName)

		SIZE
			ADD(GetSize(_has_nullbyte))
		SIZE_END
		int GetSize(bool null_check) 
		{
#ifdef VALIDATE_SIZE
			int size = GetLength(null_check);
			SIZE_CHECK
			return size;
#else
			return GetLength(null_check);
#endif
		}

		LENGTH
			ADD(GetLength(false))
		LENGTH_END
		int GetLength(bool null_check)
		{
			int size = 0;
			if (null_check)
				ADD_b(NullByte)
			ADD_s(LevelName)
			ADD_s(PathName)
			return size;
		}

		READ
			Read(reader, true);
		READ_END
		Property^ Read(IReader^ reader, bool null_check)
		{
			BEGIN_READ
			_has_nullbyte = null_check;
			if (null_check)
				CheckNullByte(reader);
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			END_READ
			return this;
		}

		WRITE
			Write(writer, _has_nullbyte);
		WRITE_END
		void Write(IWriter^ writer, bool add_null)
		{
			if (_has_nullbyte != add_null)
				Log::Error("{0:X8} | Invalid null byte state with object property {1}", writer->Pos, ToString());
			if (add_null)
				writer->Write((byte)0);
			writer->Write(LevelName);
			writer->Write(PathName);
		}

		STR_(PathName)

		Keys^ GetKeys() override
		{
			return Property::_GetKeys(_GetExcludes());
		}
		Childs^ GetChilds() override
		{
			return Property::_GetChilds(_GetExcludes());
		}

	protected:
		List<String^>^ _GetExcludes()
		{
			if (str::IsNullOrEmpty(Name))
			{
				// No name, so no value
				if (_excludes == nullptr)
				{
					_excludes = gcnew List<String^>();
					_excludes->Add("Name");
					_excludes->Add("Value");
				}
				return _excludes;
			}
			return nullptr;
		}

		static List<String^>^ _excludes = nullptr;
		bool _has_nullbyte;
	};
	#pragma endregion

	#pragma region ArrayProperty
	CLS_(ArrayProperty,ValueProperty)
		PUB_s(InnerType)
		SIZE
			ADD_s(InnerType)
			ADD_b(NullByte)
			ADD(ArrayProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			if (InnerType == "StructProperty")
			{
				ADD_i(count)
				StructProperty^ stru = (StructProperty^)Value;
				ADD_s(stru->Name)
				ADD_s(_type)
				ADD_i(stru->Length)
				ADD_i(stru->Index)
				ADD(stru->GetSize())
			}
			else if (InnerType == "ObjectProperty")
			{
				ADD_i(count)
				for each (ObjectProperty^ prop in (Properties^)Value)
					ADD(prop->GetSize(false))
			}
			else if (InnerType == "IntProperty")
			{
				ADD_i(count);
				ADD_a(Value, int)
			}
			else if (InnerType == "ByteProperty")
			{
				ADD_i(count)
				ADD_a(Value, byte);
			}
			else if (InnerType == "EnumProperty")
			{
				ADD_i(count)
				for each (str^ s in (List<str^>^)Value)
					ADD_s(s);
			}
			else if (InnerType == "StrProperty")
			{
				ADD_i(count)
				for each (str^ s in (List<str^>^)Value)
					ADD_s(s);
			}
			else
				throw gcnew Exception(String::Format("Unknown inner array type '{0}'", InnerType));
		LENGTH_END
		READ
			__int64 last_pos = reader->Pos;
			InnerType = reader->ReadString();
			CheckNullByte(reader);
			if (InnerType == "StructProperty")
			{
				int count = reader->ReadInt();
				str^ name = reader->ReadString();
				_type = reader->ReadString();
				//assert _type == self.InnerType
				int length = reader->ReadInt();
				int index = reader->ReadInt();
				StructProperty^ stru = gcnew StructProperty(this);
				stru->Name = name;
				stru->Length = length;
				stru->Index = index;
				stru->ReadAsArray(reader, count);
				Value = stru;
			}
			else if (InnerType == "ObjectProperty")
			{
				int count = reader->ReadInt();
				Properties^ objs = gcnew Properties;
				for (int i = 0; i < count; ++i)
				{
					ObjectProperty^ prop = gcnew ObjectProperty(this, false);
					objs->Add(prop->Read(reader, false));
				}
				Value = objs;
			}
			else if (InnerType == "IntProperty")
			{
				int count = reader->ReadInt();
				Value = reader->ReadInts(count);
			}
			else if (InnerType == "ByteProperty")
			{
				int count = reader->ReadInt();
				Value = reader->ReadBytes(count);
			}
			else if (InnerType == "EnumProperty")
			{
				int count = reader->ReadInt();
				List<str^>^ strings = gcnew List<str^>;
				for (int i = 0; i < count; ++i)
					strings->Add(reader->ReadString());
				Value = strings;
			}
			else if (InnerType == "StrProperty")
			{
				int count = reader->ReadInt();
				List<str^>^ strings = gcnew List<str^>;
				for (int i = 0; i < count; ++i)
					strings->Add(reader->ReadString());
				Value = strings;
			}
			else
			{
				String^ msg = String::Format("Unknown inner array type '{0}'", InnerType);
				if (!str::IsNullOrEmpty(InnerType))
					throw gcnew UnknownPropertyException(msg, InnerType->ToString(), last_pos);
				else
					throw gcnew ReadException(reader, msg);
			}
		READ_END
		WRITE
			writer->Write(InnerType);
			writer->Write((byte)0);
			if (InnerType == "StructProperty")
			{
				StructProperty^ stru = (StructProperty^) Value;
				Properties^ props = (Properties^) stru->Value;
				writer->Write((int)props->Count);
				writer->Write(stru->Name);
				writer->Write(InnerType);
#ifdef VALIDATE_LENGTH
				int new_length = stru->GetLength();
				if (new_length != stru->Length)
					Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in array type {3}", 
						stru->__StartPos, stru->Length, new_length, stru->ToString());
				stru->Length = new_length;
#else
				stru->Length = stru->GetLength();
#endif
				writer->Write(stru->Length);
				writer->Write(stru->Index);
				stru->WriteAsArray(writer);
			}
			else if (InnerType == "ObjectProperty")
			{
				Properties^ props = (Properties^) Value;
				writer->Write((int)props->Count);
				for each (Property^ prop in props)
					dynamic_cast<ObjectProperty^>(prop)->Write(writer, false);
			}
			else if (InnerType == "IntProperty")
			{
				array<__int32>^ arr = (array<__int32>^) Value;
				writer->Write((int)arr->Length);
				writer->Write(arr);
			}
			else if (InnerType == "ByteProperty")
			{
				ByteArray^ arr = (ByteArray^) Value;
				writer->Write((int)arr->Length);
				writer->Write(arr);
			}
			else if (InnerType == "EnumProperty")
			{
				List<str^>^ strings = (List<str^>^) Value;
				writer->Write((int)strings->Count);
				for each (str^ s in strings)
					writer->Write(s);
			}
			else if (InnerType == "StrProperty")
			{
				List<str^>^ strings = (List<str^>^) Value;
				writer->Write((int)strings->Count);
				for each (str^ s in strings)
					writer->Write(s);
			}
			else
				throw gcnew WriteException(writer, String::Format("Unknown inner array type '{0}'", InnerType));
		WRITE_END
	protected:
		str^ _type;
	CLS_END
	#pragma endregion

	#pragma region EnumProperty
	CLS_(EnumProperty,ValueProperty)
		PUB_s(EnumName)
		SIZE
			ADD_s(EnumName)
			ADD_b(NullByte)
			ADD(EnumProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_s((str^)Value)
		LENGTH_END
		READ
			EnumName = reader->ReadString();
			CheckNullByte(reader);
			Value = reader->ReadString();
		READ_END
		WRITE
			writer->Write(EnumName);
			writer->Write((byte)0);
			writer->Write((str^)Value);
		WRITE_END
		STR_(EnumName)
	CLS_END
	#pragma endregion

	#pragma region MapProperty
	CLS_(MapProperty,ValueProperty)
		ref class Entries : Dictionary<Object^, Object^> {};

		PUB_s(KeyType)
		PUB_s(ValueType)
		PUB(Value, Entries^)
		SIZE
			ADD_s(KeyType)
			ADD_s(ValueType)
			// Seems like only a single null byte here, remain might be an int32 -> Investigate why resp. what the real origin for those
			ADD_b(NullCheck)
			ADD(MapProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			// We've to take 4 of those 5 null bytes into account here -> Investigate why resp. what the real origin for those
			ADD(sizeof(byte)*4)
			ADD_i(count)
			for each (KeyValuePair<Object^, Object^>^ pair in (Entries^)Value)
			{
				if (*KeyType == "IntProperty")
					ADD_i(pair->Key)
				else if (*KeyType == "ObjectProperty")
					ADD(safe_cast<ObjectProperty^>(pair->Key)->GetSize(false))
				else
					throw gcnew Exception(String::Format("Unknown key type '{0}'", KeyType->ToString()));

				if (*ValueType == "ByteProperty")
					ADD_b(pair->Value)
				else if (*ValueType == "StructProperty")
					ADD(safe_cast<PropertyList^>(pair->Value)->GetSize())
				else
					throw gcnew Exception(String::Format("Unknown value type '{0}'", ValueType->ToString()));
			}
		LENGTH_END
		READ
			KeyType = reader->ReadString();
			ValueType = reader->ReadString();
			// Seems like only a single null byte here, remain might be an int32 -> Investigate why resp. what the real origin for those
			CheckNullByte(reader); 
			CheckNullByte(reader); CheckNullByte(reader); CheckNullByte(reader); CheckNullByte(reader);
			int count = reader->ReadInt();
			Value = gcnew Entries;
			for (int i = 0; i < count; ++i)
			{
				Object^ key;
				if (*KeyType == "IntProperty")
					key = reader->ReadInt();
				else if (*KeyType == "ObjectProperty")
					key = (gcnew ObjectProperty(this, false))->Read(reader, false);
				else
					throw gcnew ReadException(reader, String::Format("Unknown key type '{0}'", KeyType->ToString()));

				Object^ value;
				if (*ValueType == "ByteProperty")
					value = reader->ReadByte();
				else if (*ValueType == "StructProperty")
					value = (gcnew PropertyList(this))->Read(reader);
				else
					throw gcnew ReadException(reader, String::Format("Unknown value type '{0}'", ValueType->ToString()));

				Value->Add(key, value);
			}
		READ_END
		WRITE
			writer->Write(KeyType);
			writer->Write(ValueType);
			// Seems like only a single null byte here, remain might be an int32 -> Investigate why resp. what the real origin for those
			writer->Write((byte)0); 
			writer->Write((byte)0); writer->Write((byte)0); writer->Write((byte)0); writer->Write((byte)0);
			Entries^ entries = (Entries^) Value;
			writer->Write((int)entries->Count);
			//TODO: This might write in a different order. Investigate!
			for each (KeyValuePair<Object^, Object^>^ pair in entries)
			{
				if (*KeyType == "IntProperty")
					writer->Write((int)pair->Key);
				else if (*KeyType == "ObjectProperty")
					safe_cast<ObjectProperty^>(pair->Key)->Write(writer, false);
				else
					throw gcnew WriteException(writer, String::Format("Unknown key type '{0}'", KeyType->ToString()));

				if (*ValueType == "ByteProperty")
					writer->Write((byte)(pair->Value));
				else if (*ValueType == "StructProperty")
					safe_cast<PropertyList^>(pair->Value)->Write(writer);
				else
					throw gcnew WriteException(writer, String::Format("Unknown value type '{0}'", ValueType->ToString()));
			}
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region TextProperty
	CLS_(TextProperty,ValueProperty)
		PUB_ab(Unknown)
		SIZE
			ADD_b(NullByte)
			ADD(TextProperty::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD(13)
			ADD_s((str^)Value)
		LENGTH_END
		READ
			CheckNullByte(reader);
			Unknown = reader->ReadBytes(13);
			Value = reader->ReadString();
		READ_END
		WRITE
			writer->Write((byte)0);
			writer->Write(Unknown);
			writer->Write((str^)Value);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region RailroadTrackPosition
	CLS_(RailroadTrackPosition,ValueProperty)
		PUB_s(ClassName)
		PUB_s(PathName)
		PUB_f(Offset)
		PUB_f(Forward)
		SIZE
			ADD(RailroadTrackPosition::GetLength())
			SIZE_CHECK
		SIZE_END
		LENGTH
			ADD_s(ClassName)
			ADD_s(PathName)
			ADD_f(Offset)
			ADD_f(Forward)
		LENGTH_END
		READ
			ClassName = reader->ReadString();
			PathName = reader->ReadString();
			Offset = reader->ReadFloat();
			Forward = reader->ReadFloat();
		READ_END
		WRITE
			writer->Write(ClassName);
			writer->Write(PathName);
			writer->Write(Offset);
			writer->Write(Forward);
		WRITE_END
	CLS_END
	#pragma endregion

	#pragma region Simple derived types

	CLS_(NameProperty,StrProperty)
	CLS_END

	CLS_(RemovedInstanceArray,PropertyList)
	CLS_END

	CLS_(RemovedInstance,PropertyList)
	CLS_END

	CLS_(InventoryStack,PropertyList)
	CLS_END

	CLS_(PhaseCost, PropertyList)
	CLS_END

	CLS_(ItemAmount, PropertyList)
	CLS_END

	CLS_(ResearchTime,PropertyList)
	CLS_END

	CLS_(ResearchCost, PropertyList)
	CLS_END

	CLS_(CompletedResearch, PropertyList)
	CLS_END

	CLS_(ResearchRecipeReward, PropertyList)
	CLS_END

	CLS_(ItemFoundData, PropertyList)
	CLS_END

	CLS_(RecipeAmountStruct, PropertyList)
	CLS_END

	CLS_(MessageData, PropertyList)
	CLS_END

	CLS_(SplinePointData, PropertyList)
	CLS_END

	CLS_(SpawnData, PropertyList)
	CLS_END

	CLS_(FeetOffset, PropertyList)
	CLS_END

	CLS_(SplitterSortRule, PropertyList)
	CLS_END

	CLS_(SchematicCost, PropertyList)
	CLS_END

	CLS_(TimerHandle,PropertyList)
	CLS_END

	CLS_(TimeTableStop,PropertyList)
	CLS_END

	CLS_(TrainSimulationData,PropertyList)
	CLS_END

	CLS_(ProjectileData,PropertyList)
	CLS_END

	#pragma endregion

	#pragma endregion

	#pragma region Entities

	public ref class Entity : PropertyList
	{
	public:
		Entity(Property^ parent, str^ level_name, str^ path_name, Properties^ children)
			: PropertyList(parent)
			, LevelName(level_name)
			, PathName(path_name)
			, Children(children)
			, Unknown(0)
			, Missing(nullptr)
#ifdef EXPERIMENTAL
			, Private(nullptr)
			, MissingUsed(0)
#endif
		{ }

		PUB_s(LevelName)
		PUB_s(PathName)
		PUB(Children, Properties^)
		PUB_i(Unknown)
		PUB_ab(Missing)
#ifdef EXPERIMENTAL
		PUB(Private, Property^)
		int MissingUsed;
#endif

		SIZE
			ADD(Entity::GetLength())
			SIZE_CHECK_(__Length_Passed,size)
		SIZE_END

		LENGTH_(PropertyList)
			ADD_i(Unknown)
			ADD_ab(Missing)
		LENGTH_END

		Property^ Read(IReader^ reader, int length)
		{
#ifdef VALIDATE_SIZE
			__Length_Passed = length;
#endif

			__int64 last_pos = reader->Pos;
			PropertyList::Read(reader);
			//TODO: There is an extra 'int' following, investigate!
			// Not sure if this is valid for all elements which are of type
			// PropertyList. For now,  we will handle it only here
			// Might this be the same "int" discovered with entities below???
			Unknown = reader->ReadInt();
			__int64 bytes_read = reader->Pos - last_pos;
			if (bytes_read < 0)
				throw gcnew ReadException(reader, "Negative offset!");
			if (bytes_read != length)
				Missing = reader->ReadBytes((int)(length - bytes_read));
			return this;
		}

		void Write(IWriter^ writer, int length)
		{
#ifdef VALIDATE_LENGTH
			int new_length = Entity::GetLength();
			if (new_length != length)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in entity obj {3}", 
					__StartPos, length, new_length, ToString());
#endif

			__int64 last_pos = writer->Pos;
			PropertyList::Write(writer);
			//TODO: There is an extra 'int' following, investigate!
			// Not sure if this is valid for all elements which are of type
			// PropertyList. For now,  we will handle it only here
			// Might this be the same "int" discovered with entities below???
			writer->Write(Unknown);

			__int64 bytes_written = writer->Pos - last_pos;
			if (bytes_written < 0)
				throw gcnew WriteException(writer, "Negative offset!");
			__int64 delta = length - bytes_written;
			if (bytes_written != length && 
				(Missing == nullptr || (__int64)Missing->Length != delta))
				throw gcnew WriteException(writer, "Integrity error, expected .Missing!");
			if (Missing != nullptr)
				writer->Write(Missing);
		}

		STR_(PathName)

#ifdef VALIDATE_SIZE
		int __Length_Passed;
#endif
	};

	public ref class NamedEntity : Entity
	{
	public:
		ref class Name : Property
		{
		public:
			Name(Property^ parent) 
				: Property(parent) 
			{ }

			PUB_s(LevelName)
			PUB_s(PathName)

			SIZE
				ADD(Name::GetLength())
				SIZE_CHECK
			SIZE_END

			LENGTH
				ADD_s(LevelName)
				ADD_s(PathName)
			LENGTH_END

			READ
				LevelName = reader->ReadString();
				PathName = reader->ReadString();
			READ_END

			WRITE
				writer->Write(LevelName);
				writer->Write(PathName);
			WRITE_END
		};

		NamedEntity(Property^ parent, str^ level_name, str^ path_name, Properties^ children)
			: Entity(parent, level_name, path_name, children)
		{ }

		SIZE
			ADD(NamedEntity::GetLength())
			SIZE_CHECK_(__Length_Passed,size)
		SIZE_END

		LENGTH_(Entity)
			ADD_s(LevelName)
			ADD_s(PathName)
			ADD_i(count)
			for each (Name^ name in Children)
				ADD(name->GetSize())
		LENGTH_END

		Property^ Read(IReader^ reader, int length)
		{
			BEGIN_READ

#ifdef VALIDATE_SIZE
			__Length_Passed = length;
#endif

			__int64 last_pos = reader->Pos;
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			int count = reader->ReadInt();
			Children = gcnew Properties;
			for (int i = 0; i < count; ++i)
			{
				Name^ name = gcnew Name(this);
				Children->Add(name->Read(reader));
			}
			__int64 bytes_read = reader->Pos - last_pos;
			if (bytes_read < 0)
				throw gcnew ReadException(reader, "Negative offset!");
			//if (bytes_read != length)
			//	Missing = ReadBytes(reader, length - bytes_read);
			Entity::Read(reader, (int)(length - bytes_read));

			END_READ
			return this;
		}

		void Write(IWriter^ writer, int length)
		{
#ifdef VALIDATE_LENGTH
			int new_length = GetLength();
			if (new_length != length)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in named entity obj {3}", 
					__StartPos, length, new_length, ToString());
#endif

			__int64 last_pos = writer->Pos;
			writer->Write(LevelName);
			writer->Write(PathName);
			writer->Write((int)Children->Count);
			for each (Name^ name in Children)
				name->Write(writer);
			__int64 bytes_written = writer->Pos - last_pos;
			//TODO:
			if (bytes_written < 0)
				throw gcnew WriteException(writer, "Negative offset!");
			//if (bytes_read != length)
			//	Missing = ReadBytes(reader, length - bytes_read);
			// Even if length isn't needed for writing, 
			// better do some integrity checking there
			Entity::Write(writer, (int)(length - bytes_written));
		}

#ifdef VALIDATE_SIZE
		int __Length_Passed;
#endif
	};

	#pragma region Object
	CLS(Object)
		PUB_s(ClassName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_s(OuterPathName)
		PUB(EntityObj,Entity^)
		SIZE
			ADD_s(ClassName)
			ADD_s(LevelName)
			ADD_s(PathName)
			ADD_s(OuterPathName)
			SIZE_CHECK
			ADD_i(_EntityLength)
			SIZE_p(EntityObj)
			SIZE_CHECK_(_EntityLength,EntityObj->GetSize())
		SIZE_END
		LENGTH
			throw gcnew InvalidOperationException();
		LENGTH_END
		READ
			ClassName = reader->ReadString();
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			OuterPathName = reader->ReadString();
		READ_END
		Property^ ReadEntity(IReader^ reader)
		{
			_EntityLength = reader->ReadInt();
			Entity^ entity = gcnew Entity(this, nullptr, nullptr, nullptr);
			entity->Read(reader, _EntityLength);
			EntityObj = entity;

			// EXPERIMENTAL
#ifdef EXPERIMENTAL
			if (entity->Missing != nullptr && entity->Missing->Length > 0 && Property::DeepAnalysis)
			{
				try 
				{
					entity->Private = ReadPrivateData(this, entity->MissingUsed);
				}
				catch (Exception^ exc)
				{
					entity->Private = nullptr;
					entity->MissingUsed = 0;
					if (VERBOSITY)
						Log::Debug(String::Format("Error loading private data for entity '{0}'", PathName), exc);
				}
			}
#endif

			return this;
		}
		WRITE
			writer->Write(ClassName);
			writer->Write(LevelName);
			writer->Write(PathName);
			writer->Write(OuterPathName);
		WRITE_END
		void WriteEntity(IWriter^ writer)
		{
#ifdef VALIDATE_LENGTH
			int new_length = EntityObj->GetSize();
			if (new_length != _EntityLength)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in object's entity {3}", 
					EntityObj->__StartPos, _EntityLength, new_length, EntityObj->ToString());
			_EntityLength = new_length;
#else
			_EntityLength = EntityObj->GetSize();
#endif
			writer->Write(_EntityLength);
			EntityObj->Write(writer, _EntityLength);

			// EXPERIMENTAL
			//=> No need to save anything, enough to store Missing
		}
		STR_(ClassName)
	protected:
		int _EntityLength;
	CLS_END
	#pragma endregion

	#pragma region Actor
	CLS(Actor)
		PUB_s(ClassName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_i(NeedTransform)
		PUB(Rotation,Quat^)
		PUB(Translate,Vector^)
		PUB(Scale,Savegame::Properties::Scale^)
		PUB_i(WasPlacedInLevel)
		PUB(EntityObj,NamedEntity^)
		SIZE
			ADD_s(ClassName)
			ADD_s(LevelName)
			ADD_s(PathName)
			ADD_i(NeedTransform)
			SIZE_p(Rotation)
			SIZE_p(Translate)
			SIZE_p(Scale)
			ADD_i(WasPlacedInLevel)
			SIZE_CHECK
			ADD_i(_EntityLength)
			SIZE_p(EntityObj)
			SIZE_CHECK_(_EntityLength, EntityObj->GetSize())
		SIZE_END
		LENGTH
			throw gcnew InvalidOperationException();
		LENGTH_END
		READ
			ClassName = reader->ReadString();
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			NeedTransform = reader->ReadInt();
			Rotation = (Quat^) (gcnew Quat(this))->Read(reader);
			Translate = (Vector^) (gcnew Vector(this))->Read(reader);
			Scale = (Savegame::Properties::Scale^) (gcnew Savegame::Properties::Scale(this))->Read(reader);
			WasPlacedInLevel = reader->ReadInt();
		READ_END
		Property^ ReadEntity(IReader^ reader)
		{
			_EntityLength = reader->ReadInt();
			NamedEntity^ entity = gcnew NamedEntity(this, nullptr, nullptr, nullptr);
			entity->Read(reader, _EntityLength);
			EntityObj = entity;

			// EXPERIMENTAL
#ifdef EXPERIMENTAL
			if (entity->Missing != nullptr && entity->Missing->Length > 0 && Property::DeepAnalysis)
			{
				try 
				{
					entity->Private = ReadPrivateData(this, entity->MissingUsed);
				}
				catch (Exception^ exc)
				{
					entity->Private = nullptr;
					entity->MissingUsed = 0;
					if (VERBOSITY)
						Log::Debug(String::Format("Error loading private data for named entity '{0}'", PathName), exc);
				}
			}
#endif

			return this;
		}
		WRITE
			writer->Write(ClassName);
			writer->Write(LevelName);
			writer->Write(PathName);
			writer->Write(NeedTransform);
			Rotation->Write(writer);
			Translate->Write(writer);
			Scale->Write(writer);
			writer->Write(WasPlacedInLevel);
		WRITE_END
		void WriteEntity(IWriter^ writer)
		{
#ifdef VALIDATE_LENGTH
			int new_length = EntityObj->GetSize();
			if (new_length != _EntityLength)
				Log::Error("{0:X8} | Length mismatch: {1:X8} != {2:X8} in actor' entity {3}", 
					EntityObj->__StartPos, _EntityLength, new_length, EntityObj->ToString());
			_EntityLength = new_length;
#else
			_EntityLength = EntityObj->GetSize();
#endif
			writer->Write(_EntityLength);
			EntityObj->Write(writer, _EntityLength);

			// EXPERIMENTAL
			//=> No need to save anything, enough to store shadow _Missing
		}
		STR_(PathName)
	protected:
		int _EntityLength;
	CLS_END
	#pragma endregion

	#pragma endregion


#ifdef EXPERIMENTAL

	// Attribute used with private nodes which are using hard-coded counts for their contained data
	public ref class FixedCount : Attribute
	{
	public:
		int Count;

		FixedCount(int count) 
		{
			Count = count;
		}

		// Used to retrieve attribute instance, or null if type given has no such attribute
		static FixedCount^ Retrieve(String^ type_name)
		{
			return Retrieve(Type::GetType(type_name));
		}
		static FixedCount^ Retrieve(Type^ t)
		{
			IEnumerable<Object^>^ attrs = t->GetCustomAttributes(false);
			IEnumerator<Object^>^ iter = attrs->GetEnumerator();

			while (iter->MoveNext())
			{
				Object^ curr = iter->Current;
				if (curr->GetType() == FixedCount::typeid)
				{
					return (FixedCount^)curr;
				}
			}

			return nullptr;
		}
	};


	template<typename _Parent>
	static Property^ ReadPrivateData(_Parent^ parent, [out] int UsedUp)
	{
		/*
			* List of "something" ... no clues on what as seen only empty lists by now:
				/Game/FactoryGame/-Shared/Blueprint/BP_RailroadSubsystem.BP_RailroadSubsystem_C
				/Game/FactoryGame/-Shared/Blueprint/BP_GameMode.BP_GameMode_C
				/Game/FactoryGame/Buildable/Vehicle/Explorer/BP_Explorer.BP_Explorer_C

			* List of "Collected", every now and then with some extra data either before or after:
				/Game/FactoryGame/-Shared/Blueprint/BP_CircuitSubsystem.BP_CircuitSubsystem_C
				/Game/FactoryGame/-Shared/Blueprint/BP_GameState.BP_GameState_C

			* Conveyors do have their transported items stored, same with lifts:
				/Game/FactoryGame/Buildable/Factory/ConveyorBeltMk#/Build_ConveyorBeltMk#.Build_ConveyorBeltMk#_C
				/Game/FactoryGame/Buildable/Factory/ConveyorLiftMk#/Build_ConveyorLiftMk#.Build_ConveyorLiftMk#_C

			* Power lines will have EXACTLY two poles stored, or machine connectors:
				/Game/FactoryGame/Buildable/Factory/PowerLine/Build_PowerLine.Build_PowerLine_C
 	
			* Special data, might be some animation states with node names (e.g. hook, antenna, ...)
				/Game/FactoryGame/Buildable/Vehicle/Tractor/BP_Tractor.BP_Tractor_C
				/Game/FactoryGame/Buildable/Vehicle/Truck/BP_Truck.BP_Truck_C

			* Ignored: Yet no clues on whats contained (either unknown or no data yet)
				/Game/FactoryGame/Character/Player/BP_PlayerState.BP_PlayerState_C

			* Name guesses:
				./.
		*/

		UsedUp = 0;

		String^ classname = parent->ClassName->ToString();
		if (classname->StartsWith("/Game/FactoryGame/"))
		{
			/*
				Following exact same scheme:
					/Game/FactoryGame/<ItemType>/<ItemSubType0>[/<...N>/<TypeName>.<ClassName>
				with
					<ItemType>		<ItemSubType0>		<ItemSubType1>
					-Shared			Blueprint	
					Buildable		Factory				Conyevor[Belt|Lift]Mk# | PowerLine
									Vehicle				Tractor | Truck | Explorer
					Character		Player				./.
				and
					<ClassName> == <TypeName>"_C"
			*/

			Entity^ entity = dynamic_cast<Entity^>(parent->EntityObj);
			array<String^>^ classes = classname->Split('.');
			String^ type_name = classes[classes->Length - 1];

			if (!PropertyFactory::IsKnown(type_name))
			{
				if (VERBOSITY)
					Log::Debug("No match for private type '{0}' found, .Missing ignored", classname);
				return nullptr;
			}

			pin_ptr<byte> pinned = &(entity->Missing[0]);//(dynamic_cast<array<byte>^>(entity->Missing));
			MemoryReader^ reader = gcnew MemoryReader(pinned, entity->Missing->Length, nullptr);

			// Some structures have a hardcoded length not contained in stream
			String^ full_type_name = "Savegame.Properties." + type_name;
			FixedCount^ fixed_count = FixedCount::Retrieve(full_type_name);
			int count;
			if (fixed_count != nullptr)
				count = fixed_count->Count;
			else
				count = reader->ReadInt();

			PrivateData^ instance = gcnew PrivateData(entity);
			instance->Value = gcnew Properties();
			for (int i = 0; i < count; ++i)
			{
				Property^ prop = PropertyFactory::Construct(type_name, instance);
				instance->Value->Add(prop->Read(reader));
			}

			UsedUp = (int) reader->Pos;
			if (reader->Pos != reader->Size)
				Log::Debug("Still some dangling bytes, .Missing kept");

			reader = nullptr;

			if (VERBOSITY)
				Log::Debug("Injected private type for '{0}'", classname);

			return instance;
		}

		// (Yet) Unknown
		return nullptr;
	}


	//
	// Sub-data for entities
	//

	// 'PrivateData' is a pseudo-class and not contained, 
	// just added for UI to let it know origin of data
	CLS_(PrivateData,PropertyList)
		READ
		READ_END
		WRITE
			throw gcnew NotImplementedException();
		WRITE_END
	CLS_END


	// * /Game/FactoryGame/Character/Player/...

	//class BP_PlayerState_C(Accessor):
	//	pass #TODO: Data yet unknown -> Keep .Missing and create report


	//* /Game/FactoryGame/-Shared/Blueprint/...

	// Stores a list of circuit classes, e.g.
	//		.PathName = Persistent_Level:PersistentLevel.CircuitSubsystem.FGPowerCircuit_15
	// with .Index being the same as appended to .PathName (in this case =15)
	//
	// Those are the ones listed in
	//		/Script/FactoryGame/FGPowerCircuit/*
	CLS_(BP_CircuitSubsystem_C, Collected)
		PUB_i(Index)
		READ
			Index = reader->ReadInt();
			//LevelName = reader.readStr()
			//PathName = reader.readInt()
			return Collected::Read(reader);
		READ_END
		WRITE
			throw gcnew NotImplementedException();
		WRITE_END
	CLS_END

	// Contains path to player state which this tied to this game state, e.g.:
	//		.PathName = Persistent_Level:PersistentLevel.BP_PlayerState_C_0
	CLS_(BP_GameState_C, Collected)
	CLS_END

	//class BP_RailroadSubsystem_C(Accessor):
	//	pass #TODO: Yet no data avail to inspect -> Keep .Missing and create report

	//class BP_GameMode_C(Accessor):
	//	pass #TODO: Yet no data avail to inspect -> Keep .Missing and create report


	// * /Game/FactoryGame/Buildable/Factory/...

	// Describes an item on a belt:
	//		.PathName = /Game/FactoryGame/Resource/Parts/Fuel/Desc_Fuel.Desc_Fuel_C
	// with its offset along belt's "movement vector"(?):
	//		.Translate = [Vector] 0 / 0 / 300,8046000
	// (X+Y always empty?)
	CLS_(Build_ConveyorBelt, Property)
		PUB_i(Index)
		PUB_s(ItemName)
		PUB(Translate, Vector^)
		SIZE
		SIZE_END
		LENGTH
		LENGTH_END
		READ
			Index = reader->ReadInt();
			ItemName = reader->ReadString();
			Translate = (Vector^) (gcnew Vector(this))->Read(reader);
		READ_END
		WRITE
			throw gcnew NotImplementedException();
		WRITE_END
	CLS_END

	CLS_(Build_ConveyorBeltMk1_C, Build_ConveyorBelt)
	CLS_END

	CLS_(Build_ConveyorBeltMk2_C, Build_ConveyorBelt)
	CLS_END

	CLS_(Build_ConveyorBeltMk3_C, Build_ConveyorBelt)
	CLS_END

	CLS_(Build_ConveyorBeltMk4_C, Build_ConveyorBelt)
	CLS_END

	CLS_(Build_ConveyorBeltMk5_C, Build_ConveyorBelt)
	CLS_END

	CLS_(Build_ConveyorBeltMk6_C, Build_ConveyorBelt)
	CLS_END


	// Lift has exact same data layout as belts, but this might change
	// with future releases so an extra base class is used here
	// Describes an item on a lift:
	//		.PathName = /Game/FactoryGame/Resource/Parts/Fuel/Desc_Fuel.Desc_Fuel_C
	// with its offset along lift's "movement vector"(?):
	//		.Translate = [Vector] 0 / 0 / 300,8046000
	// (X+Y always empty?)
	CLS_(Build_ConveyorLift, Property)
		PUB_i(Index)
		PUB_s(ItemName)
		PUB(Translate, Vector^)
		SIZE
		SIZE_END
		LENGTH
		LENGTH_END
		READ
			Index = reader->ReadInt();
			ItemName = reader->ReadString();
			Translate = (Vector^) (gcnew Vector(this))->Read(reader);
		READ_END
		WRITE
			throw gcnew NotImplementedException();
		WRITE_END
	CLS_END

	CLS_(Build_ConveyorLiftMk1_C, Build_ConveyorLift)
	CLS_END

	CLS_(Build_ConveyorLiftMk2_C, Build_ConveyorLift)
	CLS_END

	CLS_(Build_ConveyorLiftMk3_C, Build_ConveyorLift)
	CLS_END

	CLS_(Build_ConveyorLiftMk4_C, Build_ConveyorLift)
	CLS_END

	CLS_(Build_ConveyorLiftMk5_C, Build_ConveyorLift)
	CLS_END

	CLS_(Build_ConveyorLiftMk6_C, Build_ConveyorLift)
	CLS_END


	// Contains exactly 2 connectors this line connects. 
	// Could be either power poles or machines, e.g.:
	//		.PathName = Persistent_Level:PersistentLevel.Build_PowerPoleMk1_C_960.PowerConnection
	// and
	//		.PathName = Persistent_Level:PersistentLevel.Build_PowerPoleMk1_C_935.PowerConnection
	// (There might be more connection "types" in future, e.g. logistical ones as with Factorio?)
	[FixedCount(2)]
	CLS_(Build_PowerLine_C, Collected)
	CLS_END


	// * /Game/FactoryGame/Buildable/Vehicle/...

	CLS_(BP_Vehicle, Property)
		PUB_s(Node)
		PUB_ab(Unknown)
		SIZE
		SIZE_END
		LENGTH
		LENGTH_END
		READ
			// Seems like some animation data?
			Node = reader->ReadString();
			//TODO: Crack those 53 bytes
			Unknown = reader->ReadBytes(53);
		READ_END
		WRITE
			throw gcnew NotImplementedException();
		WRITE_END
	CLS_END

	CLS_(BP_Tractor_C, BP_Vehicle)
	CLS_END

	CLS_(BP_Truck_C, BP_Vehicle)
	CLS_END

	CLS_(BP_Explorer_C, BP_Vehicle)
	CLS_END

#endif // EXPERIMENTAL

  }

}
