using System;

// All the enums used within Satisfactory
//

namespace SatisfactorySavegameTool.Supplements
{

	//TODO: Add suitable tiers to those phases
	public enum GamePhases
	{
		EGP_EarlyGame = 0,
		EGP_MidGame,
		EGP_LateGame,
		EGP_EndGame,
		EGP_FoodCourt,//???
	//	EGP_LaunchTowTruck,//???
		EGP_Victory,
		EGP_MAX
	}

	// Output index for splitters
	public enum OutputIndex
	{
		Center = 0,
		Right = 1,
		Left = 2,
	}

	// Splitter rules
	public enum SplitterRule
	{
		None = 0,
		Wildcard,
		AnyUndefined,
		ItemList,
	}

	// Stack sizes
	public enum EStackSize
	{
		SS_ONE     = 1,
		SS_SMALL   = 50,
		SS_MEDIUM  = 100,
		SS_BIG     = 200,
		SS_HUGE    = 500,
	}

	// Equipment slots avail
	public enum EEquipmentSlot
	{
		ES_NONE = 0,
		ES_ARMS = 1,
		ES_BACK = 2,
	}

}
