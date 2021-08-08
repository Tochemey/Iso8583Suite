using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Iso8583.Common
{
  public static class Iso8583Fields
  {
    public static readonly Dictionary<string, string> Fields =
      ReadIniFile(AppDomain.CurrentDomain.BaseDirectory + "iso8583Fields.ini");


    private static Dictionary<string, string> ReadIniFile(string fileName)
    {
      return File.ReadLines(fileName).Select(s => s.Split('=')).Select(s => new
      {
        key = s[0], value = string.Join("=", s.Select((o, n) => new
        {
          n,
          o
        }).Where(o => o.n > 0).Select(o => o.o))
      }).ToDictionary(o => o.key, o => o.value);
    }
  }
}