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

namespace Iso8583.Common
{
  /// <summary>
  ///   SSL/TLS configuration for ISO 8583 connections.
  /// </summary>
  public class SslConfiguration
  {
    /// <summary>
    ///   Whether SSL/TLS is enabled. Default is false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///   Path to the certificate file (PFX/PKCS12 format for server, PEM for client CA).
    /// </summary>
    public string CertificatePath { get; set; }

    /// <summary>
    ///   Password for the certificate file.
    /// </summary>
    public string CertificatePassword { get; set; }

    /// <summary>
    ///   Whether mutual TLS (client certificate authentication) is required.
    /// </summary>
    public bool MutualTls { get; set; }

    /// <summary>
    ///   Path to the CA certificate for verifying the remote peer.
    /// </summary>
    public string CaCertificatePath { get; set; }

    /// <summary>
    ///   The target host name for server certificate validation (client-side).
    ///   If null, uses the connection hostname.
    /// </summary>
    public string TargetHost { get; set; }
  }
}
