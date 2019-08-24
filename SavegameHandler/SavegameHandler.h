#pragma once


namespace Savegame {

	public ref class Savegame
	{
	public:
		String^ Filename;
		bool Modified;

		int TotalElements;
		Properties::Header^ Header;
		Properties::Properties^ Objects;
		Properties::Properties^ Collected;
		ByteArray^ Missing;

		Savegame(String^ filename) 
			: Filename(filename)
			, Modified(false)
			, TotalElements(0)
			, Header(nullptr)
			, Objects(nullptr)
			, Collected(nullptr)
			, Missing(nullptr)
		{
			Properties::Property::DeepAnalysis = false;
		}


		void EnableDeepAnalysis(bool enable)
		{
			Properties::Property::DeepAnalysis = enable;
		}


		Properties::Header^ PeekHeader()
		{
			return _PeekHeader();
		}

		void Load(ICallback^ callback)
		{
			_Load(callback);
		}

		void Save(ICallback^ callback)
		{
			_Save(Filename, callback);
		}

		void SaveAs(ICallback^ callback, String^ new_filename)
		{
			_Save(new_filename, callback);
		}


	protected:
		ICallback^ _callback;

		void _cbStart(__int64 size, String^ status, String^ info)
		{
			if (_callback) 
				_callback->Start(size, status, info);
		}

		void _cbUpdate(__int64 pos, String^ status, String^ info)
		{
			if (_callback) 
				_callback->Update(pos, status, info);
		}

		void _cbStop(String^ status, String^ info)
		{
			if (_callback) 
				_callback->Stop(status, info);
		}


		static void LogRedirect(String^ s)
		{
			Log::_(s, Logger::Level::Info, false, false);
		}


		Properties::Header^ _PeekHeader()
		{
			Properties::Header^ header = nullptr;
			FileReader^ reader = nullptr;
			try
			{
				reader = gcnew FileReader(Filename, nullptr);
				header = (Properties::Header^) (gcnew Properties::Header(nullptr))->Read(reader);
			}
			catch (Exception^ exc)
			{
				Log::Error(String::Format("Error loading header from '{0}'", Filename), exc);
				header = nullptr;
			}
			finally
			{
				if (reader != nullptr)
					reader->Close();
				reader = nullptr;
			}

			return header;
		}

		void _Load(ICallback^ callback)
		{
			_callback = callback;
			TotalElements = 0;

			FileReader^ reader = gcnew FileReader(Filename, nullptr);
			Log::Info("-> {0:#,#0} Bytes", reader->Size);

			_cbStart(reader->Size, "Loading file ...", "");

			try
			{
				Header = (Properties::Header^) (gcnew Properties::Header(nullptr))->Read(reader);
				TotalElements++;
				_cbUpdate(reader->Pos, nullptr, "Header");

				// Writing header to log
				//Log::Info("-> {0}", Header);
				Properties::Dumper::WriteFunc^ writer = gcnew Properties::Dumper::WriteFunc(&LogRedirect);
				Properties::Dumper::Dump(Header, writer);

				Objects = gcnew Properties::Properties;
				Collected = gcnew Properties::Properties;

				/* /vvvvv DEBUG
				count = reader.readInt()
				prev_pos = reader.Pos
				_type = reader.readInt()
				if _type == 1:
					obj = Property.Actor(self)
				elif _type == 0:
					obj = Property.Object(self)
				else:
					raise AssertionError("Savegame at pos {:,d}: Unhandled type {}"\
										.format(prev_pos, _type))
				new_child = obj.read(reader) 
				self.__total += 1
				self.Objects.append(new_child)
				#self.__total += self.__count_recurs(new_child.Childs)
				#-> Entity not yet read, so down below instead
				self.__cb_read_update(reader, None, str(obj))
				//^^^^^ DEBUG */
			
				int count = reader->ReadInt();
				Log::Info("-> {0:#,#0} game objects", count);
				for (int i=0; i<count; ++i)
				{
					__int64 prev_pos = reader->Pos;
					int type = reader->ReadInt();
					Properties::Property^ new_child;
					if (type == 1)
						new_child = (gcnew Properties::Actor(nullptr))->Read(reader);
					else if (type == 0)
						new_child = (gcnew Properties::Object(nullptr))->Read(reader);
					else
						throw gcnew Exception(
							String::Format("Savegame at pos {0:#,#0}: Unhandled type {1}", 
								prev_pos, type));
					TotalElements++;
					Objects->Add(new_child);
					//_total += _count_recurs(new_child.Childs)
					//-> Entity not yet read, so down below instead
					_cbUpdate(reader->Pos, nullptr, new_child->ToString());
				}

				__int64 prev_pos = reader->Pos;
				int count_next = reader->ReadInt();
				if (count != count_next)
					throw gcnew Exception(
						String::Format("Savegame at pos {0:#,#0}: Counts do not match ({1:#,#0} != {2:#,#0})", 
							prev_pos, count, count_next));

				for (int i = 0; i < Objects->Count;++i)
				{
					Properties::Property^ prop = Objects[i];
					if (IsInstance<Properties::Actor>(prop))
						safe_cast<Properties::Actor^>(prop)->ReadEntity(reader);
					else if (IsInstance<Properties::Object>(prop))
						safe_cast<Properties::Object^>(prop)->ReadEntity(reader);
					//else
					//	throw gcnew Exception(
					//		String::Format("Can't handle object {0}", prop));
					//=> Type checking done in first loop already
					_cbUpdate(reader->Pos, nullptr, prop->ToString());
				}

				count = reader->ReadInt();
				Log::Info("-> {0:#,#0} collected objects", count);
				for (int i = 0; i < count; ++i)
				{
					Properties::Property^ new_child = (gcnew Properties::Collected(nullptr))->Read(reader);
					TotalElements++;
					Collected->Add(new_child);
					_cbUpdate(reader->Pos, nullptr, new_child->ToString());
				}

				__int64 missing = reader->Size - reader->Pos;
				if (missing > 0)
				{
					Missing = reader->ReadBytes((int)missing);
					Log::Info("-> Found extra data of size {0:#,#0} at end of file", missing);
					TotalElements++;
				}

				Modified = false;

				_cbUpdate(reader->Pos, nullptr, "Done loading");
			}
			catch (Properties::UnknownPropertyException^ exc)
			{
				Log::Error(String::Format("Error loading '{0}', unknown property detected", Filename), exc);
				throw;
			}
			catch (Exception^ exc)
			{
				Log::Error(String::Format("Error loading '{0}'", Filename), exc);
				throw;
			}
			finally
			{
				_cbStop(nullptr, nullptr);
				reader->Close();
				reader = nullptr;

				//vvvvv DEBUG
				//fn = path.join(wx.App.Get().Path, "reports", 
				//	"Classes#{}.log".format(self.Header.BuildVersion))
				//with open(fn, "wt") as fd:
				//	for n in Property.class_names:
				//		fd.write(n+"\n")
				//^^^^^ DEBUG
			}
		}

		void _Save(String^ filename, ICallback^ callback)
		{
			_callback = callback;

			FileWriter^ writer = gcnew FileWriter(filename, nullptr);

			_cbStart(TotalElements, "Saving file ...", "");
			__int64 saved = 0;

			try
			{
				Header->Write(writer);
				saved++;
				_cbUpdate(saved, nullptr, "Header");

				writer->Write((int)Objects->Count);
				Log::Info("-> {0:#,#0} game objects", Objects->Count);
				for each (Properties::Property^ prop in Objects)
				{
					__int64 prev_pos = writer->Pos;

					int type = -1;
					if (IsInstance<Properties::Actor>(prop))
						type = 1;
					else if (IsInstance<Properties::Object>(prop))
						type = 0;
					else
						throw gcnew Exception(
							String::Format("Savegame at pos {0:#,#0}: Unhandled type {1}", 
								prev_pos, prop->GetType()->Name));

					writer->Write(type);
					prop->Write(writer);
					//saved++;
					//_cbUpdate(saved, nullptr, prop->ToString());
				}

				writer->Write((int)Objects->Count);
				for each (Properties::Property^ prop in Objects)
				{
					if (IsInstance<Properties::Actor>(prop))
						safe_cast<Properties::Actor^>(prop)->WriteEntity(writer);
					else if (IsInstance<Properties::Object>(prop))
						safe_cast<Properties::Object^>(prop)->WriteEntity(writer);
					else
						throw gcnew Exception(
							String::Format("Can't handle object {0}", prop));

					saved++;
					_cbUpdate(saved, nullptr, prop->ToString());
				}

				writer->Write((int)Collected->Count);
				Log::Info("-> {0:#,#0} collected objects", Collected->Count);
				for each (Properties::Property^ prop in Collected)
				{
					prop->Write(writer);
					saved++;
					_cbUpdate(saved, nullptr, prop->ToString());
				}

				if (Missing != nullptr)
				{
					Log::Info("-> Storing extra data of size {0:#,#0} at end of file", Missing->Length);
					writer->Write(Missing);
					saved++;
				}

				Modified = false;

				_cbUpdate(saved, nullptr, "Done saving");
				Log::Info("-> stored a {0:#,#0} Bytes", writer->Pos);
			}
			catch (Exception^ exc)
			{
				Log::Error(String::Format("Error saving '{0}'", filename), exc);
				throw;
			}
			finally
			{
				_cbStop(nullptr, nullptr);
				writer->Close();
				writer = nullptr;
			}
		}

	};

}
