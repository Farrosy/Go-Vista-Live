// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Text.RegularExpressions;

    internal static class NameMangler
    {
        // https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
        //   Identifiers must start with a letter or underscore (_).
        //   Identifiers can contain Unicode letter characters, decimal digit characters, Unicode connecting characters, Unicode combining characters, or Unicode formatting characters.
        // https://ecma-international.org/wp-content/uploads/ECMA-334_7th_edition_december_2023.pdf
        //   §6.4.3 (identifiers)
        // https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-classes-in-regular-expressions?redirectedfrom=MSDN#unicode-category-or-unicode-block-p
        // TL;DR
        //   First character: unicode categories Lu, Ll, Lt, Lm and Lo. Also underscore.
        //   Subsequent characters: unicode categories Lu, Ll, Lt, Lm, Lo, Nl, Mn, Mc, Nd, Pc and Cf.
        private static readonly Regex invalidOnCSharpIdentifierFirstCharacter = new(@"[^\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}_]");
        private static readonly Regex invalidOnCSharpIdentifierSubsequentRun = new(@"[^\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+");
        private static readonly Regex invalidOnSchemaIdentifierFirstCharacter = new("[^a-zA-Z_]");
        private static readonly Regex invalidOnSchemaIdentifierRun = new("[^a-zA-Z0-9_]+");

        public static string MangleSchemaIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var result = invalidOnSchemaIdentifierRun.Replace(s, "_");
            result = result.TrimEnd('_');

            if (result.Length == 0)
            {
                return "_";
            }

            if (invalidOnSchemaIdentifierFirstCharacter.IsMatch(result[..1]))
            {
                result = "_" + result;
            }

            return result;
        }

        public static string MangleCSharpIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var result = invalidOnCSharpIdentifierSubsequentRun.Replace(s, "_");
            result = result.TrimEnd('_');

            if (result.Length == 0)
            {
                return "_";
            }

            if (invalidOnCSharpIdentifierFirstCharacter.IsMatch(result[..1]))
            {
                result = "_" + result;
            }

            return result;
        }


    }
}
