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
  /// <summary>
  ///   Provides a lazily-loaded, read-only mapping of ISO 8583 field numbers to their human-readable descriptions.
  ///   Field definitions are loaded from an <c>iso8583Fields.ini</c> file located in the application base directory.
  ///   Used by the logging handler to include field names in diagnostic output.
  /// </summary>
  public static class Iso8583Fields
  {
    private static readonly Lazy<Dictionary<string, string>> LazyFields = new(LoadFields);

    /// <summary>
    ///   Gets the dictionary mapping ISO 8583 field numbers (as strings) to their descriptions.
    ///   Returns an empty dictionary if the <c>iso8583Fields.ini</c> file is not found.
    /// </summary>
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
