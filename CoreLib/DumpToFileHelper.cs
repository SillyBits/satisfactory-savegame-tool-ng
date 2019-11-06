using System;
using System.Collections;
using System.IO;
using System.Text;


namespace CoreLib
{

	public class DumpToFileHelper
	{
		public string Filename { get; private set; }


		protected DumpToFileHelper()
		{ }

		public DumpToFileHelper(string filename)
		{
			Filename = filename;
			_File = new StreamWriter(filename);
			_Indent = new StringBuilder();
		}

		~DumpToFileHelper()
		{
			Close();
		}


		public virtual void Close()
		{
			if (_File != null)
			{
				_File.Dispose();
				_File = null;
			}
		}


		public void Push()
		{
			Push('\t');
		}
		public void Push(char c)
		{
			Push(c, 1);
		}
		public void Push(char c, int count)
		{
			//for (int i = 0; i < count; ++i)
			//	_indent += Char(c);
			_Indent.Append(c, count);
		}

		public void Pop()
		{
			Pop(1);
		}
		public void Pop(int count)
		{
			//_indent = _indent->Substring(0, _indent->Length - count);
			if (count <= _Indent.Length)
				_Indent.Remove(_Indent.Length - count, count);
			else
				_Indent.Clear();
		}


		public void AddLine(string text)
		{
			Add(text, true);
		}
		public virtual void Add(string text, bool newline = false)
		{
			_File.Write(_Indent);
			_File.Write(text);
			if (newline)
				_File.Write("\r\n");
		}


		public void Add(ICollection coll)
		{
			AddLine(coll.GetType().Name);
			AddLine(string.Format("/ Collection with {0:#,#0} elements:", coll.Count));
			Push('|');
			foreach (object obj in coll)
			{
				if (obj is ICollection)
					Add(obj as ICollection);
				else 
					AddLine(obj);
			}
			Pop(1);
			AddLine(@"\_____ end of collection");
		}


		public void AddLine<_Type>(_Type obj)
		{
			Add(obj, true);
		}
		public void Add<_Type>(_Type obj, bool newline = false)
		{
			Add(obj.ToString(), newline);
		}


		private StreamWriter _File;
		protected StringBuilder _Indent;
	
	}

}
