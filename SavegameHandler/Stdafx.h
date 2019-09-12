// stdafx.h: Includedatei für Standardsystem-Includedateien
// oder häufig verwendete projektspezifische Includedateien,
// die nur selten geändert werden.

#pragma once

#define _CRT_SECURE_NO_WARNINGS


#include <vcclr.h>

#include <stdio.h>
#include <string.h>
#include <memory.h>
#include <share.h>


// Include zlib
#include "zlib.h"
#pragma comment(lib, "zlibstat.lib")


using namespace System;
using namespace System::Collections::Generic;
using namespace System::IO;


using namespace CoreLib;
using namespace PubSub;

using namespace FileHandler;


#ifdef _DEBUG
const bool VERBOSITY = true;
#else
const bool VERBOSITY = false;
#endif


// Enables experimental deep analysis code
#define EXPERIMENTAL

#include "Types.h"

#include "Properties.h"
#include "PropertyDumper.h"

//#include "SavegameHandler.h"
