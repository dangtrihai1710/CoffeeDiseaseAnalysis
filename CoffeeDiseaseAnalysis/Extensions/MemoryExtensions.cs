// ===========================================
// 2. Extensions/MemoryExtensions.cs - MISSING EXTENSION
// ===========================================
namespace System
{
    public static class MemoryExtensions
    {
        public static bool Contains<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].Equals(value))
                    return true;
            }
            return false;
        }
    }
}