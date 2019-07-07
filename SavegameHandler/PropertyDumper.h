#pragma once

using namespace System;
using namespace System::Text;

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
			else if (IsInstance<Property>(obj))
			{
				//if obj not in self.__reported_properties:
				//	self.__reported_properties.append(prop)
				_AddProperty(safe_cast<Property^>(obj));
			}
			else if (IsInstance<Collections::IDictionary>(obj))
			{
				_AddDict(safe_cast<Collections::IDictionary^>(obj));
			}
			else if (IsInstance<Collections::ICollection>(obj))
			{
				_AddList(safe_cast<Collections::ICollection^>(obj));
			}
			else
			{
				// Final resort: Raw display
				// -> Haven't seen this, but lets keep it until we're sure
				_AddLine("**" + obj->GetType()->Name + ":" + obj->ToString());
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
				return;
			}

			_AddLine(String::Format("/ List with {0:#,#0} elements:", l->Count));
			_Push(1, '|');

			// We can safely ignore checking elements for nullptr here, 
			// loader won't add such
	
			Collections::IEnumerator^ e = l->GetEnumerator();
			e->MoveNext();
			Object^ obj = e->Current;

			e->Reset();

			if (IsInstance<Property>(obj) || IsInstance<Collections::ICollection>(obj))
			{
				while (e->MoveNext())
					_Add(e->Current);
			}
			else
			{
				StringBuilder^ sb = gcnew StringBuilder();
				sb->Append("  " + obj->GetType()->Name + ":[ ");

				if (IsInstance<str>(obj) || IsInstance<String>(obj))
				{
					while (e->MoveNext())
						sb->Append("'" + e->Current->ToString() + "', ");
				}
				else if (IsType<byte>(obj) || IsType<int>(obj) || IsType<long>(obj))
				{
					while (e->MoveNext())
						sb->AppendFormat("{0:#,#0}, ", e->Current);
				}
				else
				{
					// Raw even for floats and doubles to get exact number w/o rounding
					while (e->MoveNext())
						sb->Append(e->Current->ToString() + ", ");
				}

				sb->Append(" ]");
				_AddLine(sb->ToString());
			}

			_Pop(1);
			_AddLine("\\ end of list");
		}

		static void _AddDict(Collections::IDictionary^ d)
		{
			String^ simple = _IsSimple(d);
			if (simple != nullptr)
			{
				_AddLine(simple);
				return;
			}

			_AddLine(String::Format("/ Dict with {0:#,#0} elements:", d->Count));
			_Push(1, '|');

			// We can safely ignore checking elements for nullptr here, 
			// loader won't add such

			Collections::IDictionaryEnumerator^ e = d->GetEnumerator();
			while (e->MoveNext())
			{
				Object^ key = e->Key;
				Object^ val = e->Value;
				String^ s_val = String::Format("Key '{0}'", key);

				if (IsInstance<Property>(val) || IsInstance<Collections::ICollection>(val))
				{
					_AddLine(s_val + ":");
					_Push(1, '\t');
					_Add(val);
					_Pop(1);
				}
				else
				{
					s_val += " = " + val->GetType()->Name + ":";

					if (IsInstance<str>(val) || IsInstance<String>(val))
					{
						s_val += "'" + val->ToString() + "'";
					}
					else if (IsType<byte>(val) || IsType<int>(val) || IsType<long>(val))
					{
						s_val += String::Format("{0:#,#0}", val);
					}
					else
					{
						// Raw even for floats and doubles to get exact number w/o rounding
						s_val += val->ToString();
					}

					_AddLine(s_val);
				}
			}

			_Pop(1);
			_AddLine("\\ end of dict");
		}

		static String^ _IsSimple(Object^ obj)
		{
			// Analyze given object chain, returning either the simplified text
			// version or 'None' if this can't be inlined due to its complexity.

			if (obj == nullptr)
				return "<empty>";

			if (IsInstance</*Value*/Property>(obj))
				return nullptr;

			if (IsInstance<Collections::IDictionary>(obj))
			{
				Collections::IDictionary^ dict = safe_cast<Collections::IDictionary^>(obj);
				if (dict->Count == 0) //not len(obj):
					return "<empty dict>";
				// Could check for one value only, but nahh, not worth the effort 
				// as there are only a few properties with dicts in it.
				return nullptr;
			}

			if (IsInstance<Collections::ICollection>(obj))
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

				StringBuilder^ sb = gcnew StringBuilder();

				if (IsInstance<str>(val) || IsInstance<String>(val))
				{
					sb->Append("[ ");
					e->Reset();
					while (e->MoveNext())
						sb->Append("'" + e->Current->ToString() + "', ");
					sb->Append(" ]");
				}
				else if (IsType<int>(val) || IsType<byte>(val))
				{
					// Extra check for those empty .Unknown[N]
					// (so no check on long here, .Unknown won't be of this type)
					bool isByte = IsType<byte>(val);

					long sum = 0;
					e->Reset();
					while (e->MoveNext())
						sum += isByte ? (byte) (e->Current) : (int) (e->Current);
					if (sum == 0)
						return String::Format("list<{0}>({1:#,#0}):[0,]", val->GetType()->Name, coll->Count);

					sb->EnsureCapacity(coll->Count*(isByte ? 5 : 10));
					sb->Append("[ ");
					e->Reset();
					while (e->MoveNext())
						sb->AppendFormat("{0:#,#0}, ", (isByte ? (byte) (e->Current) : (int)(e->Current)));
					sb->Append(" ]");
				}

				if (sb->Length == 0)
				{
					// Raw even for floats and doubles to get exact number w/o rounding
					// Long will land here too ... but if I remember correctly, there aren't
					// any long-lists at all, so who cares :D
					sb->Append("[ ");
					e->Reset();
					while (e->MoveNext())
						sb->Append(e->Current->ToString() + ", ");
					sb->Append(" ]");
				}

				return val->GetType()->Name + ":" + sb->ToString();
			}

			// None of the above so this is "defined" to be simple :D
			String^ s;
			if (IsInstance<str>(obj) || IsInstance<String>(obj))
			{
				s = "'" + obj + "'";
			}
			else if (IsType<byte>(obj) || IsType<int>(obj) || IsType<long>(obj))
			{
				s = String::Format("{0:#,#0}", obj);
			}
			else
			{
				s = obj->ToString();
			}
			return obj->GetType()->Name + ":" + s;
		}

		static void _AddProperty(/*Value*/Property^ prop)
		{
			_AddLine("-> " + prop->ToString());

			Collections::IDictionaryEnumerator^ e = prop->GetChilds()->GetEnumerator();
			while(e->MoveNext())
			{
				String^ name = (String^) e->Key;
				Object^ val = e->Value;

				if (name == "Missing")
				{
					// This needs special handling, for now, might add a specialized class later
					if (val == nullptr)
					{
						_AddLine("  .Missing = <empty>");
						continue;
					}

					_AddLine("  .Missing");
					_Push(1, '\t');
					String^ dump = Helpers::Hexdump(safe_cast<array<byte>^>(val), 16, true, true, 0);
					array<String^>^ lines = dump->Split('\n');
					Collections::IEnumerator^ e = lines->GetEnumerator();
					while(e->MoveNext())
						_AddLine((String^)e->Current);
					_Pop(1);
					continue;
				}

				String^ simple = _IsSimple(val);
				if (simple != nullptr)
				{
					_AddLine("  ." + name + " = " + simple);
					continue;
				}

				// Inline as much as possible to keep the report short
				if (IsInstance</*Value*/Property>(val) || IsInstance<Collections::ICollection>(val))
				{
					_AddLine("  ." + name + " =");
					_Push(1, '\t');
					_Add(val);
					_Pop(1);
					continue;
				}

				// Hmm, _IsSimple above should have catched this already
				// -> Lets add '??' as marker to see if this arises or not
				String^ s_val = "  ??." + name + " = " + val->GetType()->Name + ":";
				if (IsInstance<str>(val) || IsInstance<String>(val))
				{
					s_val += "'" + val->ToString() + "'";
				}
				else if (IsType<byte>(val) || IsType<int>(val) || IsType<long>(val))
				{
					s_val += String::Format("{0:#,#0}, ", val);
				}
				else
				{
					// Raw even for floats and doubles to get exact number w/o rounding
					s_val += val->ToString();
				}
				_AddLine(s_val);
			}
		}


		static WriteFunc^ _writer;
		static String^ _indent;
		//self.__reported_properties = []

	};

  };

};

