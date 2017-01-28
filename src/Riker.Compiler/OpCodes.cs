using System;
using System.Reflection.Emit;

namespace Riker
{
    internal static class OpCodes
    {
        private const int Mask = 0xfe00;

        private static readonly OpCode[] _sOpCodes = new OpCode[0x100];
        private static readonly OpCode[] _mOpCodes = new OpCode[0x100];

        static OpCodes()
        {
            RegisterOpCodes();
        }

        private static void RegisterOpCodes()
        {
            var codes = typeof(System.Reflection.Emit.OpCodes).GetFields();

            for (int i = 0; i < codes.Length; i++)
            {
                var code = (OpCode)codes[i].GetValue(null);

                if ((code.Value & Mask) == 0)
                {
                    _sOpCodes[code.Value] = code;
                }
                else
                {
                    _mOpCodes[code.Value & 0xff] = code;
                }
            }
        }

        public static OpCode GetSingleByteOpCode(byte code)
        {
            return _sOpCodes[code];
        }

        public static OpCode GetMultipleByteOpCode(byte code)
        {
            return _mOpCodes[code];
        }
    }
}