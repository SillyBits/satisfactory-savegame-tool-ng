using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CoreLib;

using Savegame;
using Savegame.Properties;

using SatisfactorySavegameTool.Dialogs;


/*
 * TODO:
 * 
 * - Create base class for actions.
 *   Besides some "Run", will also need stuff like menu item name, translation keys, ...
 *   
 * - Use iterator to find actions avail (even across assemblies) 
 *   and add them to actions menu.
 * 
 */

namespace SatisfactorySavegameTool.Actions
{

	// Stuff used to validate those stored objects
	//

	public class ValidateSavegame
	{
		public static void Run(Savegame.Savegame savegame)
		{
			_savegame = savegame;

			_progress = new ProgressDialog(null, Translate._("Action.Validate.Progress.Title"));
			_callback = _progress.Events;
			_progress.CounterFormat = Translate._("Action.Validate.Progress.CounterFormat");
			_progress.Interval = 100;

			_Run();

			//_progress.Close();
			//_progress = null;
		}


		internal async static void _Run()
		{
			bool outcome = false;

			await Task.Run(() => {
				Log.Info("Starting up validation ...");
				DateTime start_time = DateTime.Now;

				// - First, clean all errors 
				//   (in case validator was run already)
				_ClearAllErrorStates();

				// - Second, validate
				outcome = _ValidateAll();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Validation took {0}", ofs);
			});

			//_progress.Events.Destroy();
			//_progress = null;

			if (outcome)
			{
				MessageBox.Show(Application.Current.MainWindow, 
					Translate._("Action.Validate.NoErrors"), 
					Translate._("Action.Validate.Title"));
			}
			else
			{
				MessageBoxResult res = MessageBox.Show(Application.Current.MainWindow, 
					string.Format(Translate._("Action.Validate.HasErrors"), _errors), 
					Translate._("Action.Validate.Title"), 
					MessageBoxButton.YesNo, MessageBoxImage.Error);
				if (res == MessageBoxResult.Yes)
				{
					//=> TODO
					_report_sb = new StringBuilder();
					_report_depth = 0;

					await Task.Run(() => {
						Log.Info("Creating report ...");
						DateTime start_time = DateTime.Now;

						_CreateReport();

						DateTime end_time = DateTime.Now;
						TimeSpan ofs = end_time - start_time;
						Log.Info("Reporting took {0}", ofs);
					});

					ShowRawTextDialog.Show("", _report_sb.ToString());
					_report_sb = null;
				}
			}

			_progress.Events.Destroy();
			_progress = null;
		}

		internal static void _ClearAllErrorStates()
		{
			int total = 1 + _savegame.TotalElements * 10;
			Log.Info("Cleaning a {0} elements ...", total);
			_cbStart(total, Translate._("Action.Prepare"), " ");

			_CleanErrorsRecurs(_savegame.Header);

			foreach (Property prop in _savegame.Objects)
			{
				_cbUpdate(null, prop.ToString());
				_CleanErrorsRecurs(prop);
			}

			foreach (Property prop in _savegame.Collected)
			{
				_cbUpdate(null, prop.ToString());
				_CleanErrorsRecurs(prop);
			}

			//_CleanErrorsRecurs(_savegame.Missing);

			//_cbStop("Cleaned", "");
			Log.Info("... done cleaning");
		}

		internal static void _CleanErrorsRecurs(Property prop)
		{
			if (prop == null)
				return;

			//_cbUpdate(null, prop.ToString());
			prop.Errors = null;

			Dictionary<string, object> childs = prop.GetChilds();
			foreach (string name in childs.Keys)
			{
				object sub = childs[name];
				if (sub is Property)
					_CleanErrorsRecurs(sub as Property);
				else if (sub is IDictionary)
				{
					IDictionary coll = sub as IDictionary;
					foreach (object key in coll.Keys)
					{
						object obj = coll[key];
						if (obj is Property)
							_CleanErrorsRecurs(obj as Property);
					}
				}
				else if (sub is ICollection)
				{
					ICollection coll = sub as ICollection;
					foreach (object obj in coll)
					{
						if (obj is Property)
							_CleanErrorsRecurs(obj as Property);
					}
				}
			}
		}
		

