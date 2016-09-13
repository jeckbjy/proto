using System;
using System.Collections.Generic;
using System.Text;

namespace proto
{
    class Converter
    {
        public static ulong encode_i64(long n)
        {
            return (ulong)((n << 1) ^ (n >> 63));
        }

        public static ulong encode_f64(double n)
        {
            return (ulong)BitConverter.DoubleToInt64Bits(n);
        }

        public static ulong encode_f32(float n)
        {
            return (uint)BitConverter.ToInt32(BitConverter.GetBytes(n), 0);
        }

        public static long decode_i64(ulong n)
        {
            return (long)(n >> 1) ^ (-(long)(n & 1));
        }

        public static double decode_f64(ulong n)
        {
            return BitConverter.Int64BitsToDouble((long)n);
        }

        public static float decode_f32(ulong n)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes((int)(uint)n), 0);
        }

        public static ulong encode(object obj)
        {
            ulong data;
            TypeCode code = Type.GetTypeCode(obj.GetType());
            switch (code)
            {
                case TypeCode.Boolean:
                    data = (ulong)((bool)obj ? 1 : 0);
                    break;
                case TypeCode.Byte:
                    data = (byte)obj;
                    break;
                case TypeCode.UInt16:
                    data = (ushort)obj;
                    break;
                case TypeCode.UInt32:
                    data = (uint)obj;
                    break;
                case TypeCode.UInt64:
                    data = (ulong)obj;
                    break;
                case TypeCode.SByte:
                    data = encode_i64((sbyte)obj);
                    break;
                case TypeCode.Int16:
                    data = encode_i64((short)obj);
                    break;
                case TypeCode.Int32:
                    data = encode_i64((int)obj);
                    break;
                case TypeCode.Int64:
                    data = encode_i64((long)obj);
                    break;
                case TypeCode.Single:
                    data = encode_f32((float)obj);
                    break;
                case TypeCode.Double:
                    data = encode_f64((double)obj);
                    break;
                default:
                    return 0;
            }
            return data;
        }

        public static object decode(Type type, ulong val)
        {
            TypeCode code = Type.GetTypeCode(type);
            switch (code)
            {
                case TypeCode.Boolean:
                    return val == 1;
                case TypeCode.Byte:
                    return (byte)val;
                case TypeCode.UInt16:
                    return (ushort)val;
                case TypeCode.UInt32:
                    return (uint)val;
                case TypeCode.UInt64:
                    return (ulong)val;
                case TypeCode.SByte:
                    return (sbyte)decode_i64(val);
                case TypeCode.Int16:
                    return (short)decode_i64(val);
                case TypeCode.Int32:
                    return (int)decode_i64(val);
                case TypeCode.Int64:
                    return (long)decode_i64(val);
                case TypeCode.Single:
                    return (float)decode_f32(val);
                case TypeCode.Double:
                    return (double)decode_f64(val);
                default:
                    return 0;
            }
        }

        //#region Encode
        //public static ulong encode(bool data)
        //{
        //    return (ulong)(data ? 1 : 0);
        //}

        //public static ulong encode(byte data)
        //{
        //    return (ulong)(data);
        //}

        //public static ulong encode(ushort data)
        //{
        //    return (ulong)(data);
        //}

        //public static ulong encode(uint data)
        //{
        //    return (ulong)(data);
        //}

        //public static ulong encode(ulong data)
        //{
        //    return (ulong)(data);
        //}

        //public static ulong encode(char data)
        //{
        //    return encode_i64((long)data);
        //}

        //public static ulong encode(short data)
        //{
        //    return encode_i64((long)data);
        //}

        //public static ulong encode(int data)
        //{
        //    return encode_i64((long)data);
        //}

        //public static ulong encode(long data)
        //{
        //    return encode_i64(data);
        //}

        //public static ulong encode(float data)
        //{
        //    return encode_f32(data);
        //}

        //public static ulong encode(double data)
        //{
        //    return encode_f64(data);
        //}
        //#endregion

        //#region Decode
        //public static void decode(ulong data, out bool value)
        //{
        //    value = (data != 0);
        //}

        //public static void decode(ulong data, out byte value)
        //{
        //    value = (byte)data;
        //}

        //public static void decode(ulong data, out ushort value)
        //{
        //    value = (ushort)data;
        //}

        //public static void decode(ulong data, out uint value)
        //{
        //    value = (uint)data;
        //}

        //public static void decode(ulong data, out ulong value)
        //{
        //    value = data;
        //}

        //public static void decode(ulong data, out char value)
        //{
        //    value = (char)decode_i64(data);
        //}

        //public static void decode(ulong data, out short value)
        //{
        //    value = (short)decode_i64(data);
        //}

        //public static void decode(ulong data, out int value)
        //{
        //    value = (int)decode_i64(data);
        //}

        //public static void decode(ulong data, out long value)
        //{
        //    value = decode_i64(data);
        //}

        //public static void decode(ulong data, out float value)
        //{
        //    value = decode_f32(data);
        //}

        //public static void decode(ulong data, out double value)
        //{
        //    value = decode_f64(data);
        //}

        //#endregion
    }
}
