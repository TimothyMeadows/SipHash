using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PinnedMemory;

namespace SipHash
{
    public sealed class SipHashService : IDisposable
    {
        private readonly PinnedMemory<ulong> _keys; // [K0, K1]
        private bool _disposed;

        public SipHashService(IOptions<SipHashOptions> options)
        {
            if (options?.Value is null) throw new ArgumentNullException(nameof(options));
            if (!options.Value.K0.HasValue || !options.Value.K1.HasValue)
                throw new ArgumentException("SipHashOptions requires K0 and K1 to be set.");
            _keys = new PinnedMemory<ulong>(new[] { options.Value.K0.Value, options.Value.K1.Value }, zero: false, locked: true);
        }

        public ulong ComputeHash(ReadOnlySpan<byte> data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SipHashService));
            ulong k0 = _keys.Read(0);
            ulong k1 = _keys.Read(1);
            return SipHashCore.Hash(k0, k1, data);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _keys.Dispose();
            _disposed = true;
        }
    }

    public static class SipHashCore
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Rotl(ulong x, int b) => (x << b) | (x >> (64 - b));

        public static ulong Hash(ulong k0, ulong k1, ReadOnlySpan<byte> msg)
        {
            ulong v0 = 0x736f6d6570736575UL ^ k0;
            ulong v1 = 0x646f72616e646f6dUL ^ k1;
            ulong v2 = 0x6c7967656e657261UL ^ k0;
            ulong v3 = 0x7465646279746573UL ^ k1;

            int idx = 0, len = msg.Length;

            while (idx + 8 <= len)
            {
                ulong m = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(msg.Slice(idx)));
                idx += 8;

                v3 ^= m;
                for (int r = 0; r < 2; r++)
                {
                    v0 += v1; v2 += v3; v1 = Rotl(v1, 13); v3 = Rotl(v3, 16);
                    v1 ^= v0; v3 ^= v2; v0 = Rotl(v0, 32);
                    v2 += v1; v0 += v3; v1 = Rotl(v1, 17); v3 = Rotl(v3, 21);
                    v1 ^= v2; v3 ^= v0; v2 = Rotl(v2, 32);
                }
                v0 ^= m;
            }

            ulong b = ((ulong)len) << 56;
            int rem = len - idx;
            switch (rem)
            {
                case 7: b |= (ulong)msg[idx + 6] << 48; goto case 6;
                case 6: b |= (ulong)msg[idx + 5] << 40; goto case 5;
                case 5: b |= (ulong)msg[idx + 4] << 32; goto case 4;
                case 4: b |= (ulong)msg[idx + 3] << 24; goto case 3;
                case 3: b |= (ulong)msg[idx + 2] << 16; goto case 2;
                case 2: b |= (ulong)msg[idx + 1] << 8;  goto case 1;
                case 1: b |= msg[idx]; break;
            }

            v3 ^= b;
            for (int r = 0; r < 2; r++)
            {
                v0 += v1; v2 += v3; v1 = Rotl(v1, 13); v3 = Rotl(v3, 16);
                v1 ^= v0; v3 ^= v2; v0 = Rotl(v0, 32);
                v2 += v1; v0 += v3; v1 = Rotl(v1, 17); v3 = Rotl(v3, 21);
                v1 ^= v2; v3 ^= v0; v2 = Rotl(v2, 32);
            }
            v0 ^= b;

            v2 ^= 0xff;
            for (int r = 0; r < 4; r++)
            {
                v0 += v1; v2 += v3; v1 = Rotl(v1, 13); v3 = Rotl(v3, 16);
                v1 ^= v0; v3 ^= v2; v0 = Rotl(v0, 32);
                v2 += v1; v0 += v3; v1 = Rotl(v1, 17); v3 = Rotl(v3, 21);
                v1 ^= v2; v3 ^= v0; v2 = Rotl(v2, 32);
            }

            return v0 ^ v1 ^ v2 ^ v3;
        }
    }

    public class SipHashOptions
    {
        public ulong? K0 { get; set; }
        public ulong? K1 { get; set; }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSipHash(this IServiceCollection services)
        {
            services.AddSingleton<SipHashService>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<SipHashOptions>>().Value;
                return new SipHashService(Options.Create(opts));
            });
            return services;
        }
    }
}
