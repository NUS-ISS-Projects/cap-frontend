using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DISTestKit.Helper
{
    public static class BinaryPduHelper
    {
        public static void WriteInt16(List<byte> buffer, short value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        public static void WriteByte(List<byte> buffer, byte value)
        {
            buffer.Add(value);
        }
    }

}
