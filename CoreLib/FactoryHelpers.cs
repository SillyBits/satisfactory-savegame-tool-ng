using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib
{
	// Provides low-level factories and alike for enumerating
	// and creating instances at runtime using .Net reflection API
	//


	//
	// Basic factory for creating arbitrary instances of type _InstanceType
	// using the supplied construction parameters
	//
	public abstract class BaseFactory<_InstanceType>
		where _InstanceType : class
	{
		public BaseFactory()
			: this(CoreLib.VERBOSITY)
		{

		}

		public BaseFactory(bool verbose)
		{
			_verbose = verbose;

			_CreateLookup();
		}

		public _InstanceType this[string typename, params object[] parms]
		{
			get { return _CreateInstance(typename, parms); }
		}

		protected virtual _InstanceType _CreateInstance(string typename, object[] parms)
		{
			_InstanceType inst = null;
			try
			{
				ConstructorInfo ci = _lookup[typename];
				if (ci != null)
				{
					object obj_inst = ci.Invoke(parms);
					// Intended to throw if conversion fails
					inst = (_InstanceType) obj_inst;
				}
				else
				{
					Log.Error("Unable to create instance of type [{0}]", typename);
				}
			}
			catch (Exception exc)
			{
				inst = null;
				Log.Error(string.Format("Failed to create instance of type [{0}]", typename), exc);
			}

			return inst;
		}

		// Your implementation of _CreateLookup should do something similar to:
		//
		//		// Discover actual assembly, e.g.
		//		Assembly assembly = Assembly::GetCallingAssembly();
		//		// Setup signature of constructor were looking for, e.g.
		//		Type[] cons = { typeof(Savegame.Properties.Property), typeof(int), typeof(object) };
		//		// Creating actual lookup
		//		_CreateLookup(assembly, cons);
		//
		protected abstract void _CreateLookup();

		protected void _CreateLookup(Assembly assembly, Type[] cons)
		{
			Type basetype = typeof(_InstanceType);

			if (_verbose)
				Log.Debug("Discovering constructors:\n"
					+ "\t-> assembly : {0}\n"
					+ "\t-> base type: {1}, {2}\n"
					+ "\t-> signature: [ {3} ]", 
					assembly.FullName, 
					basetype.Name, basetype.FullName,
					string.Join<Type>(", ", cons)
				);

			_lookup = new Lookup(assembly, cons);

			if (_verbose)
				Log.Debug("... discovered a total of {0} constructors", _lookup.Count);
		}

		protected bool _verbose;
		protected Lookup _lookup;

		protected class Lookup : BaseLookup<ConstructorInfo>
		{
			internal Lookup(Assembly assembly, Type[] cons)
				: this(assembly, cons, CoreLib.VERBOSITY)
			{ }

			internal Lookup(Assembly assembly, Type[] cons, bool verbose)
				: base(verbose)
			{
				SelectorFunc sel;
				if (typeof(_InstanceType).IsInterface)
				{
					// When dealing with interfaces, we've to check the other way around -.-
					sel = (typeinfo) => {
						if (typeof(_InstanceType).IsAssignableFrom(typeinfo))
							return typeinfo.GetConstructor(cons);
						return null;
					};
				}
				else
				{
					sel = (typeinfo) => {
						if (typeinfo.IsSubclassOf(typeof(_InstanceType)))
							return typeinfo.GetConstructor(cons);
						return null;
					};
				}

				_Create(assembly, sel);
			}
		}

	}


	//
	// Basic lookup table
	//
	// Your implementation (advised to subclass) should do something similar to:
	//
	//		// Discover assembly to iterate, e.g.
	//		Assembly assembly = Assembly::GetCallingAssembly();
	//		// Define some predicate which is used to check if type discovered should 
	//		// be included or not. Returning null will skip this, else will be added.
	//		SelectorFunc sel = (type) => return ( some conditional check + conversion to _LookupType );
	//		// Create actual lookup
	//		var lookup = new BaseLookup<_LookupType>();
	//		lookup._Create(assembly, sel);
	//
	public abstract class BaseLookup<_LookupType>
		where _LookupType : class
	{
		public BaseLookup()
			: this(CoreLib.VERBOSITY)
		{ }

		public BaseLookup(bool verbose)
		{
			_verbose = verbose;
		}

		public int Count
		{
			get	{ return _lookup.Count; }
		}

		public _LookupType this[string typename]
		{
			get { return _lookup.ContainsKey(typename) ? _lookup[typename] : null; }
		}

		public delegate _LookupType SelectorFunc(TypeInfo typeinfo);

		public void _Create(Assembly assembly, SelectorFunc selector)
		{
			Type basetype = typeof(_LookupType);

			if (_verbose)
				Log.Debug("Discovering types:\n"
					+ "\t-> assembly : {0}\n"
					+ "\t-> base type: {1}, {2}", 
					assembly.FullName, 
					basetype.Name, basetype.FullName
				);

			_lookup = new Dictionary<string, _LookupType>();

			//TODO: Could use Linq here to reduce set?
			_LookupType result;
			IEnumerable<TypeInfo> types = assembly.DefinedTypes;
			foreach(TypeInfo ti in types)
			{
				//if (ti.IsSubclassOf(basetype))
				{
					result = selector(ti);
					if (result != null)
					{
						_lookup.Add(ti.Name, result);
						if (_verbose)
							Log.Debug("- {0} ({1}) => {2}", ti.Name, ti.FullName, result);
					}
				}
			}

			if (_verbose)
				Log.Debug("... discovered a total of {0} matches", _lookup.Count);
		}

		protected bool _verbose;
		protected Dictionary<string, _LookupType> _lookup;

	}

}
