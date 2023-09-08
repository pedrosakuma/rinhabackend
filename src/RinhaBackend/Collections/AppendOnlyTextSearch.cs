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
            items = new T[initialCapacity];
        }

        public void Add(byte[] text, T item)
        {
            int localLength = Interlocked.Increment(ref length);
            items[localLength] = item;
            strings[localLength] = text;
        }

        public int Search(string query, Span<T> destination)
        {
            int currentLength = length;
            int resultCount = 0;
            Span<byte> queryBytes = stackalloc byte[Encoding.ASCII.GetMaxByteCount(query.Length)];
            int written = Encoding.ASCII.GetBytes(query, queryBytes);
            for (int i = 0; i < currentLength; i++)
            {
                if (strings[i] != null 
                    && strings[i].AsSpan().IndexOf(queryBytes.Slice(0, written)) != -1)
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
