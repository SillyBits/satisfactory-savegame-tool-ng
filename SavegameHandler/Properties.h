#pragma once
#pragma warning(disable : 4091)

using namespace System::Reflection;

using namespace CoreLib;
using namespace Reader;

namespace Savegame
{
  namespace Properties
  {

	// Needed for PropertyFactory
	ref class Property;
	ref class ValueProperty;


	// Our class factory to generate instances based on content read from save game
	static public ref class PropertyFactory
	{
	public:
		//static TypeInfo^ Get(String^ type_name)
		//{
		//	if (Types.Count == 0)
		//		Create();
		//
		//	if (!Types.ContainsKey(type_name))
		//		return nullptr;
		//
		//	return Types[type_name];
		//}

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

	protected:
		// Caches all Property-based classes in assembly and allows for constructing those with .Invoke(Object[])
		static void Create()
		{
			Log::Debug("Discovering all available property types ...");

			Assembly^ ass = Assembly::GetCallingAssembly();
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
					// Not sure if we do need this
					//Types.Add(ti->Name, ti);

					ConstructorInfo^ ci = ti->GetConstructor(cons);
					if (ci)
					{
						Cons.Add(ti->Name, ci);
						Log::Debug("- {0} ({1})", ti->Name, ti->FullName);
					}
				}
			}

			Log::Debug("... discovered a total of {0} property types", Cons.Count);
		}

		//static Dictionary<String^, TypeInfo^> Types;
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
			Log::Debug("Discovering all published members for '{0}' ...", t);

			Published^ published = gcnew Published();

			array<MemberInfo^>^ members = t->GetMembers();

			for (int index=0; index < members->Length; ++index)
			{
				MemberInfo^ mi = (MemberInfo^) members->GetValue(index);
				//Debug::Write("? " + mi);

				IEnumerable<CustomAttributeData^>^ attrs = mi->CustomAttributes;
				IEnumerator<CustomAttributeData^>^ iter = attrs->GetEnumerator();

				while (iter->MoveNext())
				{
					CustomAttributeData^ cad = iter->Current;

					//Debug::Write("? ? " + cad);

					if (cad->AttributeType == Publish::typeid)
					{
						if (!published->ContainsKey(mi->Name))
						{
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

			Log::Debug("... discovered a total of {0} property types", published->Count);

			return published;
		}
	};


	// The base accessor, all properties must be subclasses!
	public ref class Property abstract
	{
	public:
		Property^ Parent;

		/*[Publish]*/ String^ TypeName;//TODO: Use a property instead to reduce memory usage
		/*[Publish]*/ List<String^>^ Errors = nullptr; //TODO:


		Property(Property^ parent)
			: Parent(parent)
		{
			TypeName = this->GetType()->Name;
		}


		typedef Publish::Published::KeyCollection Keys;
		Keys^ GetKeys()
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

			return published->Keys;
		}

		typedef Dictionary<String^, Object^> Childs;
		Childs^ GetChilds()
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

			Publish::Published::Enumerator iter = published->GetEnumerator();
			while (iter.MoveNext())
			{
				KeyValuePair<String^, MemberInfo^> pair = iter.Current;

				Object^ value;
				switch (pair.Value->MemberType)
				{
				case MemberTypes::Field:
					value = ((FieldInfo^)pair.Value)->GetValue(this);
					break;
				case MemberTypes::Property:
					value = ((PropertyInfo^)pair.Value)->GetValue(this);
					break;
				case MemberTypes::Method:
					value = ((MethodInfo^)pair.Value)->Invoke(this, nullptr);
					break;
				default:
					throw gcnew ArgumentOutOfRangeException(String::Format("{0}: Invalid type '{1}' for member {2}", 
						this->GetType(), pair.Value->MemberType, pair.Key));
				}

				childs->Add(pair.Key, value);
			}

			return childs;
		}


		virtual Property^ Read(IReader^ reader) abstract;


		static ByteArray^ ReadBytes(IReader^ reader, int count)
		{
			ByteArray^ bytes = gcnew ByteArray(count);
			pin_ptr<byte> p = &bytes[0];
			reader->ReadByte(p, count);
			return bytes;
		}

		static array<__int32>^ ReadInts(IReader^ reader, int count)
		{
			array<__int32>^ ints = gcnew array<__int32>(count);
			pin_ptr<__int32> p = &ints[0];
			reader->ReadInt(p, count);
			return ints;
		}

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


		String^ ToString() override { return "[" + TypeName + "]"; }


		ref class ReadException : Exception
		{
		public:
			ReadException(IReader^ reader, String^ msg)
				: Exception(String::Format("Reader({0}|{1}): {2}", reader->Name, reader->PrevPos, msg))
			{ }
		};


	protected:
		static Dictionary<String^, Publish::Published^> _keys;

	};


