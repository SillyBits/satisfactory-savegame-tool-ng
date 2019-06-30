using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib
{
    public class CoreLib
    {

#if DEBUG
		public const bool VERBOSITY = true;
#else
		public const bool VERBOSITY = false;
#endif

    }
}
