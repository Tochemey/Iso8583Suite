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
  ///   Message type indicator
  /// </summary>
  public class MTI
  {
    private readonly Iso8583Version _isoVersion;
    private readonly MessageClass _messageClass;
    private readonly MessageFunction _messageFunction;
    private readonly MessageOrigin _messageOrigin;

    /// <summary>
    ///   creates a new instance of MTI
    /// </summary>
    /// <param name="isoVersion"></param>
    /// <param name="messageClass"></param>
    /// <param name="messageFunction"></param>
    /// <param name="messageOrigin"></param>
    public MTI(Iso8583Version isoVersion, MessageClass messageClass, MessageFunction messageFunction,
      MessageOrigin messageOrigin)
    {
      _isoVersion = isoVersion;
      _messageClass = messageClass;
      _messageFunction = messageFunction;
      _messageOrigin = messageOrigin;
    }

    /// <summary>
    ///   returns the MTI value
    /// </summary>
    /// <returns></returns>
    public int Value() => (int)_isoVersion + (int)_messageClass + (int)_messageFunction + (int)_messageOrigin;
  }
}