	// Macros to ease with implementing all those types
	#define CLS_(name,base)		public ref class name : base { public: name(Property^ parent) : base(parent) { }
	#define CLS(name)			CLS_(name,Property)
	#define CLS_END				};

	#define PUB(name,type)		[Publish] type name;
	#define PUB_b(name)			PUB(name,byte)
	#define PUB_i(name)			PUB(name,__int32)
	#define PUB_l(name)			PUB(name,__int64)
	#define PUB_f(name)			PUB(name,float)
	#define PUB_s(name)			PUB(name,str^)
	#define PUB_o(name)			PUB(name,Object^)
	#define PUB_p(name)			PUB(name,ValueProperty^)
	#define PUB_a(name,type)	PUB(name,array<type>^)
	#define PUB_ab(name)		PUB(name,ByteArray^)

	#define READ				Property^ Read(IReader^ reader) override {
	#define READ_END			return this; }

	#define STR(s)				String^ ToString() override { return s; };
	#define STR_(s)				STR("[" + TypeName + "] " + s)


	// Most basic property of all
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


		static ValueProperty^ Read(IReader^ reader, Property^ parent)
		{
			int last = reader->Pos;

			str^ name = reader->ReadString();
			if (name == "None")
				return nullptr;

			str^ type = reader->ReadString();
			int length = reader->ReadInt();
			int index = reader->ReadInt();

			//Accessor^ prop = AccessorFactory::ConstructProperty(type, parent, name, length, index, nullptr);
			Property^ acc = PropertyFactory::Construct(type, parent);
			if (acc == nullptr)
				throw gcnew ReadException(reader, String::Format("Unknown type '{0}'", type));

			ValueProperty^ prop = (ValueProperty^)acc;

			prop->Name = name;
			prop->Length = length;
			prop->Index = index;

			return (ValueProperty^) prop->Read(reader);
		}

		READ
			return Read(reader, this);
		READ_END

		//WRITE
		//	...
		//WRITE_END

		String^ ToString() override { return "[" + TypeName + "] " + (Name ? Name : str::Statics::empty); };

	/*
	class Property(Accessor):
		@staticmethod
		def raise_error(pos, msg):
			raise "ERROR at pos {:,d}: {}".format(pos, msg)
	*/

	CLS_END

	typedef List<Property^> Properties;


	// Multiple properties as array
	//public ref class PropertyList : Property
	CLS(PropertyList)
		PUB(Value,Properties^)
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
		STR(String::Format("[{0}].Value[0-{1}]", TypeName, Value->Count - 1))
	CLS_END


	//
	// Simple types
	//

	CLS_(BoolProperty, ValueProperty)
		PUB_b(Value)
		READ
			Value = reader->ReadByte();
			CheckNullByte(reader);
			return this;
		READ_END
	CLS_END

	CLS_(ByteProperty, ValueProperty)
		PUB(Unknown,str^)
		//PUB(Value,Object^)
		READ
			Unknown = reader->ReadString();
			CheckNullByte(reader);
			if (*Unknown == "None")
				Value = reader->ReadByte();
			else
				Value = reader->ReadString();
		READ_END
	CLS_END
	
	CLS_(IntProperty, ValueProperty)
		PUB_i(Value);
		READ
			CheckNullByte(reader);
			Value = reader->ReadInt();
			return this;
		READ_END
	CLS_END

