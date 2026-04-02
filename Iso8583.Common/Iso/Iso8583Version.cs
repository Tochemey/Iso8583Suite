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

namespace Iso8583.Common.Iso
{
  /// <summary>
  ///   The first digit of the MTI indicates the ISO 8583 version in which the message is encoded.
  ///   <see cref="https://en.wikipedia.org/wiki/ISO_8583" />
  /// </summary>
  public enum Iso8583Version
  {
    /// <summary>
    ///   ISO 8583:1987
    /// </summary>
    V1987 = 0x0000,

    /// <summary>
    ///   ISO 8583:1993
    /// </summary>
    V1993 = 0x1000,

    /// <summary>
    ///   ISO 8583:2003
    /// </summary>
    V2003 = 0x2000,

    /// <summary>
    ///   National use
    /// </summary>
    NATIONAL = 0x8000,

    /// <summary>
    ///   Private use
    /// </summary>
    PRIVATE = 0x9000
  }
}