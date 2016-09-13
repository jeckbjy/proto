using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace proto
{
    class Encoder
    {
        private MemoryStream m_stream;
        private int m_tag = 0;

        public Encoder()
        {
            m_stream = new MemoryStream();
        }

        public Encoder(MemoryStream stream)
        {
            m_stream = stream;
        }

        public void Encode(IMessage packet)
        {

        }

        public Encoder Write<T>(T data, int tagOffset = 1)
        {
            m_tag += tagOffset;
            if (WriteField(data, m_tag))
                m_tag = 0;

            return this;
        }

        private bool WriteField(object obj, int tag)
        {
            Type type = obj.GetType();
            if(type.IsValueType)
            {
                ulong data = proto.Converter.encode(obj);
                if (data == 0)
                    return false;
                WriteTag(tag, data, false);
            }
            else if(type == typeof(string))
            {
                string str = obj as string;
                if (string.IsNullOrEmpty(str))
                    return false;

                byte[] buff = Encoding.UTF8.GetBytes(str);
                WriteTag(tag, (ulong)buff.Length, true);
                Write(buff, buff.Length);
            }
            else if(type == typeof(MemoryStream))
            {
                MemoryStream stream = obj as MemoryStream;
                if (stream.Length == 0)
                    return false;

                byte[] buff = stream.GetBuffer();
                WriteTag(tag, (ulong)buff.Length, true);
                Write(buff, buff.Length);
            }
            else if(typeof(IMessage).IsAssignableFrom(type))
            {
                IMessage msg = obj as IMessage;
                MemoryStream stream = new MemoryStream();
                Encoder encoder = new Encoder(stream);
                msg.Encode(encoder);
                if (stream.Length == 0)
                    return false;

                byte[] buff = stream.GetBuffer();
                WriteTag(tag, (ulong)buff.Length, true);
                Write(buff, buff.Length);
            }
            else if(type.IsGenericType)
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
            else
            {
                throw new Exception("bad type");
            }

            return true;
        }

        private void WriteItem(object obj)
        {
            Type type = obj.GetType();
            if (type.IsValueType)
            {
                ulong data = proto.Converter.encode(obj);
                WriteValue(data);
            }
            else if (type == typeof(string))
            {
                string str = obj as string;

                byte[] buff = Encoding.UTF8.GetBytes(str);
                WriteValue(buff);
            }
            else if (type == typeof(MemoryStream))
            {
                MemoryStream stream = obj as MemoryStream;
                byte[] buff = stream.GetBuffer();
                WriteValue(buff);
            }
            else if (typeof(IMessage).IsAssignableFrom(type))
            {
                IMessage msg = obj as IMessage;
                MemoryStream stream = new MemoryStream();
                Encoder encoder = new Encoder(stream);
                msg.Encode(encoder);

                byte[] buff = stream.GetBuffer();
                WriteValue(buff);
            }
            else
            {
                throw new Exception("bad type");
            }
        }

        private void WriteValue(ulong length)
        {
            byte[] value = new byte[10];
            int size = EncodeVar(value, 0, length);
            m_stream.Write(value, 0, size);
        }

        private void WriteValue(byte[] buff)
        {
            int length = buff.Length;

            byte[] value = new byte[10];
            int size = EncodeVar(value, 0, (ulong)length);
            Write(value, size);
            if (length > 0)
                Write(buff, length);
        }

        private void WriteTag(int tag, UInt64 val, bool ext)
        {
            byte[] data = new byte[20];
            int size = EncodeTag(data, tag, val, ext);
            Write(data, size);
        }
       
        private void Write(byte[] data, int size)
        {
            m_stream.Write(data, 0, (int)size);
        }

        private int EncodeVar(byte[] buff, int offset, ulong data)
        {
            //外部确保buff足够，int32最多5位，int64最多10位
            //高位标识：0表示结尾,1表示后边还有数据
            int count = offset;
            while (data > 0x7F)
            {
                buff[count++] = (byte)(data & 0x7F | 0x80);
                data >>= 7;
            }
            buff[count++] = (byte)(data & 0x7F);
            return count - offset;
        }

        private int EncodeTag(byte[] buff, int tag, ulong val, bool ext)
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

            // encode body
            int length = 1;
            buff[0] = flag;

            if (tag > 0)
                length += EncodeVar(buff, length, (ulong)tag);

            if (val > 0)
                length += EncodeVar(buff, length, val);

            return length;
        }
    }
}
