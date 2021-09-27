using System.Text.RegularExpressions;


namespace NpgsqlCrud
{
    ///
    public static class CaseFormatter
    {
        ///
        public static string CamelCaseToLowerCase(string s)
        {
            string[] frags = Regex.Split(s, @"(?<!^)(?=[A-Z])");

            for (int i = 0; i < frags.Length; i++)
            {
                frags[i] = frags[i].ToLower();
            }

            return string.Join("_", frags);
        }


        ///
        public static string LowerCaseToCamelCase(string s)
        {
            string[] frags = s.Split('_');
            for (int i = 0; i < frags.Length; i++)
            {
                string frag = frags[i];

                switch (frag.Length)
                {
                    case 0:
                        return "";

                    case 1:
                        frags[i] = frag.ToUpper();
                        break;

                    default:
                        frags[i] = char.ToUpper(frag[0]) + frag.Substring(1);
                        break;
                }
            }

            return string.Join("", frags);
        }
    }

}
