#pragma once

using namespace System;

using namespace CoreLib;


// Allows for creating complete dumps on properties

namespace Savegame
{
  namespace Properties
  {

	static public ref class Dumper
	{
	protected:

	public:

		delegate void WriteFunc(String^ data);

		static void Dump(Property^ prop, WriteFunc^ writer)
		{
			_writer = writer;
			_indent = "";
			//self.__reported_properties = []

			_Add(prop);
		}

	protected:

		// Add given object to output
		// Note that this is called recursively if required!
		static void _Add(Object^ obj)
		{
			String^ simple = _IsSimple(obj);
			if (simple != nullptr)
			{
				_AddLine(simple);
			}
			else
			{
				if (IsInstance</*Value*/Property>(obj))
				{
					//if obj not in self.__reported_properties:
					//	self.__reported_properties.append(prop)
					_AddProperty(safe_cast</*Value*/Property^>(obj));
				}
				else if (IsInstance<Collections::IDictionary>(obj)) //dict
				{
					_AddDict(safe_cast<Collections::IDictionary^>(obj));
				}
				else if (IsInstance<Collections::ICollection>(obj)) //list
				{
					_AddList(safe_cast<Collections::ICollection^>(obj));
				}
				else
				{
					// Final resort: Raw display
					_AddLine("**" + obj->GetType()->Name + ": " + obj->ToString());
				}
			}
		}

		static void _Push(int count, char c)
		{
			//assert len(char) == 1
			for (int i = 0; i < count; ++i)
				_indent += Char(c);
		}

		static void _Pop(int count)
		{
			//assert len(_indent) >= count
			_indent = _indent->Substring(0, _indent->Length - count);
		}

		static void _AddLine(String^ text)
		{
			_AddLine(text, true);
		}

		static void _AddLine(String^ text, bool nl)
		{
			_writer(_indent);
			_writer(text);
			if (nl)
				_writer("\n");
		}


		static void _AddList(Collections::ICollection^ l)
		{
			String^ simple = _IsSimple(l);
			if (simple != nullptr)
			{
				_AddLine(simple);
			}
			else
			{
				_AddLine(String::Format("/ List with {0:#,#0} elements:", l->Count));
				_Push(1, '|');
	
				Collections::IEnumerator^ e = l->GetEnumerator();
				e->MoveNext();
				if (IsInstance<Property>(e->Current)
					|| IsInstance<Collections::ICollection>(e->Current)
					|| IsInstance<Collections::IDictionary>(e->Current))
				{
					e->Reset();
					while (e->MoveNext())
						_Add(e->Current);
				}
				else
				{
					String^ str_vals;
					if (IsInstance<str>(e->Current))
					{
						str_vals = "[ ";
						e->Reset();
						while (e->MoveNext())
							str_vals += "'" + e->Current->ToString() + "', ";
						str_vals += " ]";
					}
					else
					{
						str_vals = "[ ";
						e->Reset();
						while (e->MoveNext())
							str_vals += e->Current->ToString() + ", ";
						str_vals += " ]";
					}
					_AddLine("  " + e->Current->GetType()->Name + ":[ " + str_vals + " ]");
				}

				_Pop(1);
				_AddLine("\\ end of list");
			}
		}

		static void _AddDict(Collections::IDictionary^ d)
		{
			String^ simple = _IsSimple(d);
			if (simple != nullptr)
			{
				_AddLine(simple);
			}
			else
			{
				_AddLine(String::Format("/ Dict with {0:#,#0} elements:", d->Count));
				_Push(1, '|');

				//for key,val in d.items():
				Collections::IDictionaryEnumerator^ e = d->GetEnumerator();
				while (e->MoveNext())
				{
					Object^ key = e->Key;
					Object^ val = e->Value;

					//if val != None and isinstance(val, (Property.Accessor,list,dict)):
					if (IsInstance<Property>(e->Current)
						|| IsInstance<Collections::ICollection>(e->Current)
						|| IsInstance<Collections::IDictionary>(e->Current))
					{
						_AddLine(String::Format("Key '{0}':", key));
						_Push(1, '\t');
						_Add(val);
						_Pop(1);
					}
					else
					{
						String^ s_val;
						// Inline simple values, incl. any None
						if (IsInstance<str>(val))
						{
							s_val = "str:'" + val->ToString() + "'";
						}
						else
						{
							if (val != nullptr)
							{
								s_val = val->GetType()->Name + ":" + val->ToString();
							}
							else
							{
								s_val = "<empty>";
							}
						}
						_AddLine(String::Format("Key {0} = {1}", key, s_val));
					}
				}

				_Pop(1);
				_AddLine("\\ end of dict");
			}
		}

		static String^ _IsSimple(Object^ obj)
		{
			// Analyze given objectchain, returning either the simplified text
			// version or 'None' if this can't be inlined due to its complexity.

			if (obj == nullptr)
				return "<empty>";

			if (IsInstance</*Value*/Property>(obj))
				return nullptr;

			if (IsInstance<Collections::IDictionary>(obj))//dict
			{
				Collections::IDictionary^ dict = safe_cast<Collections::IDictionary^>(obj);
				if (dict->Count == 0) //not len(obj):
					return "<empty dict>";
				// Could check for one value only, but nahh
				return nullptr;
			}

			if (IsInstance<Collections::ICollection>(obj))//list
			{
				Collections::ICollection^ coll = safe_cast<Collections::ICollection^>(obj);
				if (coll->Count == 0)
					return "<empty list>";

				Collections::IEnumerator^ e = coll->GetEnumerator();
				e->MoveNext();
				Object^ val = e->Current;
				Type^ val_t = val->GetType();
				while (e->MoveNext())
				{
					// Mixed types can't be inlined
					if (e->Current->GetType() != val_t)
						return nullptr;
				}

				String^ chk = _IsSimple(val);
				if (chk == nullptr)
				{
					// Has complex types stored in it
					return nullptr;
				}

				String^ t = nullptr;
				String^ vals = nullptr;
				if (IsInstance<str>(val))//str
				{
					//vals = "[ '" + "', '".join(obj) + "' ]"
					vals = "[ ";
					e->Reset();
					while (e->MoveNext())
						vals += "'" + e->Current->ToString() + "', ";
					vals += " ]";
				}
				else if (IsType<int>(val))
				{
					// Extra check for those empty .Unknown[N]
					int sum = 0;
					e->Reset();
					while (e->MoveNext())
						sum += (int) (e->Current);
					if (sum == 0)
					{
						t = String::Format("list({0:#,#0})", coll->Count);
						vals = "[0,]";
					}
					else
					{
						//vals = "[ " + ", ".join([ "{:,d}".format(val) for val in obj ]) + " ]"
						vals = "[ ";
						e->Reset();
						while (e->MoveNext())
							vals += String::Format("{0:#,#d}, ", (int)(e->Current));
						vals += " ]";
					}
				}
				if (t == nullptr)
					t = val->GetType()->Name;
				if (vals == nullptr)
				{
					//vals = "[ " + ", ".join([ str(val) for val in obj ]) + " ]"
					vals = "[ ";
					e->Reset();
					while (e->MoveNext())
						vals += "'" + e->Current->ToString() + "', ";
					vals += " ]";
				}
				return t + ":" + vals;
			}

			// None of the above so this is "defined" to be simple :D
			String^ s;
			if (IsInstance<str>(obj))//str
				s = "'" + obj + "'";
			else if (IsType<int>(obj))//int
				s = String::Format("{0:#,#0}", obj);
			else
				s = obj->ToString();
			return obj->GetType()->Name + ":" + s;
		}

		static void _AddProperty(/*Value*/Property^ prop)
		{
			_AddLine("-> " + prop->ToString());
			//_Push();

			Collections::IDictionaryEnumerator^ e = prop->GetChilds()->GetEnumerator();
			while(e->MoveNext())
			{
				String^ name = (String^) e->Key;
				Object^ val = e->Value;

				if (name == "Missing")
				{
					// This needs special handling, for now, might add a specialized class later
					if (val != nullptr)
					{
						_AddLine("  .Missing");
						_Push(1, '\t');
						String^ dump = Helpers::Hexdump(safe_cast<array<byte>^>(val), 16, true, true, 0);
						array<String^>^ lines = dump->Split('\n');
						Collections::IEnumerator^ e = lines->GetEnumerator();
						while(e->MoveNext())
							_AddLine((String^)e->Current);
						_Pop(1);
					}
					else
					{
						_AddLine("  .Missing = <empty>");
					}
				}
				else
				{
					String^ simple = _IsSimple(val);
					if (simple != nullptr)
					{
						_AddLine("  ." + name + " = " + simple);
					}
					else
					{
						// Inline as much as possible to keep the report short
						if (IsInstance</*Value*/Property>(val)
							|| IsInstance<Collections::ICollection>(val)
							|| IsInstance<Collections::IDictionary>(val))
						{
							_AddLine("  ." + name + " =");
							_Push(1, '\t');
							_Add(val);
							_Pop(1);
						}
						else
						{
							// Inline simple values, incl. any None
							String^ s_val = val->ToString();
							if (IsInstance<str>(val))
							{
								s_val = "str:'" + val + "'";
							}
							else
							{
								if (val != nullptr)
								{
									s_val = val->GetType()->Name + ":" + val->ToString();
								}
								else
								{
									s_val = "<empty>";
								}
							}
							_AddLine("  **." + name + " = " + s_val);
						}
					}
				}
			}

			//_Pop();
		}


		static WriteFunc^ _writer;
		static String^ _indent;
		//self.__reported_properties = []

	};

  };

};

