# Contributing to Iso8583Suite

Thank you for your interest in contributing! This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0, 9.0, or 10.0
- Git

### Setup

```bash
git clone https://github.com/Tochemey/Iso8583Suite.git
cd Iso8583Suite
dotnet restore
dotnet build Iso8583Suite.slnx
```

### Running Tests

```bash
dotnet test Iso8583Suite.slnx
```

### Running Benchmarks

```bash
dotnet run --project Iso8583.Benchmarks -c Release
```

## How to Contribute

### Reporting Bugs

- Search [existing issues](https://github.com/Tochemey/Iso8583Suite/issues) before opening a new one.
- Include .NET version, OS, and a minimal reproduction.

### Suggesting Features

Open an issue describing the use case and proposed behavior.

### Submitting Pull Requests

1. Fork the repository and create a branch from `main`.
2. Make your changes.
3. Add or update tests for any new or changed behavior.
4. Ensure all tests pass: `dotnet test Iso8583Suite.slnx`
5. Ensure the build succeeds on all target frameworks: `dotnet build -c Release`
6. Open a pull request against `main`.

## Code Guidelines

- Target all supported frameworks (`net8.0`, `net9.0`, `net10.0`).
- Follow existing code style and naming conventions in the project.
- Keep public API changes minimal and backward-compatible where possible.
- Avoid adding new external dependencies without discussion in an issue first.
- Performance-sensitive code (codecs, pipeline handlers) should include benchmarks.

## Project Structure

```
Iso8583.Common/        Core library: codecs, pipeline handlers, configuration
Iso8583.Client/        TCP client with reconnection and request correlation
Iso8583.Server/        TCP server with connection management
Iso8583.Tests/         Unit tests (xUnit)
Iso8583.Benchmarks/    Performance benchmarks (BenchmarkDotNet)
SampleServer/          Example authorization server
SampleClient/          Example authorization client
```

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Questions?

Open an issue or start a discussion on the repository.
