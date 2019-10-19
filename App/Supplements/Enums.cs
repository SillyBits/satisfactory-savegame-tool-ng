﻿using System;

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
		Left = 1,
		Right = 2
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

}
