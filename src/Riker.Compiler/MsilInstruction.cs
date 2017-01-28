using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Riker
{
    internal struct MsilInstruction
    {
        private static string mscorlib = typeof(string).Assembly.FullName;

        public OpCode Code { get; set; }
        public int Offset { get; set; }
        public object Operand { get; set; }
        public bool IsMultiByte { get; set; }

        public string ToString(MethodInfo method)
        {
            var dtn = new AssemblyName(method.DeclaringType.Assembly.FullName).Name;
            var result = new StringBuilder();
            var locals = method.GetMethodBody().LocalVariables;

            result.AppendFormat("IL_{0:x4}: 0x{1:x2}{2} {3}{4}", Offset, Code.Value, IsMultiByte ? null : "  ", Code.Name, new string(' ', 12 - Code.Name.Length));

            switch (Code.Value)
            {
                case 0x02:
                {
                    var param = method.GetParameters();
                    result.AppendFormat(" // {0}", method.IsStatic ? param[0].Name : "this");
                    break;
                }
                case 0x03:
                {
                    var param = method.GetParameters();
                    result.AppendFormat(" // {0}", method.IsStatic ? param[1].Name : param[0].Name);
                    break;
                }
                case 0x06:
                case 0x0a:
                {
                    result.AppendFormat(" // {0}", "V_0"/*GetReturnTypeName(dtn, method.GetMethodBody().LocalVariables[0].LocalType)*/);
                    break;
                }
            }

            switch (Code.OperandType)
            {
                case OperandType.InlineBrTarget:
                {
                    var offset = (int)Operand;
                    result.AppendFormat("IL_{0:x8}", offset);
                    break;
                }
                case OperandType.InlineField:
                {
                    var operand = (FieldInfo)Operand;
                    result.AppendFormat(" {0} {1}::{2}", GetReturnTypeName(dtn, operand.FieldType), GetTypeName(dtn, operand.ReflectedType), operand.Name);
                    break;
                }
                //case OperandType.InlineI:
                //{
                //    ReadInt32();
                //    break;
                //}
                //case OperandType.InlineI8:
                //{
                //    ReadInt64();
                //    break;
                //}
                case OperandType.InlineMethod:
                {
                    var methodInfo = Operand as MethodInfo;

                    if (methodInfo != null)
                    {
                        var operand = methodInfo;

                        if (operand.IsStatic == false)
                        {
                            result.Append(" instance");
                        }

                        result.AppendFormat(" {0} {1}::{2}", GetReturnTypeName(dtn, operand.ReturnType), GetTypeName(dtn, operand.ReflectedType), operand.Name);
                        result.Append("(");
                        result.Append(string.Join(", ", operand.GetParameters().Select(x => GetReturnTypeName(dtn, x.ParameterType))));
                        result.Append(")");
                    }

                    var constructorInfo = Operand as ConstructorInfo;

                    if (constructorInfo != null)
                    {
                        var operand = constructorInfo;

                        if (operand.IsStatic == false)
                        {
                            result.Append(" instance");
                        }

                        result.AppendFormat(" void {0}::{1}", GetTypeName(dtn, operand.ReflectedType), operand.Name);
                        result.Append("(");
                        result.Append(string.Join(", ", operand.GetParameters().Select(x => GetReturnTypeName(dtn, x.ParameterType))));
                        result.Append(")");
                    }

                    break;
                }
                case OperandType.InlineNone:
                {
                    break;
                }
                //case OperandType.InlineR:
                //{
                //    ReadSingle32();
                //    break;
                //}
                //case OperandType.InlineSig:
                //{
                //    ReadInt32();
                //    break;
                //}
                //case OperandType.InlineString:
                //{
                //    ReadInt32();
                //    break;
                //}
                //case OperandType.InlineSwitch:
                //{
                //    ReadInt32();
                //    break;
                //}
                //case OperandType.InlineTok:
                //{
                //    ReadInt32();
                //    break;
                //}
                case OperandType.InlineType:
                {
                    var type = (Type)Operand;
                    result.AppendFormat(" {0}", GetReturnTypeName(dtn, type));
                    break;
                }
                //case OperandType.InlineVar:
                //{
                //    ReadUInt16();
                //    break;
                //}
                case OperandType.ShortInlineBrTarget:
                {
                    var offset = (byte)Operand;
                    result.AppendFormat(" IL_{0:x4}", offset);
                    break;
                }
                case OperandType.ShortInlineI:
                {
                    var value = (byte)Operand;
                    result.AppendFormat(" {0} // 0x{0:x2}", value);
                    break;
                }
                case OperandType.ShortInlineR:
                {
                    var value = (int)Operand;
                    result.AppendFormat(" {0} // 0x{0:x4}", value);
                    break;
                }
                case OperandType.ShortInlineVar:
                {
                    var index = (byte)Operand;
                    result.AppendFormat(" V_{0} // {1}", index, GetReturnTypeName(dtn, locals[index].LocalType));
                    break;
                }
                default:
                {
                    throw new NotSupportedException("Unknown operand type.");
                }
            }

            return result.ToString();
        }
        
        private static string GetReturnTypeName(string declaringTypeName, Type type)
        {
            if (type.Assembly.FullName == mscorlib)
            {
                if (type == typeof(void))
                {
                    return "void";
                }

                if (type == typeof(object))
                {
                    return "object";
                }

                if (type == typeof(IntPtr))
                {
                    return "native int";
                }

                if (type == typeof(int))
                {
                    return "int32";
                }

                if (type == typeof(int[]))
                {
                    return "int32[]";
                }
            }

            return "class " + GetTypeName(declaringTypeName, type);
        }

        // Todo: Handle Generics!
        private static string GetTypeName(string declaringTypeName, Type type)
        {
            var name = new AssemblyName(type.Assembly.FullName).Name;

            Func<string, string> cleanType = s =>
            {
                if (s.Contains('<') || s.Contains('>'))
                {
                    return $"'{s}'";
                }

                return s;
            };

            var nestedType = type.IsNested
                ? $".{cleanType(type.DeclaringType.Name)}"
                : null;

            var accessOperator = type.IsNested
                ? "+"
                : ".";

            var fullName = $"{type.Namespace}{nestedType}{accessOperator}{cleanType(type.Name)}";

            return name == declaringTypeName
                ? $"{fullName}"
                : $"[{name}]{fullName}";
        }
    }
}