using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace proto
{
    // 属性
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ProtoFieldAttribute : Attribute
    {
        private uint tag;

        public ProtoFieldAttribute(uint tag)
        {
            this.tag = tag;
        }

        public uint Tag { get { return tag; } }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ProtoPacketAttribute : Attribute
    {
        private uint msgid;
        public ProtoPacketAttribute(uint msgid)
        {
            this.msgid = msgid;
        }
        public uint MsgID { get { return this.msgid; } }
    }

    public class MetaField
    {
        private uint m_tag;
        private MemberInfo m_info;
        public uint Tag { get { return m_tag; } }
        public Type Type { get { return m_info.GetType(); } }

        public MetaField(uint tag, MemberInfo info)
        {
            this.m_tag = tag;
            this.m_info = info;
        }

        public object GetValue(object obj)
        {
            if (m_info.MemberType == MemberTypes.Field)
                return (m_info as FieldInfo).GetValue(obj);
            else
                return (m_info as PropertyInfo).GetValue(obj, null);
        }

        public void SetValue(object obj, object val)
        {
            if (m_info.MemberType == MemberTypes.Field)
                (m_info as FieldInfo).SetValue(obj, val);
            else
                (m_info as PropertyInfo).SetValue(obj, val, null);
        }
    }

    public class MetaType : IEnumerable
    {
        public delegate object Creator();
        private bool m_serializable = false;
        private uint m_msgID;
        private Type m_type;
        private Creator m_func;
        private SortedDictionary<uint, MetaField> m_fields = new SortedDictionary<uint, MetaField>();

        public bool Serializable { get { return m_serializable; } }
        public uint MsgID { get { return m_msgID; } }

        public MetaType(Type type, Creator fun)
        {
            m_type = type;
            m_func = fun;
            Parse(type, true);
        }

        public object Create()
        {
            return m_func();
        }

        public MetaField GetField(uint id)
        {
            MetaField field;
            if (!m_fields.TryGetValue(id, out field))
                return null;
            return field;
        }

        public IEnumerator GetEnumerator()
        {
            return m_fields.GetEnumerator();
        }

        private void Parse(Type type, bool publicOnly)
        {
            // 判断是否是可序列化消息
            object[] attrs = type.GetCustomAttributes(typeof(ProtoPacketAttribute), true);
            if (attrs.Length == 0)
                return;
            m_serializable = true;
            m_msgID = (attrs[0] as ProtoPacketAttribute).MsgID;
            // 解析fields
            BindingFlags flags = publicOnly ? BindingFlags.Public | BindingFlags.Instance : BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo[] props = type.GetProperties(flags);
            FieldInfo[] fields = type.GetFields(flags);
            MemberInfo[] members = new MemberInfo[fields.Length + props.Length];
            foreach (var mem in members)
            {
                attrs = mem.GetCustomAttributes(typeof(ProtoFieldAttribute), true);
                if (attrs == null || attrs.Length == 0)
                    continue;
                ProtoFieldAttribute attr = attrs[0] as ProtoFieldAttribute;
                m_fields.Add(attr.Tag, new MetaField(attr.Tag, mem));
            }
        }
    }

    public static class ModelManager
    {
        private static Dictionary<Type, MetaType> m_metas = new Dictionary<Type, MetaType>();
        private static Dictionary<uint, MetaType> m_packets = new Dictionary<uint, MetaType>();

        public static MetaType GetMeta(Type type)
        {
            MetaType meta;
            if (!m_metas.TryGetValue(type, out meta))
                return null;
            return meta;
        }

        public static MetaType GetMeta(uint msgid)
        {
            MetaType meta;
            if (!m_packets.TryGetValue(msgid, out meta))
                return null;
            return meta;
        }

        public static void Add(Type type, MetaType.Creator fun)
        {
            MetaType meta = new MetaType(type, fun);
            m_metas.Add(type, meta);
            if (meta.MsgID != 0)
                m_packets.Add(meta.MsgID, meta);
        }

        public static object Create(Type type)
        {
            MetaType meta = GetMeta(type);
            if (meta == null)
            {
                return Activator.CreateInstance(type);
            }
            return meta.Create();
        }

        public static object Create(uint msgid)
        {
            MetaType meta = GetMeta(msgid);
            if (meta == null)
                return null;
            return meta.Create();
        }
    }
}
