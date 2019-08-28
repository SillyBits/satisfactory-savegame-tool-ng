using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;


/*
 * TODO:
 * 
 * - Add more complex hierarchical filters
 * 
 */

namespace CoreLib
{

	public static class Filters
	{

		public interface IFilter
		{
			bool Test(object value);
		}


		/// <summary>
		/// Actual filter definition, you can subclass from this in case you need additional parameters.
		/// Per definition, a (None,None,null) filter represents a "match all".
		/// </summary>
		public class Definition
		{
			public Operations Operation { get; set; }
			public Conditions Condition { get; set; }
			public string     Value     { get; set; }

			public Definition()
			{
				Operation = Operations.None;
				Condition = Conditions.None;
				Value     = null;
			}

			public Definition(Operations operation, Conditions condition, string value)
			{
				Operation = operation;
				Condition = condition;
				Value     = value;
			}
		}


		public class Definitions : List<Definition>
		{
			public Definitions(IEnumerable<Definition> defs)
				: base(defs)
			{ }
		}


		#region Filter chain
		public static FilterChain CreateChain(Definitions defs)
		{
			FilterChain chain = new AndChain();

			if (defs != null && defs.Count > 0)
			{
				if (!defs.Any(f => f.Operation == Operations.Or))
				{
					// Simple AND chain
					defs.ForEach(def => chain.Add(CreateFilterOp(def.Condition, def.Value)));
				}
				else
				{
					//TODO:
					Definition prev = defs[0];
					Definition curr;
					FilterChain or = new OrChain();
					for (int index = 1; index < defs.Count; ++index)
					{
						curr = defs[index];
						if (curr.Operation == Operations.Or)
						{
							// Add another "or"
							or.Add(prev);
						}
						else
						{
							if (or.Count != 0)
							{
								// End current "or" and create new chain
								or.Add(prev);
								chain.Add(or);
								or = new OrChain();
							}
							else
							{
								// Add single filter
								chain.Add(prev);
							}
						}

						prev = curr;
					}

					// Add last pending
					if (prev.Operation == Operations.Or)
						or.Add(prev);
					if (or.Count > 0)
						chain.Add(or);
					if (prev.Operation == Operations.And)
						chain.Add(prev);
				}
			}

			return chain;
		}

		public abstract class FilterChain : List<IFilter>, IFilter
		{
			public void Add(Definition def)
			{
				Add(CreateFilterOp(def.Condition, def.Value));
			}

			public abstract bool Test(object value);
		}

		internal class AndChain : FilterChain
		{
			internal AndChain() { }
			internal AndChain(IFilter filter) { Add(filter); }
			internal AndChain(List<IFilter> filters) { AddRange(filters); }
			internal AndChain(IEnumerable<IFilter> filters) { AddRange(filters); }

			public override bool Test(object value)
			{
				return this.All(f => f.Test(value));
			}
		}

		internal class OrChain : FilterChain
		{
			internal OrChain() { }
			internal OrChain(IFilter filter) { Add(filter); }
			internal OrChain(List<IFilter> filters) { AddRange(filters); }
			internal OrChain(IEnumerable<IFilter> filters) { AddRange(filters); }

			public override bool Test(object value)
			{
				return this.Any(f => f.Test(value));
			}
		}
		#endregion


		#region Basic filter ops
		internal static IFilter CreateFilterOp(Conditions cond, object filterval)
		{
			switch (cond)
			{
				// The empty "match all" doesn't require a type
				case Conditions.None:
					return new None();

				// Those must use reflection to call generic version
				case Conditions.Equal:
				case Conditions.NotEqual:
				case Conditions.Less:
				case Conditions.LessEqual:
				case Conditions.Greater:
				case Conditions.GreaterEqual:
					Type type = filterval.GetType();
					MethodInfo method;
					if (!_creators.ContainsKey(type))
					{
						method = _creator.MakeGenericMethod(type);
						_creators.Add(type, method);
					}
					else
						method = _creators[type];
					return (IFilter) method.Invoke(null, new object[] { cond, filterval });

				// Those will require a string value
				case Conditions.Contains:
				case Conditions.StartsWith:
				case Conditions.EndsWith:
				case Conditions.Regex:
					if (filterval is string)
						return CreateFilterOp<string>(cond, filterval);
					return CreateFilterOp<string>(cond, filterval.ToString());
			}
			return null;
		}

