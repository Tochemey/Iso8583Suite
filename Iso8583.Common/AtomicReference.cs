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

using System.Threading;

namespace Iso8583.Common
{
  public class AtomicReference<T>
    where T : class
  {
    // ReSharper disable once InconsistentNaming
    /// <summary>
    ///   TBD
    /// </summary>
    protected T atomicValue;

    /// <summary>
    ///   Sets the initial value of this <see cref="AtomicReference{T}" /> to <paramref name="originalValue" />.
    /// </summary>
    /// <param name="originalValue">TBD</param>
    public AtomicReference(T originalValue) => atomicValue = originalValue;

    /// <summary>
    ///   Default constructor
    /// </summary>
    public AtomicReference() => atomicValue = default;

    /// <summary>
    ///   The current value of this <see cref="AtomicReference{T}" />
    /// </summary>
    public T Value
    {
      get => Volatile.Read(ref atomicValue);
      set => Volatile.Write(ref atomicValue, value);
    }

    /// <summary>
    ///   If <see cref="Value" /> equals <paramref name="expected" />, then set the Value to
    ///   <paramref name="newValue" />.
    /// </summary>
    /// <param name="expected">TBD</param>
    /// <param name="newValue">TBD</param>
    /// <returns><c>true</c> if <paramref name="newValue" /> was set</returns>
    public bool CompareAndSet(T expected, T newValue)
    {
      var previous = Interlocked.CompareExchange(ref atomicValue, newValue, expected);
      return ReferenceEquals(previous, expected);
    }

    /// <summary>
    ///   Atomically sets the <see cref="Value" /> to <paramref name="newValue" /> and returns the old <see cref="Value" />.
    /// </summary>
    /// <param name="newValue">The new value</param>
    /// <returns>The old value</returns>
    public T GetAndSet(T newValue) => Interlocked.Exchange(ref atomicValue, newValue);

    #region Conversion operators

    /// <summary>
    ///   Performs an implicit conversion from <see cref="AtomicReference{T}" /> to <typeparamref name="T" />.
    /// </summary>
    /// <param name="atomicReference">The reference to convert</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator T(AtomicReference<T> atomicReference) => atomicReference.Value;

    /// <summary>
    ///   Performs an implicit conversion from <typeparamref name="T" /> to <see cref="AtomicReference{T}" />.
    /// </summary>
    /// <param name="value">The reference to convert</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator AtomicReference<T>(T value) => new(value);

    #endregion
  }
}