		internal static bool _ValidateAll()
		{
			int total = 1 + _savegame.TotalElements * 10;
			Log.Info("Validating a {0} elements ...", total);
			_cbStart(total, Translate._("Action.Validate.Progress.Validate"), "");

			bool outcome = _Validate(_savegame.Header);

			foreach (Property prop in _savegame.Objects)
				outcome &= _Validate(prop);

			foreach (Property prop in _savegame.Collected)
				outcome &= _Validate(prop);

			//outcome &= _ValidateObject(_savegame.Missing);

			_cbStop(Translate._("Action.Done"), "");
			Log.Info("... done validation, outcome={0}", outcome);

			return outcome;
		}

		internal static bool _Validate(Property prop)
		{
			if (prop == null)
				return true;

			_cbUpdate(null, prop.ToString());

			bool outcome = _ValidateProperty(prop);
			if (!outcome)
				_errors++;

			Dictionary<string, object> childs = prop.GetChilds();
			foreach (string name in childs.Keys)
			{
				object sub = childs[name];
				if (sub == null)
					continue;

				/*
				if Config.Get().incident_report.enabled \
				  and name == "Missing":
					#if not self.__was_missing_reported_already(sub):
					#	self.__missing_reported.append(sub)
					#	Reporter.Reporter.Add(parent.Root)
					#-> Better gather all unknown nodes, 
					#   even those "Element count=0" ones
					Reporter.Reporter.Add(parent.Root)
				*/

				/*
				if isinstance(prop, (list,dict)):
					self.__total += 1
					for obj in prop:
						outcome &= self.__check(obj)
				#elif isinstance(sub, Property.Accessor):
				else:
					#self.__total += 1
					outcome &= self.__check(prop)
				 */

				if (sub is Property)
					outcome &= _Validate(sub as Property);
				else
				{
					if (!_ValidateObject(sub))
					{
						outcome = false;
						_AddError(prop, string.Format("Invalid child '{0}', value {1}", 
							name, sub));
					}
				}
			}

			if (!outcome)
				prop.AddError("Has one or more errors");

			return outcome;
		}


		internal static void _CreateReport()
		{
			int total = 1 + _savegame.TotalElements * 10;
			//Log.Info("Creating report ...", total);
			_cbStart(total, Translate._("Action.Validate.Report"), "");

			if (_CreateReportRecurs(_savegame.Header))
				_AddToReport("");

			foreach (Property prop in _savegame.Objects)
			{
				if (_CreateReportRecurs(prop))
					_AddToReport("");
			}

			foreach (Property prop in _savegame.Collected)
			{
				if (_CreateReportRecurs(prop))
					_AddToReport("");
			}

			//_CreateReportRecurs(_savegame.Missing);

			_cbStop("Done", "");
			//Log.Info("... done reporting");
		}

		internal static bool _CreateReportRecurs(Property prop)
		{
			if (prop == null)
				return false;

			_cbUpdate(null, prop.ToString());

			if (!prop.HasErrors)
				return false;

			_AddToReport(prop.ToString());
			foreach (string err in prop.Errors)
				_AddToReport(err);

			_report_depth++;

			Dictionary<string, object> childs = prop.GetChilds();
			foreach (string name in childs.Keys)
			{
				object sub = childs[name];

				if (sub is Property)
				{
					_CreateReportRecurs(sub as Property);
				}
				else if (sub is IDictionary)
				{
					IDictionary coll = sub as IDictionary;
					foreach (object key in coll.Keys)
					{
						object obj = coll[key];
						if (obj is Property)
							_CreateReportRecurs(obj as Property);
					}
				}
				else if (sub is ICollection)
				{
					ICollection coll = sub as ICollection;
					foreach (object obj in coll)
					{
						if (obj is Property)
							_CreateReportRecurs(obj as Property);
					}
				}
			}

			_report_depth--;

			return true;
		}


