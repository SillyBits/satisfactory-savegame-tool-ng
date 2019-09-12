#pragma once


#include "CloudsaveReader.h"
#include "CloudsaveWriter.h"


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
			, _reader(nullptr)
		{
			Reset();
			Properties::Property::DeepAnalysis = false;
		}

		virtual ~Savegame()
		{
			if (_reader)
				_reader->Close();
			_reader = nullptr;

			Reset();
		}


		void EnableDeepAnalysis(bool enable)
		{
			Properties::Property::DeepAnalysis = enable;
		}

		void Reset()
		{
			Modified      = false;
			TotalElements = 0;
			Header        = nullptr;
			Objects       = nullptr;
			Collected     = nullptr;
			Missing       = nullptr;

			if (_reader)
				_reader->Seek(0, IReader::Positioning::Start);
		}

		Properties::Header^ PeekHeader()
		{
			return _PeekHeader();
		}

		void Load(ICallback^ callback)
		{
			_callback = callback;

			if (!Header)
				_PeekHeader();

			if (Header->SaveVersion <= 20)
				_Load(_reader);
			else
				_LoadCloudsave();
		}

		void Save(ICallback^ callback)
		{
			_callback = callback;

			if (Header->SaveVersion <= 20)
				_Save(Filename);
			else
				_SaveCloudsave(Filename);
		}

		void SaveAs(ICallback^ callback, String^ new_filename)
		{
			_callback = callback;

			if (Header->SaveVersion <= 20)
				_Save(new_filename);
			else
				_SaveCloudsave(new_filename);
		}


	protected:
		IReader^   _reader;
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
			try
			{
				if (!_reader)
					_reader = gcnew FileReader(Filename, nullptr);
				else
					_reader->Seek(0, IReader::Positioning::Start);
				Header = (Properties::Header^) (gcnew Properties::Header(nullptr))->Read(_reader);
			}
			catch (Exception^ exc)
			{
				Log::Error(String::Format("Error loading header from '{0}'", Filename), exc);
				_reader->Seek(0, IReader::Positioning::Start);
				Header = nullptr;
			}

			return Header;
		}

		void _LoadCloudsave()
		{
			CloudsaveReader^ cs_reader = gcnew CloudsaveReader(_reader, nullptr);
			_Load(cs_reader);
		}

		void _Load(IReader^ reader)
		{
			TotalElements = 0;

			Log::Info("-> {0:#,#0} Bytes", reader->Size);

			_cbStart(reader->Size, "Loading file ...", "");

			try
			{
				// Writing header to log
				Properties::Dumper::WriteFunc^ writer = gcnew Properties::Dumper::WriteFunc(&LogRedirect);
				Properties::Dumper::Dump(Header, writer);

				Objects = gcnew Properties::Properties;
				Collected = gcnew Properties::Properties;

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
			}
		}

		void _Save(String^ filename)
		{
			FileWriter^ writer = gcnew FileWriter(filename, nullptr);
			Header->Write(writer);
			_Save(writer);
		}

		void _SaveCloudsave(String^ filename)
		{
			FileWriter^ writer = gcnew FileWriter(filename+".test", nullptr);

			_cbStart(TotalElements, "Querying file ...", "");
			int count = 0;

			Header->Write(writer);

			int total_size = 8;//count*2
			for each (Properties::Property^ prop in Objects)
			{
				total_size += prop->GetSize() + 4;//Type
				_cbUpdate(++count, nullptr, prop->ToString());
			}
			total_size += 4;//count
			for each (Properties::Property^ prop in Collected)
			{
				total_size += prop->GetSize();
				_cbUpdate(++count, nullptr, prop->ToString());
			}
			if (Missing)
			{
				total_size += Missing->Length;
				_cbUpdate(++count, nullptr, Missing->ToString());
			}

			CloudsaveWriter^ cs_writer = gcnew CloudsaveWriter(writer, nullptr);
			cs_writer->Write(total_size);
			_Save(cs_writer);
			Log::Debug("... written a total of {0:#,#0} bytes, compressed down to {1:#,#0} bytes)", 
				total_size, writer->Pos);
		}

		void _Save(IWriter^ writer)
		{
			_cbStart(TotalElements, "Saving file ...", "");
			__int64 saved = 1;

			try
			{
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
				Log::Error(String::Format("Error saving '{0}'", writer->Name), exc);
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