		private static MethodInfo _creator = typeof(Filters)
			.GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
			.Where(info => info.Name == "CreateFilterOp" && info.IsGenericMethod)
			.First()
			;

		private static Dictionary<Type, MethodInfo> _creators = new Dictionary<Type, MethodInfo>();

		private static IFilter CreateFilterOp<_Type>(Conditions cond, object filterval)
			where _Type : IComparable
		{
			switch (cond)
			{
				case Conditions.Equal       : return new Equal<_Type>((_Type)filterval);
				case Conditions.NotEqual    : return new NotEqual<_Type>((_Type)filterval);
				case Conditions.Less        : return new Less<_Type>((_Type)filterval);
				case Conditions.LessEqual   : return new LessEqual<_Type>((_Type)filterval);
				case Conditions.Greater     : return new Greater<_Type>((_Type)filterval);
				case Conditions.GreaterEqual: return new GreaterEqual<_Type>((_Type)filterval);
				case Conditions.Contains    : return new Contains((string)filterval);
				case Conditions.StartsWith  : return new StartsWith((string)filterval);
				case Conditions.EndsWith    : return new EndsWith((string)filterval);
				case Conditions.Regex       : return new RegularEx((string)filterval);
			}
			return null;
		}


		private class None : IFilter
		{
			public bool Test(object value) { return true; }
		}

		private class Equal<_Type> : IFilter 
			where _Type : IComparable
		{
			internal Equal(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) == 0; }
			private _Type _filterval;
		}

		private class NotEqual<_Type> : IFilter
			where _Type : IComparable
		{
			internal NotEqual(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) != 0; }
			private _Type _filterval;
		}

		private class Less<_Type> : IFilter
			where _Type : IComparable
		{
			internal Less(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) < 0; }
			private _Type _filterval;
		}

		private class LessEqual<_Type> : IFilter
			where _Type : IComparable
		{
			internal LessEqual(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) <= 0; }
			private _Type _filterval;
		}

		private class Greater<_Type> : IFilter
			where _Type : IComparable
		{
			internal Greater(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) > 0; }
			private _Type _filterval;
		}

		private class GreaterEqual<_Type> : IFilter
			where _Type : IComparable
		{
			internal GreaterEqual(_Type filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((_Type)value).CompareTo(_filterval) >= 0; }
			private _Type _filterval;
		}

		// Those are allowed on strings only!
		private class Contains : IFilter
		{
			internal Contains(string filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((string)value).Contains(_filterval); }
			private string _filterval;
		}

		private class StartsWith : IFilter
		{
			internal StartsWith(string filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((string)value).StartsWith(_filterval); }
			private string _filterval;
		}

		private class EndsWith : IFilter
		{
			internal EndsWith(string filterval) { _filterval = filterval; }
			public bool Test(object value) { return ((string)value).EndsWith(_filterval); }
			private string _filterval;
		}

		private class RegularEx : IFilter
		{
			internal RegularEx(string regex) : this(new Regex(regex, RegexOptions.CultureInvariant|RegexOptions.IgnoreCase)) { }
			internal RegularEx(Regex regex) { _regex = regex; }
			public bool Test(object value) { return _regex.IsMatch((string)value); }
			private Regex _regex;
		}
		#endregion


		public enum Operations
		{
			None = 0,
			And,
			Or,
		}


		public enum Conditions
		{
			None = 0,
			Equal,
			NotEqual,
			Less,
			LessEqual,
			Greater,
			GreaterEqual,
			Contains,
			StartsWith,
			EndsWith,
			Regex,
		}

	}

}