		/*
		def __was_missing_reported_already(self, missing):
			l = len(missing)
			for m in self.__missing_reported:
				if missing == m:
					return True
			#s = sum(missing)
			#for m in self.__missing_reported:
			#	if l == len(m) and s == sum(m):
			#		return True
			return False
		*/


		internal static void _cbStart(int total, string status, string info)
		{
			_count = 0;
			_errors = 0;
			if (_callback != null) 
				_callback.Start(total, status, info);
		}

		internal static void _cbUpdate(string status, string info)
		{
			_count++;
			if (_callback != null) 
				_callback.Update(_count, status, info);
		}

		internal static void _cbStop(string status, string info)
		{
			if (_callback != null) 
				_callback.Stop(status, info);//reader->Pos == reader->Size);
		}

		internal static Savegame.Savegame _savegame;
		internal static ProgressDialog _progress;
		internal static ICallback _callback;
		internal static int _count;
		internal static int _errors;
		internal static StringBuilder _report_sb;
		internal static int _report_depth;


		// Reporting helper
		//

		internal static void _AddToReport(string s)
		{
			_report_sb.Append('\t', _report_depth);
			_report_sb.AppendLine(s);
		}


		// Validation helpers
		//
		// Most validation can be done the abstract way, but a few will need special 
		// handling which is being dealt with by adding pseudo-classes in between which
		// can then be referenced below. For example Actor.Scale, which uses class 
		// 'Scale' instead of 'Vector' and class 'Scale' being a sub-class of 'Vector'.
		//

		internal static bool _ValidateProperty(Property prop)
		{
			if (!_validators.ContainsKey(prop.TypeName))
				return true;
			return _validators[prop.TypeName](prop);
		}

		internal static bool _ValidateObject(object obj)
		{
			//if (!_validators.ContainsKey(prop.TypeName))
			//	return true;

			if (obj is float)
				return _IsValid((float) obj);

			if (obj is IDictionary)
			{
				bool outcome = true;

				IDictionary coll = obj as IDictionary;
				foreach (object key in coll.Keys)
				{
					object sub = coll[key];
					if (sub is Property)
						outcome &= _Validate(sub as Property);
					else
						outcome &= _ValidateObject(sub);
				}

				return outcome;
			}

			if (obj is ICollection)
			{
				bool outcome = true;

				ICollection coll = obj as ICollection;
				foreach (object sub in coll)
				{
					if (sub is Property)
						outcome &= _Validate(sub as Property);
					else
						outcome &= _ValidateObject(sub);
				}

				return outcome;
			}

			// No handler for validation found
			return true;
		}

		internal static float LOWER_SCALE = +1.0e-10f;
		internal static float LOWER_BOUND = -1.0e+10f;
		internal static float UPPER_BOUND = +1.0e+10f;

