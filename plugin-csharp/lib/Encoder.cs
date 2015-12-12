using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace proto
{
    public class Encoder
    {
        private class TagInfo
        {
            public long tpos;
            public long bpos;
            public uint size;
        }
        private List<TagInfo> m_indexs = new List<TagInfo>();
        private Stream m_stream;
        private long m_spos;    // 数据起始位置

        public Encoder(Stream stream)
        {
            m_stream = stream;
            m_spos = stream.Position;
        }

        public void Encode<T>(T msg)
        {
            MetaType meta = ModelManager.GetMeta(typeof(T));
            if (meta == null)
                return;
            // clear
            m_indexs.Clear();
            // 预留空间
            byte[] buff = new byte[13];
            byte flag;
            long bpos, ipos, epos;
            // 预留head
            m_stream.Write(buff, 0, buff.Length);
            // 写入msg
            bpos = m_stream.Position;
            WritePacket(meta, msg);
            // 写入index
            ipos = m_stream.Position;
            if (m_indexs.Count > 0)
            {// 应该从后向前写入
                for (int i = m_indexs.Count - 1; i >= 0; --i)
                {
                    TagInfo info = m_indexs[i];
                    if(info.size > 0)
                        WriteVariant(info.size);
                }
                m_indexs.Clear();
            }
            epos = m_stream.Position;
            // 序列化头部
            uint len1, len2, len3;
            uint len = 1;
            len1 = PackHead(buff, len, epos - bpos);
            len += len1;
            len2 = PackHead(buff, len, epos - ipos);
            len += len2;
            len3 = PackHead(buff, len, meta.MsgID);
            len += len3;
            flag = (byte)((len1 << 5) + (len2 << 2) + len3);
            buff[0] = flag;
            m_spos = bpos - len;
            m_stream.Seek(m_spos, SeekOrigin.Begin);
            m_stream.Write(buff, 0, (int)len);
            m_stream.Seek(m_spos, SeekOrigin.Begin);
        }

        private byte PackHead(byte[] buff, uint off, long data)
        {
            if (data == 0)
                return 0;
            byte count = 0;
            while (data > 0)
            {
                buff[off + count++] = (byte)data;
                data >>= 8;
            }
            return count;
        }

        private void WritePacket(MetaType meta, object instance)
        {
            uint last_tag = 0;
            foreach (KeyValuePair<uint, MetaField> kv in meta)
            {
                MetaField field = kv.Value;
                object obj = field.GetValue(instance);
                if (obj == null)
                    continue;
                if(CanWrite(obj))
                {
                    WriteField(field.Tag - last_tag, obj);
                    last_tag = field.Tag;
                }
            }
        }

        private bool CanWrite(object obj)
        {
            Type type = obj.GetType();
            if(type.IsValueType)
            {
                ulong data = encode(obj);
                return data != 0;
            }
            else if (type == typeof(string))
            {
                if ((obj as string).Length == 0)
                    return false;
            }
            else if (type == typeof(MemoryStream))
            {
                if ((obj as MemoryStream).Length == 0)
                    return false;
            }
            else if (type.IsGenericType)
            {
                if ((obj as ICollection).Count == 0)
                    return false;
            }
            return true;
        }

        private bool WriteField(uint tag, object obj)
        {
            if (obj == null)
                return false;
            Type type = obj.GetType();
            if(type.IsValueType)
            {// 普通类型
                ulong data = encode(obj);
                WriteTag(tag, data, false);
                return true;
            }
            else
            {
                // 写入tag
                int index;
                WriteBegin(tag, out index);
                WriteComplex(type, obj);
                WriteEnd(index);
                return true;
            }
        }

        private void WriteComplex(Type type, object obj)
        {
            if (type.IsGenericType)
            {
                int count = type.GetGenericArguments().Length;
                // 一个一个写入
                if (count == 1)
                {
                    IEnumerator itor = (obj as IEnumerable).GetEnumerator();
                    while (itor.MoveNext())
                    {
                        WriteItem(itor.Current);
                    }
                }
                else if (count == 2)
                {
                    IDictionaryEnumerator itor = (IDictionaryEnumerator)(obj as IEnumerable).GetEnumerator();
                    while (itor.MoveNext())
                    {
                        WriteItem(itor.Key);
                        WriteItem(itor.Value);
                    }
                }
            }
            else if(type == typeof(string))
            {
                byte[] buff = Encoding.UTF8.GetBytes(obj as string);
                m_stream.Write(buff, 0, buff.Length);
            }
            else if(type == typeof(MemoryStream))
            {
                MemoryStream stream = obj as MemoryStream;
                byte[] buff = stream.GetBuffer();
                m_stream.Write(buff, 0, (int)stream.Length);
            }
            else if(type.IsClass)
            {
                MetaType meta = ModelManager.GetMeta(type);
                if (meta != null)
                    WritePacket(meta, obj);
            }
        }

        private void WriteItem(object obj)
        {
            if (obj == null)
                return;
            // do pack??
            WriteField(1, obj);
        }

        private void WriteVariant(ulong data)
        {
            byte[] buff = new byte[10];
            int count = 0;
            while (data > 0x7F)
            {
                buff[count++] = (byte)(data & 0x7F | 0x80);
                data >>= 7;
            }
            buff[count++] = (byte)(data & 0x7F);
            m_stream.Write(buff, 0, count);
        }

        private void WriteTag(uint tag, ulong val, bool ext)
        {
            byte flag = (byte)(ext ? 0x80 : 0);
            tag -= 1;
            if (tag < 3)
            {
                flag |= (byte)(tag << 5);
                tag = 0;
            }
            else
            {
                flag |= 0x60;
                tag -= 2;
            }
            // 写入低4位
            flag |= (byte)(val & 0x0F);
            val >>= 4;
            if (val > 0)
                flag |= 0x10;
            // 写入数据
            m_stream.WriteByte(flag);
            // 写入var
            if (tag > 0)
                WriteVariant(tag);
            if (val > 0)
                WriteVariant(val);
        }

        private void WriteBegin(uint tag, out int index)
        {
            index = m_indexs.Count;
            TagInfo info = new TagInfo();
            info.tpos = m_stream.Position;
            WriteTag(tag, 0, true);
            info.bpos = m_stream.Position;
            m_indexs.Add(info);
        }

        private void WriteEnd(int index)
        {
            TagInfo info = m_indexs[index];
            long epos = m_stream.Position;
            long leng = epos - info.bpos;
            m_stream.Seek(info.tpos, SeekOrigin.Begin);
            byte flag = (byte)m_stream.ReadByte();
            flag |= (byte)(leng & 0x0F);
            leng >>= 4;
            if(leng > 0)
            {
                flag |= 0x10;
                info.size = (uint)leng;
            }
            m_stream.Seek(-1, SeekOrigin.Current);
            m_stream.WriteByte(flag);
            m_stream.Seek(epos, SeekOrigin.Begin);
        }

        private ulong encodei64(long n)
        {
            return (ulong)((n << 1) ^ (n >> 63));
        }

        private ulong encodef64(double n)
        {
            return (ulong)BitConverter.DoubleToInt64Bits(n);
        }

        private ulong encodef32(float n)
        {
            return (uint)BitConverter.ToInt32(BitConverter.GetBytes(n), 0);
        }

        private ulong encode(object obj)
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
                    data = encodei64((sbyte)obj);
                    break;
                case TypeCode.Int16:
                    data = encodei64((short)obj);
                    break;
                case TypeCode.Int32:
                    data = encodei64((int)obj);
                    break;
                case TypeCode.Int64:
                    data = encodei64((long)obj);
                    break;
                case TypeCode.Single:
                    data = encodef32((float)obj);
                    break;
                case TypeCode.Double:
                    data = encodef64((double)obj);
                    break;
                default:
                    return 0;
            }
            return data;
        }
    }
}
