using System;
using System.Collections.Generic;
using System.Text;

namespace Pix2d.Common.Extensions
{
    public static class StringExtensions
    {
        public static string FirstCharacterToLower(this string str)
        {
            if (String.IsNullOrEmpty(str) || Char.IsLower(str, 0))
                return str;
            
            return Char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
