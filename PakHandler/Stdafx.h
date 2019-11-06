// stdafx.h: Includedatei für Standardsystem-Includedateien
// oder häufig verwendete projektspezifische Includedateien,
// die nur selten geändert werden.

#pragma once

#define _CRT_SECURE_NO_WARNINGS


#include <vcclr.h>

#include <stdio.h>
#include <string.h>
#include <memory.h>


// Uses library: detex
namespace detex {
#include "detex.h"
};


using namespace System;
using namespace System::Collections::Generic;
using namespace System::IO;
using namespace System::Windows::Media;
using namespace System::Windows::Media::Imaging;


using namespace CoreLib;
using namespace PubSub;

using namespace FileHandler;

using namespace Reader;


#ifdef _DEBUG
const bool VERBOSITY = true;
#else
const bool VERBOSITY = false;
#endif


//// Enables experimental code
//#define EXPERIMENTAL


#include "Types.h"

#include "Structures.h"

//#include "PakHandler.h"