		internal delegate bool ValidatorFunc(Property prop);
		internal static Dictionary<string, ValidatorFunc> _validators = new Dictionary<string, ValidatorFunc>
		{
		//vvvvv Must be handled different
		//	{ "str",					_v_str },
		//	{ "byte",					_v_byte },
		//	{ "bool",					_v_bool },
		//	{ "float",					_v_float },
		//^^^^^ Must be handled different

		//	{ "Property", 				_v_Property },
			{ "PropertyList", 			_v_PropertyList }, 
			{ "BoolProperty", 			_v_BoolProperty },
		//	{ "ByteProperty", 			_v_ByteProperty },
		//	{ "IntProperty", 			_v_IntProperty },
		//	{ "FloatProperty", 			_v_FloatProperty },
		//	{ "StrProperty", 			_v_StrProperty },
		//	{ "Header", 				_v_Header },
		//	{ "Collected", 				_v_Collected },
		//	{ "StructProperty", 		_v_StructProperty },
			{ "Vector",					_v_Vector },
			{ "Rotator", 				_v_Rotator },
			{ "Scale",					_v_Scale },
			{ "Box", 					_v_Box },
		//	{ "Color", 					_v_Color },
			{ "LinearColor", 			_v_LinearColor },
		//	{ "Transform", 				_v_Transform },
			{ "Quat", 					_v_Quat },
		//	{ "RemovedInstanceArray",	_v_RemovedInstanceArray },
		//	{ "RemovedInstance", 		_v_RemovedInstance },
		//	{ "InventoryStack", 		_v_InventoryStack },
		//	{ "InventoryItem", 			_v_InventoryItem },
		//	{ "PhaseCost", 				_v_PhaseCost },
		//	{ "ItemAmount",				_v_ItemAmount },
		//	{ "ResearchCost", 			_v_ResearchCost },
		//	{ "CompletedResearch", 		_v_CompletedResearch },
		//	{ "ResearchRecipeReward",	_v_ResearchRecipeReward },
		//	{ "ItemFoundData", 			_v_ItemFoundData },
		//	{ "RecipeAmountStruct", 	_v_RecipeAmountStruct },
		//	{ "MessageData", 			_v_MessageData },
		//	{ "SplinePointData", 		_v_SplinePointData },
		//	{ "SpawnData", 				_v_SpawnData },
		//	{ "FeetOffset",				_v_FeetOffset },
		//	{ "SplitterSortRule", 		_v_SplitterSortRule },
		//	{ "ArrayProperty", 			_v_ArrayProperty },
		//	{ "ObjectProperty", 		_v_ObjectProperty },
		//	{ "EnumProperty", 			_v_EnumProperty },
		//	{ "NameProperty", 			_v_NameProperty },
		//	{ "MapProperty", 			_v_MapProperty },
		//	{ "TextProperty", 			_v_TextProperty },
		//	{ "Entity", 				_v_Entity },
		//	{ "NamedEntity", 			_v_NamedEntity },
		//	{ "Object", 				_v_Object },
		//	{ "Actor", 					_v_Actor },
		};


		internal static void _AddError(Property prop, string info = null)
		{
			string s = string.Format("Invalid value for [{0}]", prop.TypeName);//_("Invalid value(s) for [{}]").format(obj.TypeName)
			if (!string.IsNullOrEmpty(info))
				s += ": " + info;
			Log.Info("[V] " + s);
			prop.AddError(s);
		}

		internal static bool _IsValid(float val, float lowerbounds = float.NaN, float upperbounds = float.NaN)
		{
			//global LOWER_BOUND, UPPER_BOUND
			if (float.IsInfinity(val) || float.IsNaN(val))
				return false;
			float limit = !float.IsNaN(lowerbounds) ? lowerbounds : LOWER_BOUND;
			if (val < limit)
				return false;
			limit = !float.IsNaN(upperbounds) ? upperbounds : UPPER_BOUND;
			if (val > limit)
				return false;
			return true;
		}

		internal static bool _v_3(Property prop, float a, float b, float c, 
								  float lowerbounds = float.NaN, float upperbounds = float.NaN)
		{
			if (!( _IsValid(a, lowerbounds, upperbounds)
				&& _IsValid(b, lowerbounds, upperbounds)
				&& _IsValid(c, lowerbounds, upperbounds)))
			{
				_AddError(prop, string.Format("{0} | {1} | {2}", a, b, c));
				return false;
			}
			return true;
		}

