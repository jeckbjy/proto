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
        public ProtoPacketAttribute(uint msgid = uint.MaxValue)
        {
            this.msgid = msgid;
        }
        public bool HasMsgID { get { return msgid != uint.MaxValue; } }
        public uint MsgID { get { return this.msgid; } }
    }

    public class MetaField
    {
        private uint m_tag;
        private Type m_type;
        private MemberInfo m_info;
        public uint Tag { get { return m_tag; } }
        public Type Type { get { return m_type; } }

        public MetaField(uint tag, MemberInfo info)
        {
            this.m_tag = tag;
            this.m_info = info;
            if (info.MemberType == MemberTypes.Property)
                m_type = (info as PropertyInfo).PropertyType;
            else if (info.MemberType == MemberTypes.Field)
                m_type = (info as FieldInfo).FieldType;
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
        private bool m_serializable = false;
        private uint m_msgID;
        private Type m_type;
        private SortedDictionary<uint, MetaField> m_fields = new SortedDictionary<uint, MetaField>();

        public bool Serializable { get { return m_serializable; } }
        public uint MsgID { get { return m_msgID; } }

        public MetaType(Type type)
        {
            m_type = type;
            Parse(type, true);
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
            AddFields(type.GetProperties(flags));
            AddFields(type.GetFields(flags));
        }

        private void AddFields<T>(T[] fields) where T:MemberInfo
        {
            foreach(var mem in fields)
            {
                object[] attrs = mem.GetCustomAttributes(typeof(ProtoFieldAttribute), true);
                if (attrs == null || attrs.Length == 0)
                    continue;
                ProtoFieldAttribute attr = attrs[0] as ProtoFieldAttribute;
                m_fields.Add(attr.Tag, new MetaField(attr.Tag, mem));
            }
        }
    }

    public static class ModelManager
    {
        public delegate object Creator();
        private static Dictionary<uint, Creator> m_msgs = new Dictionary<uint, Creator>();
        private static Dictionary<Type, Creator> m_types = new Dictionary<Type, Creator>();
        private static Dictionary<Type, MetaType> m_metas = new Dictionary<Type, MetaType>();

        public static MetaType GetMeta(Type type)
        {
            MetaType meta;
            if (!m_metas.TryGetValue(type, out meta))
            {
                meta = new MetaType(type);
                if (meta.Serializable)
                {
                    m_metas.Add(type, meta);
                    return meta;
                }
                else
                {
                    return null;
                }
            }
            return meta;
        }

        public static void Add(Type type, Creator fun)
        {
            m_types.Add(type, fun);
            // 校验是否是消息
            object[] attrs = type.GetCustomAttributes(typeof(ProtoPacketAttribute), true);
            if(attrs.Length > 0)
            {
                ProtoPacketAttribute attr = attrs[0] as ProtoPacketAttribute;
                if (attr.HasMsgID)
                    m_msgs.Add(attr.MsgID, fun);
            }
        }

        public static object Create(Type type)
        {
            Creator fun;
            if(!m_types.TryGetValue(type, out fun))
            {
                return Activator.CreateInstance(type);
            }
            return fun();
        }

        public static object Create(uint msgid)
        {
            Creator fun;
            if (!m_msgs.TryGetValue(msgid, out fun))
                return null;
            return fun();
        }
    }
}