	CLS_(FloatProperty, ValueProperty)
		PUB_f(Value)
		READ
			CheckNullByte(reader);
			Value = reader->ReadFloat();
			return this;
		READ_END
	CLS_END
	
	CLS_(StrProperty, ValueProperty)
		PUB_s(Value)
		READ
			CheckNullByte(reader);
			Value = reader->ReadString();
			return this;
		READ_END
	CLS_END


	//
	// Complex types
	//

	CLS(Header)
		PUB_i(Type)
		PUB_i(Version)
		PUB_i(BuildVersion)
		PUB_s(MapName)
		PUB_s(MapOptions)
		PUB_s(SessionName)
		PUB_i(PlayDuration)
		PUB_l(SaveDateTime)
		PUB_b(Visibility)
		READ
			Type = reader->ReadInt();
			Version = reader->ReadInt();
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
	CLS_END

	CLS(Collected) 
		//TODO: Find correct name, if any
		PUB_s(LevelName)
		PUB_s(PathName)
		READ
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
		READ_END
		STR_(PathName)
	CLS_END

	CLS_(StructProperty,ValueProperty)
		PUB_ab(Unknown)
		//PUB_o(Value)
		bool IsArray;
		READ
			IsArray = false;
			str^ inner = reader->ReadString();
			Property^ acc = PropertyFactory::Construct(inner, this);
			if (acc == nullptr)
				throw gcnew ReadException(reader, String::Format("Unknown inner structure type '{0}'", inner));
			Unknown = ReadBytes(reader, 17);
			Value = acc->Read(reader);
		READ_END
		void ReadAsArray(IReader^ reader, int count)
		{
			IsArray = true;
			str^ inner = reader->ReadString();
			Unknown = ReadBytes(reader, 17);
			Properties^ props = gcnew Properties;
			for (int i = 0; i < count; ++i)
			{
				Property^ acc = PropertyFactory::Construct(inner, this);
				if (acc == nullptr)
					throw gcnew ReadException(reader, String::Format("Unknown inner structure type '{0}'", inner));
				props->Add(acc->Read(reader));
			}
			Value = props;
		}
	CLS_END

	CLS(Vector)
		PUB_f(X)
		PUB_f(Y)
		PUB_f(Z)
		READ
			X = reader->ReadFloat();
			Y = reader->ReadFloat();
			Z = reader->ReadFloat();
		READ_END
	CLS_END

	CLS_(Rotator,Vector)
	CLS_END

	// 'Scale' is a pseudo-class and not contained, added for the 
	// validation step as different set of bounds must be used
	CLS_(Scale,Vector)
		static Scale^ FromVector(Vector^ v)
		{
			Scale^ scale = gcnew Scale(v->Parent);
			scale->X = v->X;
			scale->Y = v->Y;
			scale->Z = v->Z;
			return scale;
		}
	CLS_END

	CLS(Box)
		PUB_f(MinY)
		PUB_f(MinX)
		PUB_f(MinZ)
		PUB_f(MaxX)
		PUB_f(MaxY)
		PUB_f(MaxZ)
		PUB_b(IsValid)
		READ
			MinX = reader->ReadFloat();
			MinY = reader->ReadFloat();
			MinZ = reader->ReadFloat();
			MaxX = reader->ReadFloat();
			MaxY = reader->ReadFloat();
			MaxZ = reader->ReadFloat();
			IsValid = reader->ReadByte();
		READ_END
	CLS_END

	CLS(Color)
		PUB_b(R)
		PUB_b(G)
		PUB_b(B)
		PUB_b(A)
		READ
			R = reader->ReadByte();
			G = reader->ReadByte();
			B = reader->ReadByte();
			A = reader->ReadByte();
		READ_END
	CLS_END

	CLS(LinearColor)
		PUB_f(R)
		PUB_f(G)
		PUB_f(B)
		PUB_f(A)
		READ
			R = reader->ReadFloat();
			G = reader->ReadFloat();
			B = reader->ReadFloat();
			A = reader->ReadFloat();
		READ_END
	CLS_END