		internal static bool _v_4(Property prop, float a, float b, float c, float d,
								  float lowerbounds = float.NaN, float upperbounds = float.NaN)
		{
			if (!( _IsValid(a, lowerbounds, upperbounds)
				&& _IsValid(b, lowerbounds, upperbounds)
				&& _IsValid(c, lowerbounds, upperbounds)
				&& _IsValid(d, lowerbounds, upperbounds)))
			{
				_AddError(prop, string.Format("{0} | {1} | {2} | {3}", a, b, c, d));
				return false;
			}
			float len = (a*a) + (b*b) + (c*c) + (d*d);
			if (!_IsValid(len, 0.9999f, 1.0001f))
			{
				_AddError(prop, string.Format("{0}² + {1}² + {2}² + {3}² = {4} != 1", a, b, c, d, len));
				return false;
			}
			return true;
		}

		//
		// Actual validators
		//

		internal static bool _v_PropertyList(Property obj)
		{
			PropertyList proplist = obj as PropertyList;
			bool outcome = true;
			int index = 0;
			foreach (ValueProperty prop in proplist.Value)
			{
				if (!_ValidateProperty(prop))
				{
					string name = string.Format("Value[{0}]", index);
					if (prop.Name != null)
						name += "='" + prop.Name + "'";
					obj.AddError(string.Format("Invalid value for {0}", name));//_("Invalid value(s) for {}").format(name))
					outcome = false;
				}
				index++;
			}
			return outcome;
		}

		internal static bool _v_BoolProperty(Property obj)
		{
			BoolProperty bool_prop = obj as BoolProperty;
			if (!_IsValid((byte)bool_prop.Value, 0, 1))
			{
				_AddError(obj, string.Format("0 <= {0} <= 1", bool_prop.Value));
				return false;
			}
			return true;
		}

		internal static bool _v_Vector(Property obj)
		{
			return _v_VectorP2(obj, float.NaN);
		}
		internal static bool _v_VectorP2(Property obj, float lowerbounds = float.NaN)
		{
			Savegame.Properties.Vector v = obj as Savegame.Properties.Vector;
			return _v_3(obj, v.X, v.Y, v.Z, lowerbounds);
		}

		internal static bool _v_Rotator(Property obj)
		{
			Rotator rot = obj as Rotator;
			return _v_3(obj, rot.X, rot.Y, rot.Z);
		}

		internal static bool _v_Scale(Property obj)
		{
			return _v_VectorP2(obj, LOWER_SCALE);
		}

		internal static bool _v_Box(Property obj)
		{
			Box b = obj as Box;
			bool outcome = true;
			if (!( _IsValid(b.MinX) 
				&& _IsValid(b.MinY) 
				&& _IsValid(b.MinZ)))
			{
				_AddError(obj, string.Format("Min: {0} | {1} | {2}", b.MinX, b.MinY, b.MinZ));
				outcome = false;
			}
			if (!( _IsValid(b.MaxX) 
				&& _IsValid(b.MaxY) 
				&& _IsValid(b.MaxZ)))
			{
				_AddError(obj, string.Format("Max: {0} | {1} | {2}", b.MaxX, b.MaxY, b.MaxZ));
				outcome = false;
			}
			return outcome;
		}

		internal static bool _v_LinearColor(Property obj)
		{
			LinearColor col = obj as LinearColor;
			if (!( _IsValid(col.R, 0.0f, 1.0f) 
				&& _IsValid(col.G, 0.0f, 1.0f) 
				&& _IsValid(col.B, 0.0f, 1.0f) 
				&& _IsValid(col.A, 0.0f, 1.0f)))
			{
				_AddError(obj, string.Format("{0} | {1} | {2} | {3}", col.R, col.G, col.B, col.A));
				return false;
			}
			return true;
		}

		internal static bool _v_Quat(Property obj)
		{
			Quat quat = obj as Quat;
			if (float.IsInfinity(quat.D) || float.IsNaN(quat.D))
				return _v_3(obj, quat.A, quat.B, quat.C);
			return _v_4(obj, quat.A, quat.B, quat.C, quat.D);
		}

	}

}
