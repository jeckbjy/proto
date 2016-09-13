using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace proto
{
    class Decoder
    {
        private MemoryStream m_stream;
        private long m_epos;
        private uint m_tag = 0;

        private ulong m_val;
        private bool  m_ext;

        private bool EndOfStream
        {
            get { return m_stream.Position >= m_epos; }
        }

        Decoder(MemoryStream stream)
        {
            m_stream = stream;
            m_epos = stream.Length;
        }

        public bool Decode(IMessage msg)
        {
            return true;
        }

        private object ReadField(Type type)
        {
            object result = null;
            if(m_ext)
            {
                long epos = m_epos;
                m_epos = m_stream.Position + (long)m_val;
                result = ReadObject(type);
                m_epos = epos;
            }
            else if(type.IsValueType)
            {
                result = ReadObject(type);
            }

            return result;
        }

        private object ReadObject(Type type)
        {
            if (type.IsValueType)
            {
                return Converter.decode(type, m_val);
            }
            else if (type == typeof(string))
            {
                byte[] buff = new byte[m_val];
                if (Read(buff, (int)m_val))
                    return Encoding.UTF8.GetString(buff);
            }
            else if (type == typeof(MemoryStream))
            {
                MemoryStream stream = new MemoryStream();
                stream.SetLength((int)m_val);
                if (Read(stream.GetBuffer(), (int)m_val))
                    return stream;
            }
            else if (typeof(IMessage).IsAssignableFrom(type))
            {// 消息解析
                IMessage msg = (IMessage)NewObject(type);
                msg.Decode(this);
                return msg;
            }
            else if (type.IsGenericType)
            {// 泛型解析
                Type type_def = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
                // LinkedList:暂时无法支持
                Type[] gen_types = type.GetGenericArguments();
                object container = NewObject(type);

                if (typeof(IList).IsAssignableFrom(type_def))
                {
                    IList list = (IList)container;
                    while (!EndOfStream)
                    {
                        object obj = ReadObject(gen_types[0]);
                        if (obj != null)
                            list.Add(obj);
                    }
                }
                else if (typeof(IDictionary<,>).IsAssignableFrom(type_def))
                {
                    IDictionary dict = (IDictionary)container;
                    while (!EndOfStream)
                    {
                        object key = ReadObject(gen_types[0]);
                        object val = ReadObject(gen_types[1]);
                        if (key != null && val != null)
                            dict.Add(key, val);
                    }
                }

                return container;
            }

            return null;
        }

        private object NewObject(Type type)
        {
            // unity ios 不支持
            return Activator.CreateInstance(type);
        }

        private long suspend(long count)
        {
            long epos = m_epos;
            m_epos = m_stream.Position + count;
            return epos;
        }

        private void recovery(long epos)
        {
            m_epos = epos;
        }

        private bool Read(byte[] buffer, int count, int offset = 0)
        {
            if (m_stream.Position + count > m_epos)
                return false;

            m_stream.Read(buffer, offset, count);
            return true;
        }

        private ulong ReadVariant()
        {
            ulong data = 0;
            int off = 0;
            int tmp = 0;
            do
            {
                if (off >= 64)
                    return 0;
                tmp = m_stream.ReadByte();
                data |= (ulong)(tmp & 0x7F) << off;
                off += 7;
            } while ((tmp & 0x80) != 0);
            return data;
        }

        private bool ReadTag()
        {
            if (EndOfStream)
                return false;

            uint  flag;
            uint  tag;
            ulong val;
            ulong tmp;
            bool  ext;

            flag = (uint)m_stream.ReadByte();
            ext = (flag & 0x80) != 0;

            // parse tag
            tag = flag & 0x60;
            if(tag == 3)
            {
                tmp = ReadVariant();
                tag += (uint)tmp;
                tag += 2;
            }

            tag += 1;

            // parse value
            val = flag & 0x0f;
            if((flag & 0x10) != 0)
            {
                tmp = ReadVariant();
                val |= (tmp << 4);
            }

            m_tag = tag;
            m_val = val;
            m_ext = ext;

            return true;
        }
    }
}