	CLS_(Transform,PropertyList)
		READ
			PropertyList^ obj = (PropertyList^) PropertyList::Read(reader);
			for (int i = 0; i < obj->Value->Count; ++i)
			{
				ValueProperty^ prop = (ValueProperty^) Value[i];
				if (*(prop->Name) == "Scale3D")
				{
					prop->Value = Scale::FromVector((Vector^)prop->Value);
					break;
				}
			}
			return obj;
		READ_END
	CLS_END

	CLS(Quat)
		PUB_f(A)
		PUB_f(B)
		PUB_f(C)
		PUB_f(D)
		READ
			A = reader->ReadFloat();
			B = reader->ReadFloat();
			C = reader->ReadFloat();
			D = reader->ReadFloat();
		READ_END
	CLS_END

	CLS_(RemovedInstanceArray,PropertyList)
	CLS_END

	CLS_(RemovedInstance,PropertyList)
	CLS_END

	CLS_(InventoryStack,PropertyList)
	CLS_END

	CLS(InventoryItem)
		//TODO: Might also be some PropertyList? Investigate	
		PUB_s(Unknown)
		PUB_s(ItemName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_p(Value)
		READ
			Unknown = reader->ReadString();
			ItemName = reader->ReadString();
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			Value = ValueProperty::Read(reader, this);
		READ_END
		STR_(ItemName)
	CLS_END

	CLS_(PhaseCost, PropertyList)
	CLS_END

	CLS_(ItemAmount, PropertyList)
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

	CLS_(ObjectProperty,ValueProperty)
		PUB_s(LevelName)
		PUB_s(PathName)
		READ
			Read(reader, true);
		READ_END
		Property^ Read(IReader^ reader, bool null_check)
		{
			if (null_check)
				CheckNullByte(reader);
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			return this;
		}
		STR_(PathName)
	CLS_END

	CLS_(ArrayProperty,ValueProperty)
		PUB_s(InnerType)
		//PUB_o(Value)
		READ
			InnerType = reader->ReadString();
			if (InnerType == "StructProperty")
			{
				CheckNullByte(reader);
				int count = reader->ReadInt();
				str^ name = reader->ReadString();
				str^ type = reader->ReadString();
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
				CheckNullByte(reader);
				int count = reader->ReadInt();
				Properties^ objs = gcnew Properties;
				for (int i = 0; i < count; ++i)
				{
					ObjectProperty^ prop = gcnew ObjectProperty(this);
					objs->Add(prop->Read(reader, false));
				}
				Value = objs;
			}
			else if (InnerType == "IntProperty")
			{
				CheckNullByte(reader);
				int count = reader->ReadInt();
				Value = ReadInts(reader, count);
			}
			else if (InnerType == "ByteProperty")
			{
				CheckNullByte(reader);
				int count = reader->ReadInt();
				Value = ReadBytes(reader, count);
			}
			else
				throw gcnew ReadException(reader, String::Format("Unknown inner array type '{0}'", InnerType));
		READ_END
	CLS_END

	CLS_(EnumProperty,ValueProperty)
		PUB_s(EnumName)
		PUB_s(Value)
		READ
			EnumName = reader->ReadString();
			CheckNullByte(reader);
			Value = reader->ReadString();
		READ_END
		STR_(EnumName)
	CLS_END

	CLS_(NameProperty,StrProperty)
	CLS_END

	CLS_(MapProperty,ValueProperty)
		ref class Entry : PropertyList
		{
		public:
			PUB(Parent, MapProperty^)
			PUB_i(Key)
			Entry(MapProperty^ parent, int key) 
				: PropertyList(parent)
				, Key(key)
			{ }
		};
		typedef Dictionary<int, Entry^> Entries;

		PUB_s(MapName)
		PUB_s(ValueType)
		PUB(Value, Entries^)
		READ
			MapName = reader->ReadString();
			ValueType = reader->ReadString();
			/*5*/ CheckNullByte(reader); CheckNullByte(reader); CheckNullByte(reader); CheckNullByte(reader); CheckNullByte(reader);
			int count = reader->ReadInt();
			Value = gcnew Entries;
			for (int i = 0; i < count; ++i)
			{
				int key = reader->ReadInt();
				Entry^ entry = gcnew Entry(this, key);
				Value->Add(key, (Entry^)entry->Read(reader));
			}
		READ_END
	CLS_END

	CLS_(TextProperty,ValueProperty)
		PUB_ab(Unknown)
		PUB_s(Value)
		READ
			CheckNullByte(reader);
			Unknown = ReadBytes(reader, 13);
			Value = reader->ReadString();
		READ_END
	CLS_END

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
			//, Private(nullptr)
		{ }
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB(Children, Properties^)
		PUB_i(Unknown)
		PUB_ab(Missing)
		//PUB_o(Private, ...)

		Property^ Read(IReader^ reader, int length)
		{
			int last_pos = reader->Pos;
			PropertyList::Read(reader);
			//TODO: There is an extra 'int' following, investigate!
			// Not sure if this is valid for all elements which are of type
			// PropertyList. For now,  we will handle it only here
			// Might this be the same "int" discovered with entities below???
			Unknown = reader->ReadInt();
			int bytes_read = reader->Pos - last_pos;
			if (bytes_read < 0)
				throw gcnew ReadException(reader, "Negative offset!");
			if (bytes_read != length)
				Missing = ReadBytes(reader, length - bytes_read);
			return this;
		}
		STR_(PathName)
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
			READ
				LevelName = reader->ReadString();
				PathName = reader->ReadString();
			READ_END
		};

