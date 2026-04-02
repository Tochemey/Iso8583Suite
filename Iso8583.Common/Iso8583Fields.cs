// Copyright 2021-2026 Arsene Tochemey Gandote
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Iso8583.Common
{
  public static class Iso8583Fields
  {
    private static readonly Lazy<Dictionary<string, string>> LazyFields = new(LoadFields);

    public static Dictionary<string, string> Fields => LazyFields.Value;

    private static Dictionary<string, string> LoadFields()
    {
      var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iso8583Fields.ini");
      if (!File.Exists(fileName))
        return new Dictionary<string, string>();

      return File.ReadLines(fileName)
        .Where(line => !string.IsNullOrWhiteSpace(line) && line.Contains('='))
        .Select(s => s.Split('=', 2))
        .Where(s => s.Length == 2)
        .ToDictionary(s => s[0].Trim(), s => s[1].Trim());
    }
  }
}
