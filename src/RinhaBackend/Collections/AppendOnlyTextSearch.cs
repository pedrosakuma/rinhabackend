using System.Text;

namespace RinhaBackend.Collections
{
    public class AppendOnlyTextSearch<T>
    {
        private byte[][] strings;
        private T[] items;
        private int length;

        public AppendOnlyTextSearch(int initialCapacity)
        {
            strings = new byte[initialCapacity][];
            for (int i = 0; i < strings.Length; i++)
                strings[i] = Array.Empty<byte>();
            items = new T[initialCapacity];
        }

        public void Add(byte[] text, T item)
        {
            int index = Interlocked.Increment(ref length) - 1;
            items[index] = item;
            strings[index] = text;
        }

        public int Search(string query, Span<T> destination)
        {
            int currentLength = length;
            int resultCount = 0;
            Span<byte> queryBytes = stackalloc byte[Encoding.ASCII.GetMaxByteCount(query.Length)];
            Ascii.FromUtf16(query, queryBytes, out int written);
            Span<byte> queryBytesSlice = queryBytes.Slice(0, written);
            Ascii.ToLowerInPlace(queryBytesSlice, out _);
            for (int i = 0; i < currentLength; i++)
            {
                if (strings[i].AsSpan().IndexOf(queryBytesSlice) != -1)
                {
                    destination[resultCount++] = items[i];
                    if (resultCount == destination.Length)
                        break;
                }
            }
            return resultCount;
        }
    }
}