		NamedEntity(Property^ parent, str^ level_name, str^ path_name, Properties^ children)
			: Entity(parent, level_name, path_name, children)
		{ }

		Property^ Read(IReader^ reader, int length)
		{
			int last_pos = reader->Pos;
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			int count = reader->ReadInt();
			Children = gcnew Properties;
			for (int i = 0; i < count; ++i)
			{
				Name^ name = gcnew Name(this);
				Children->Add(name->Read(reader));
			}
			int bytes_read = reader->Pos - last_pos;
			if (bytes_read < 0)
				throw gcnew ReadException(reader, "Negative offset!");
			//if (bytes_read != length)
			//	Missing = ReadBytes(reader, length - bytes_read);
			Entity::Read(reader, length - bytes_read);
			return this;
		}
	};

	CLS(Object)
		//int Type;
		PUB_s(ClassName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_s(OuterPathName)
		PUB(EntityObj,Property^)
		READ
			//Type = 0;
			ClassName = reader->ReadString();
			LevelName = reader->ReadString();
			PathName = reader->ReadString();
			OuterPathName = reader->ReadString();
		READ_END
		Property^ ReadEntity(IReader^ reader)
		{
			int length = reader->ReadInt();
			Entity^ entity = gcnew Entity(this, nullptr, nullptr, nullptr);
			entity->Read(reader, length);
			EntityObj = entity;
			// EXPERIMENTAL
			//if self.Entity.Missing and Config.Get().deep_analysis.enabled:
			//	try:
			//		self.Entity.Private = self.__read_sub()
			//	except:
			//		self.Entity.Private = None
			//		# For now, just raise it to get more info on what went wrong
			//		if AppConfig.DEBUG:
			//			raise
			return this;
		}
		STR_(ClassName)
	CLS_END

	CLS(Actor)
		//int Type;
		PUB_s(ClassName)
		PUB_s(LevelName)
		PUB_s(PathName)
		PUB_i(NeedTransform)
		PUB(Rotation,Quat^)
		PUB(Translate,Vector^)
		PUB(Scale,Scale^)
		PUB_i(WasPlacedInLevel)
		PUB(EntityObj,Property^)
		READ
			//Type = 1;
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
			int length = reader->ReadInt();
			NamedEntity^ entity = gcnew NamedEntity(this, nullptr, nullptr, nullptr);
			entity->Read(reader, length);
			EntityObj = entity;
			// EXPERIMENTAL
			//if self.Entity.Missing and Config.Get().deep_analysis.enabled:
			//	try:
			//		self.Entity.Private = self.__read_sub()
			//	except:
			//		self.Entity.Private = None
			//		# For now, just raise it to get more info on what went wrong
			//		if AppConfig.DEBUG:
			//			raise
			return this;
		}
		STR_(PathName)
	CLS_END

  }
}
