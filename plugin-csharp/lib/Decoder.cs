using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace proto
{
    public class Decoder
    {
        private List<uint> m_indexs = new List<uint>();
        private Stream m_stream;
        // flag中读取到的当前数据
        private ulong m_val;
        private uint m_tag;
        private bool m_ext;
        public Decoder(Stream stream)
        {
            m_stream = stream;
        }

        public T Decode<T>() where T:class
        {
            T msg = (T)Decode();
            return msg;
        }

        public object Decode()
        {
            long pos = m_stream.Position;
            uint flag = (uint)m_stream.ReadByte();
            uint len1, len2, len3, len;
            len1 = (flag >> 5);
            len2 = (flag >> 2) & 0x07;
            len3 = flag & 0x03;
            len = len1 + len2 + len3;
            byte[] buff = new byte[len];
            if (m_stream.Read(buff, 0, (int)len) < len)
                return null;
            uint msg_len, idx_len, msg_id;
            msg_len = decode_group_var(buff, 0, len1);
            idx_len = decode_group_var(buff, len1, len2);
            msg_id  = decode_group_var(buff, len1 + len2, len3);
            if (m_stream.Length - m_stream.Position < msg_len)
            {
                m_stream.Seek(pos, SeekOrigin.Begin);
                return null;
            }
            // 校验消息
            MetaType meta = ModelManager.GetMeta(msg_id);
            if (meta == null)
                return null;
            // 开始解析消息
            pos = m_stream.Position;
            long epos = pos + msg_len;
            long offset = msg_len - idx_len;
            if(idx_len > 0)
            {// 先读取index
                uint val;
                m_stream.Seek(offset, SeekOrigin.Current);
                while(m_stream.Position < epos)
                {
                    val = (uint)ReadVariant();
                    m_indexs.Add(val);
                }
                m_stream.Seek(pos, SeekOrigin.Begin);
            }
            if(msg_len > 0)
            {// 解析消息
                ReadPacket(meta, (uint)offset);
            }
            // 移动到合适位置,防止有错误消息解析
            m_stream.Seek(pos + msg_len, SeekOrigin.Begin);

            return null;
        }

        private object ReadPacket(MetaType meta, uint lens)
        {
            if (!meta.Serializable)
                return null;
            uint field_tag = 0;
            object msg = meta.Create();
            long epos = m_stream.Position + lens;
            while(m_stream.Position < epos)
            {
                ReadTag();
                field_tag += m_tag;
                MetaField field_meta = meta.GetField(field_tag);
                if (field_meta == null)
                    continue;
                // 读取field
                object field = ReadField(field_meta.Type);
                field_meta.SetValue(msg, field);
            }
            return null;
        }

        private object ReadField(Type type)
        {
            if(type.IsValueType)
            {
                return ReadBasic(type);
            }
            else if(type == typeof(string))
            {
                byte[] buff = new byte[m_val];
                if (m_stream.Read(buff, 0, (int)m_val) < (int)m_val)
                    return null;
                string str = Encoding.UTF8.GetString(buff);
                return str;
            }
            else if(type == typeof(MemoryStream))
            {
                MemoryStream stream = new MemoryStream();
                stream.SetLength((long)m_val);
                if (m_stream.Read(stream.GetBuffer(), 0, (int)m_val) < (int)m_val)
                    return null;
                return stream;
            }
            else if(type.IsGenericType || type.IsArray)
            {
                return ReadGeneric(type);
            }
            else if(type.IsClass)
            {// 复杂类型
                MetaType meta = ModelManager.GetMeta(type);
                if (meta == null)
                    return null;
                ReadPacket(meta, (uint)m_val);
            }
            return null;
        }

        private object ReadBasic(Type type)
        {
            TypeCode code = Type.GetTypeCode(type);
            switch(code)
            {
                case TypeCode.Boolean:
                    return m_val == 1;
                case TypeCode.Char:
                    return (char)(byte)m_val;
                case TypeCode.SByte:
                    return (sbyte)(byte)m_val;
                case TypeCode.Byte:
                    return (byte)m_val;
                case TypeCode.UInt16:
                    return (ushort)m_val;
                case TypeCode.UInt32:
                    return (uint)m_val;
                case TypeCode.UInt64:
                    return m_val;
                case TypeCode.Int16:
                    return (short)decodei64(m_val);
                case TypeCode.Int32:
                    return (int)decodei64(m_val);
                case TypeCode.Int64:
                    return (long)decodei64(m_val);
                case TypeCode.Single:
                    return (float)decodef32(m_val);
                case TypeCode.Double:
                    return (double)decodef64(m_val);
                default:
                    return 0;
            }
        }

        private object ReadGeneric(Type type)
        {
            // LinkedList:暂时无法支持
            Type[] gen_types = type.GetGenericArguments();
            object container = ModelManager.Create(type);
            long epos = (long)m_val;
            if(type == typeof(List<>))
            {
                IList list = (IList)container; 
                while(m_stream.Position < epos)
                {
                    ReadTag();
                    object val = ReadField(gen_types[0]);
                    list.Add(val);
                }
            }
            else if(type == typeof(Dictionary<,>)||
                type == typeof(SortedDictionary<,>))
            {
                IDictionary dict = (IDictionary)container;
                while(m_stream.Position < epos)
                {
                    ReadTag();
                    object key = ReadField(gen_types[0]);
                    object val = ReadField(gen_types[1]);
                    dict.Add(key, val);
                }
            }
            return container;
        }

        private void ReadTag()
        {
            uint flag = (uint)m_stream.ReadByte();
            m_ext = (flag & 0x80) != 0;
            // read tag
            m_tag = flag & 0x60;
            if (m_tag == 3)
            {
                ulong tmp = ReadVariant();
                m_tag = (uint)(tmp + 2);
            }
            ++m_tag;
            // read data
            m_val = flag & 0x0F;
            if((flag & 0x10) != 0)
            {
                ulong temp;
                if(!m_ext)
                {
                    temp = ReadVariant();
                }
                else if(m_indexs.Count > 0)
                {
                    temp = m_indexs[m_indexs.Count - 1];
                    m_indexs.RemoveAt(m_indexs.Count - 1);
                }
                else
                {
                    throw new Exception("bad packet");
                }
                m_val = (temp << 4) | m_val;
            }
        }

        private ulong ReadVariant()
        {
            ulong data = 0;
            int off = 0;
            int tmp = 0;
            do
            {
                if(off >= 64)
                    return 0;
                tmp = m_stream.ReadByte();
                data |= (ulong)(tmp & 0x7F) << off;
                off += 7;
            }while((tmp & 0x80) != 0);
            return data;
        }

        private static uint decode_group_var(byte[] buff, uint off, uint len)
        {
            int data = 0;
            int shift = 0;
            for(uint i = 0; i < len; ++i)
            {
                data |= (int)buff[i] << shift;
                shift += 8;
            }
            return (uint)data;
        }

        private static long decodei64(ulong n)
        {
            return (long)(n >> 1) ^ (-(long)(n & 1));
        }

        private static double decodef64(ulong n)
        {
            return BitConverter.Int64BitsToDouble((long)n);
        }

        private static float decodef32(ulong n)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes((int)(uint)n), 0);
        }
    }
}
