using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Riker
{
    internal sealed class MsilInstructionReader
    {
        private readonly byte[] _il;
        private int _index;

        internal MsilInstructionReader(byte[] il)
        {
            _il = il;
        }

        internal IList<MsilInstruction> ReadAll(Module module)
        {
            var result = new List<MsilInstruction>();

            while (_index < _il.Length)
            {
                var il = _il[_index++];

                OpCode code;
                bool isMultiByte;
                object operand = null;

                if (il == 0xfe)
                {
                    il = _il[_index++];
                    isMultiByte = true;
                    code = OpCodes.GetMultipleByteOpCode(il);
                }
                else
                {
                    isMultiByte = false;
                    code = OpCodes.GetSingleByteOpCode(il);
                }

                var offset = _index - (isMultiByte ? 2 : 1);

                switch (code.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    {
                        operand = ReadInt32() + _index;
                        break;
                    }
                    case OperandType.InlineField:
                    {
                        operand = module.ResolveField(ReadInt32());
                        break;
                    }
                    case OperandType.InlineI:
                    {
                        operand = ReadInt32();
                        break;
                    }
                    case OperandType.InlineI8:
                    {
                        operand = ReadInt64();
                        break;
                    }
                    case OperandType.InlineMethod:
                    {
                        operand = module.ResolveMember(ReadInt32());
                        break;
                    }
                    case OperandType.InlineNone:
                    {
                        break;
                    }
                    case OperandType.InlineR:
                    {
                        operand = ReadSingle32();
                        break;
                    }
                    case OperandType.InlineSig:
                    {
                        operand = ReadInt32();
                        break;
                    }
                    case OperandType.InlineString:
                    {
                        operand = ReadInt32();
                        break;
                    }
                    case OperandType.InlineSwitch:
                    {
                        operand = ReadInt32();
                        break;
                    }
                    case OperandType.InlineTok:
                    {
                        operand = ReadInt32();
                        break;
                    }
                    case OperandType.InlineType:
                    {
                        operand = module.ResolveType(ReadInt32());
                        break;
                    }
                    case OperandType.InlineVar:
                    {
                        operand = ReadUInt16();
                        break;
                    }
                    case OperandType.ShortInlineBrTarget:
                    {
                        operand = (byte)(ReadUInt8() + _index);
                        break;
                    }
                    case OperandType.ShortInlineI:
                    {
                        operand = ReadUInt8();
                        break;
                    }
                    case OperandType.ShortInlineR:
                    {
                        operand = ReadSingle32();
                        break;
                    }
                    case OperandType.ShortInlineVar:
                    {
                        operand = ReadUInt8();
                        break;
                    }
                    default:
                    {
                        throw new NotSupportedException("Unknown operand type.");
                    }
                }

                var instruction = new MsilInstruction
                {
                    Code        = code,
                    Offset      = offset,
                    Operand     = operand,
                    IsMultiByte = isMultiByte
                };

                result.Add(instruction);
            }

            return result;
        }

        private byte ReadUInt8()
        {
            return (byte)(_il[_index++] << 0x00);
        }

        private ushort ReadUInt16()
        {
            var value = _il[_index++] << 0x00 |
                        _il[_index++] << 0x08;

            return (ushort)value;
        }

        private int ReadInt32()
        {
            return _il[_index++] << 0x00 |
                   _il[_index++] << 0x08 |
                   _il[_index++] << 0x10 |
                   _il[_index++] << 0x18;
        }

        private long ReadInt64()
        {
            return _il[_index++] << 0x00 |
                   _il[_index++] << 0x08 |
                   _il[_index++] << 0x10 |
                   _il[_index++] << 0x18 |
                   _il[_index++] << 0x20 |
                   _il[_index++] << 0x28 |
                   _il[_index++] << 0x30 |
                   _il[_index++] << 0x38;
        }

        private float ReadSingle32()
        {
            return _il[_index++] << 0x00 |
                   _il[_index++] << 0x08 |
                   _il[_index++] << 0x10 |
                   _il[_index++] << 0x18;
        }
    }
}