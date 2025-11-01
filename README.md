# SipHash (.NET 8)

Compact, high-performance SipHash-2-4 implementation and a small DI-ready service for .NET 8.

- Language: C# 12
- Target: .NET 8

## Overview

This library exposes:

- `SipHashCore` — a pure static implementation of SipHash-2-4.
- `SipHashService` — a singleton-friendly service that stores the two 64-bit keys in pinned memory and exposes `ComputeHash(ReadOnlySpan<byte>)`.
- `SipHashOptions` — simple options POCO for keys.
- `ServiceCollectionExtensions.AddSipHash` — helper to register the service with the Microsoft DI container.

The implementation is optimized for speed (span-based APIs, `Unsafe.ReadUnaligned`, AggressiveInlining). Keys are stored using `PinnedMemory<ulong>` to avoid GC relocation; review `PinnedMemory` behavior for zeroing/locking guarantees in your security model.

## Installation

Add the project to your solution or reference the assembly/NuGet package that contains the SipHash implementation.

## Quick usage

1. Configure keys in DI (recommended):

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SipHash;

var host = Host.CreateDefaultBuilder()
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<SipHashOptions>(opts =>
        {
            opts.K0 = 0x0123456789abcdefUL;
            opts.K1 = 0xfedcba9876543210UL;
        });

        services.AddSipHash(); // registers SipHashService as singleton
    })
    .Build();

// resolve & use
var svc = host.Services.GetRequiredService<SipHashService>();
byte[] data = System.Text.Encoding.UTF8.GetBytes("hello");
ulong tag = svc.ComputeHash(data);
```

2. Use directly (no DI):

```csharp
ulong k0 = 0x0123456789abcdefUL;
ulong k1 = 0xfedcba9876543210UL;
byte[] data = System.Text.Encoding.UTF8.GetBytes("hello");
ulong tag = SipHashCore.Hash(k0, k1, data);
```

Note: `SipHashCore.Hash` accepts any `ReadOnlySpan<byte>` (byte[] or Memory<byte>).

## API Reference

- SipHashService : IDisposable
  - Constructor: `SipHashService(IOptions<SipHashOptions> options)` — validates presence of K0 and K1 and pins them in memory.
  - `ulong ComputeHash(ReadOnlySpan<byte> data)` — computes SipHash-2-4 tag for the given data. Throws `ObjectDisposedException` if disposed.
  - `void Dispose()` — disposes pinned memory. After disposal, service methods throw.

- SipHashCore (static)
  - `static ulong Hash(ulong k0, ulong k1, ReadOnlySpan<byte> msg)` — pure function implementing SipHash-2-4. Uses little-endian packing.

- SipHashOptions
  - `ulong? K0 { get; set; }`
  - `ulong? K1 { get; set; }`

- ServiceCollectionExtensions
  - `IServiceCollection AddSipHash(this IServiceCollection services)` — registers `SipHashService` as a singleton using configured `SipHashOptions`.

## Thread-safety

`ComputeHash` uses local stack state and reads keys from pinned memory. As implemented, `SipHashService` is safe for concurrent callers provided:

- `PinnedMemory<ulong>.Read(int)` is thread-safe for concurrent reads.
- No mutation of keys occurs after construction.

## Security considerations

- Keys are pinned to avoid relocation, but this does not prevent process memory disclosure (core dumps, swap, memory scanners). Treat keys as sensitive.
- Review `PinnedMemory` behavior for whether it zeros memory on dispose; current constructor uses `zero: false` so initial allocation is not zeroed. If you require explicit zeroing, ensure `PinnedMemory` is configured accordingly or zero keys manually before dispose.
- Register the service as singleton only if the keys' lifetime aligns with application lifetime and DI usage.

## Implementation notes

- The code implements SipHash-2-4 (2 compression rounds, 4 finalization rounds).
- The implementation assumes little-endian byte order when packing/unpacking 64-bit words (typical for x86/x64). If targeting big-endian platforms, validate behavior.
- For maximum performance the implementation uses `Unsafe.ReadUnaligned` and `MemoryMarshal`.

## Examples

```csharp
byte[] data = System.Text.Encoding.UTF8.GetBytes("example");
ulong tag = SipHashCore.Hash(k0, k1, data);
string hex = tag.ToString("x16");
```

## Contributing

Contributions are welcome. Open issues or PRs with focused changes: tests, API improvements, or security-hardening (zeroing keys, secure memory).