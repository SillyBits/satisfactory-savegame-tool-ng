#pragma once


namespace Savegame {

	public ref class Savegame
	{
	public:
		String^ Filename;

		int TotalElements;
		Properties::Header^ Header;
		Properties::Properties^ Objects;
		Properties::Properties^ Collected;
		ByteArray^ Missing;

		Savegame(String^ filename) 
			: Filename(filename)
			, TotalElements(0)
			, Header(nullptr)
			, Objects(nullptr)
			, Collected(nullptr)
			, Missing(nullptr)
		{
		}

		
		void Load(ICallback^ callback)
		{
			_Load(callback);//, treeview)
		}

		void Save(ICallback^ callback)
		{
			throw gcnew NotImplementedException("Save functionality not yet available!");
			//_Save(Filename, callback);
		}

		void SaveAs(ICallback^ callback, String^ new_filename)
		{
			throw gcnew NotImplementedException("Save functionality not yet available!");
			//_Save(new_filename, callback)
		}


	protected:
		ICallback^ _callback;

		void _cbStart(IReader^ reader, String^ status, String^ info)
		{
			if (_callback) 
				_callback->Start(reader->Size, status, info);
		}

		void _cbUpdate(IReader^ reader, String^ status, String^ info)
		{
			if (_callback) 
				_callback->Update(reader->Pos, status, info);
		}

		void _cbStop(IReader^ reader, String^ status, String^ info)
		{
			if (_callback) 
				_callback->Stop(status, info);
		}


		void _Load(ICallback^ callback)
		{
			_callback = callback;
			TotalElements = 0;

			FileReader^ reader = gcnew FileReader(Filename, nullptr);
			Log::Info("-> {0:#,#} Bytes", reader->Size);

			//if treeview.IsSideloadingAvail:
			//	treeview.InitSideloading(self)
			_cbStart(reader, "Loading file ...", "");
			//if treeview.IsSideloadingAvail:
			//	treeview.sideload_start(self.Filename)

			try
			{
				Header = (Properties::Header^) (gcnew Properties::Header(nullptr))->Read(reader);
				TotalElements++;
				_cbUpdate(reader, nullptr, "Header");

				// Writing header to log
				//dumper = PropertyDumper.Dumper(
				//	lambda text: Log.Log(text, add_ts=False, add_newline=False))
				//dumper.Dump(self.Header)
				//dumper = None
				Log::Info("-> {0}", Header);

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
			
				//self.Objects = []
				int count = reader->ReadInt();
				Log::Info("-> {0:#,#} game objects", count);
				for (int i=0; i<count; ++i)
				{
					int prev_pos = reader->Pos;
					int type = reader->ReadInt();
					Properties::Property^ new_child;
					if (type == 1)
						new_child = (gcnew Properties::Actor(nullptr))->Read(reader);
					else if (type == 0)
						new_child = (gcnew Properties::Object(nullptr))->Read(reader);
					else
						throw gcnew Exception(
							String::Format("Savegame at pos {0:#,#}: Unhandled type {1}", 
								prev_pos, type));
					TotalElements++;
					Objects->Add(new_child);
					//_total += _count_recurs(new_child.Childs)
					//-> Entity not yet read, so down below instead
					_cbUpdate(reader, nullptr, new_child->ToString());
					//if treeview.IsSideloadingAvail:
					//	treeview.sideload_update(obj)
					//-> Entity not yet read, so down below instead
				}

				int prev_pos = reader->Pos;
				int count_next = reader->ReadInt();
				if (count != count_next)
					throw gcnew Exception(
						String::Format("Savegame at pos {0:#,#}: Counts do not match ({1:#,#} != {2:#,#})", 
							prev_pos, count, count_next));

				for (int i = 0; i < Objects->Count;++i)
				{
					Properties::Property^ prop = Objects[i];
					if (IsInstance<Properties::Actor>(prop))
						safe_cast<Properties::Actor^>(prop)->ReadEntity(reader);
					else if (IsInstance<Properties::Object>(prop))
						safe_cast<Properties::Object^>(prop)->ReadEntity(reader);
					else
						throw gcnew Exception(
							String::Format("Can't handle object {0}", prop));
					//_total += self.__count_recurs(obj.Childs)
					_cbUpdate(reader, nullptr, prop->ToString());//was: obj->ToString()
					//if treeview.IsSideloadingAvail:
					//	treeview.sideload_update(obj)
				}

				//self.Collected = []
				count = reader->ReadInt();
				Log::Info("-> {0:#,#} collected objects", count);
				for (int i = 0; i < count; ++i)
				{
					Properties::Property^ new_child = (gcnew Properties::Collected(nullptr))->Read(reader);
					TotalElements++;
					Collected->Add(new_child);
					//self.__total += self.__count_recurs(new_child.Childs)
					_cbUpdate(reader, nullptr, new_child->ToString());
					//if treeview.IsSideloadingAvail:
					//TODO: treeview.sideload_update(obj)
				}

				int missing = reader->Size - reader->Pos;
				if (missing > 0)
				{
					Missing = Properties::Property::ReadBytes(reader, missing);
					Log::Info("-> Found extra data of size {0:#,#} at end of file", missing);
					TotalElements++;
				}
				_cbUpdate(reader, nullptr, "Done loading");
			}

			//catch(Accessor::ReadException)
			//except Property.Property.PropertyReadException as exc:
			//	print(exc)
			//	if AppConfig.DEBUG: 
			//		raise

			//except Reader.ReaderBase.ReadException as exc:
			//	print(exc)
			//	if AppConfig.DEBUG: 
			//		raise

			//except Exception as exc:  
			//catch
			//{
			//	print("Catched an exception while reading somewhere around pos {:,d}".format(reader.Pos))
			//	print(exc)
			//	if AppConfig.DEBUG: 
			//		raise
			//}

			finally
			{
				//self.__update_totals() -> Now done while loading
				//sleep(0.01)
				_cbStop(reader, nullptr, nullptr);
				reader->Close();
				reader = nullptr;


				//if treeview.IsSideloadingAvail:
				//	treeview.sideload_end()
				//	treeview.UninitSideloading()
			
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

			//...
		}

	};

}
