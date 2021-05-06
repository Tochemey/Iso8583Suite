using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Iso8583.Common.Netty
{
    public static class Iso8583Fields
    {
        private static readonly Dictionary<string, string> _fields = new();

        public static Dictionary<string, string> Fields()
        {
            return _fields;
        }


        static Iso8583Fields()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith("iso8583Fields.ini"));
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream ??
                                                throw new InvalidOperationException(
                                                    "unable to load ISO8583 field descriptions"));
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue; // empty line
                if (line.Contains("="))
                    _fields.Add(line.Split('=')[0],
                        string.Join("=",
                            line.Split('=').Skip(1).ToArray()));
            }
        }
    }
}