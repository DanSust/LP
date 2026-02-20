using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Common
{
    public static class BooleanExtensions
    {
        public static bool ToBool(this int value) => value == 1;
        public static bool ToBool(this int? value) => value == 1;
        public static bool ToBool(this string value) => value == "1";

        // Handles any numeric string
        public static bool ToBoolSafe(this string value)
            => int.TryParse(value, out int intValue) && intValue == 1;
    }
}
