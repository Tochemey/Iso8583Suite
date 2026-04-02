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
  ///   Position four of the MTI defines the location of the message source within the payment chain.
  ///   <see cref="https://en.wikipedia.org/wiki/ISO_8583#Message_origin" />
  /// </summary>
  public enum MessageOrigin
  {
    /// <summary>
    ///   xxx0	Acquirer
    /// </summary>
    ACQUIRER = 0x0000,

    /// <summary>
    ///   xxx1	Acquirer repeat
    /// </summary>
    ACQUIRER_REPEAT = 0x0001,

    /// <summary>
    ///   xxx2	Issuer
    /// </summary>
    ISSUER = 0x0002,

    /// <summary>
    ///   xxx3	Issuer repeat
    /// </summary>
    ISSUER_REPEAT = 0x0003,

    /// <summary>
    ///   xxx4	Other
    /// </summary>
    OTHER = 0x0004,

    /// <summary>
    ///   xxx5	Other repeat
    /// </summary>
    OTHER_REPEAT = 0x0005
  }
}