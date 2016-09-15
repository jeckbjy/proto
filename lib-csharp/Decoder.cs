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
        // read tag
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

        public Decoder Read<T>(out T data, uint tag = 1)
        {
            if (PreRead(tag))
                data = (T)ReadField(typeof(T));
            else
                data = default(T);
            return this;
        }

        private bool PreRead(uint tag)
        {
            if (EndOfStream)
                return false;

            if(m_tag == 0)
            {
                if (!ReadTag())
                    return false;
            }

            while(tag > m_tag)
            {
                // skip not used
                if (m_ext && m_val > 0)
                    m_stream.Seek((long)m_val, SeekOrigin.Current);

                if (!ReadTag())
                    return false;
            }

            m_tag -= tag;
            if (m_tag != 0)
                m_val = 0;

            return true;
        }

        private object ReadField(Type type)
        {
            return ReadObject(type, m_val);
        }

        private object ReadObject(Type type, ulong data)
        {
            if (type.IsValueType)
            {
                return Converter.decode(type, data);
            }

            // parse complex body data:length + context

            // suspend end position
            long epos = m_epos;
            m_epos = m_stream.Position + (long)data;
            object result = ReadContext(type, (int)data);
            // 保证全部读取
            m_stream.Seek(epos, SeekOrigin.Begin);
            // 恢复
            m_epos = epos;

            return null;
        }

        private object ReadContext(Type type, int length)
        {
            // parse context
            if (type == typeof(string))
            {
                byte[] buff = new byte[length];
                if (ReadData(buff, length))
                    return Encoding.UTF8.GetString(buff);
            }
            else if (type == typeof(MemoryStream))
            {
                MemoryStream stream = new MemoryStream();
                stream.SetLength(length);
                if (ReadData(stream.GetBuffer(), length))
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
                ulong variant = 0;
                Type type_def = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
                // LinkedList:暂时无法支持
                Type[] gen_types = type.GetGenericArguments();
                object container = NewObject(type);

                if (typeof(IList).IsAssignableFrom(type_def))
                {
                    IList list = (IList)container;
                    while (!EndOfStream)
                    {
                        if (!ReadVariant(out variant))
                            return null;

                        object obj = ReadObject(gen_types[0], variant);
                        if (obj != null)
                            list.Add(obj);
                    }
                }
                else if (typeof(IDictionary<,>).IsAssignableFrom(type_def))
                {
                    IDictionary dict = (IDictionary)container;
                    while (!EndOfStream)
                    {
                        if (!ReadVariant(out variant))
                            return null;
                        object key = ReadObject(gen_types[0], variant);

                        if (!ReadVariant(out variant))
                            return null;
                        object val = ReadObject(gen_types[1], variant);

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

        private bool ReadData(byte[] buffer, int count, int offset = 0)
        {
            if (m_stream.Position + count > m_epos)
                return false;

            m_stream.Read(buffer, offset, count);
            return true;
        }

        private bool ReadVariant(out ulong data)
        {
            data = 0;
            int off = 0;
            int tmp = 0;
            do
            {
                if (off >= 64)
                    return false;

                if (EndOfStream)
                    return false;

                tmp = m_stream.ReadByte();
                data |= (ulong)(tmp & 0x7F) << off;
                off += 7;
            } while ((tmp & 0x80) != 0);

            return true;
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
                if (!ReadVariant(out tmp))
                    return false;
                tag += (uint)tmp;
                tag += 2;
            }

            tag += 1;

            // parse value
            val = flag & 0x0f;
            if((flag & 0x10) != 0)
            {
                if (!ReadVariant(out tmp))
                    return false;
                val |= (tmp << 4);
            }

            m_tag = tag;
            m_val = val;
            m_ext = ext;

            return true;
        }
    }
}
