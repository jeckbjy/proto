using System;
using System.Text;
using System.IO;

namespace proto
{
    class ProtoReader
    {
        // 关键标示
        const string DELIM = ",:*={}[]<>";
        int m_line = 0;
        StreamReader reader;

        public bool EndOfStream
        {
            get { return reader.EndOfStream; }
        }

        public int Line
        {
            get { return m_line; }
        }

        static bool isWordChar(char ch)
        {
            return Char.IsLetterOrDigit(ch) || ch == '-' || ch == '_';
        }

        public ProtoReader(string path)
        {
            reader = new StreamReader(File.OpenRead(path));
        }

        public string ReadLine()
        {
            // 1:trim 2:remove last ; and , 3:replace \t to ' ', 4 = insert ' '
            // igonre note
            StringBuilder builder = new StringBuilder();
            while (builder.Length == 0)
            {
                if (reader.EndOfStream)
                    return null;
                char lastChar;
                bool quot = false;
                uint quotSize = 0;
                while (!reader.EndOfStream)
                {
                    lastChar = builder.Length > 0 ? builder[builder.Length - 1] : '\0';
                    char ch = (char)reader.Read();
                    if (ch == '\n')
                    {
                        ++m_line;
                        break;
                    }
                    else if(ch == '#')
                    {// single line node
                        ++m_line;
                        reader.ReadLine();
                        break;
                    }
                    else if( ch == '\r' || ch == ';')
                    {// 忽略这两个字符，以行分隔，无需这些修饰
                        continue;
                    }
                    else if (ch == '\"' || ch == '\'')
                    {// 校验是否字符引用
                        ++quotSize;
                        quot = !quot;
                        builder.Append('\"');
                        continue;
                    }
                    else if (quot)
                    {// quot内不处理，直接插入
                        builder.Append(ch);
                        continue;
                    }

                    // 其他正常情况处理
                    if (DELIM.IndexOf(ch) != -1)
                    {// 关键分隔符两边插入空格便于分隔
                        if (builder.Length != 0 && lastChar != ' ')
                            builder.Append(' ');
                        builder.Append(ch);
                        builder.Append(' ');
                    }
                    else if (Char.IsWhiteSpace(ch))
                    {// 忽略开始空格和多余空格
                        if (builder.Length != 0 && lastChar != ' ')
                            builder.Append(' ');
                    }
                    else if(isWordChar(ch))
                    {// 正常允许的字符
                        builder.Append(ch);
                    }
                    else
                    {
                        throw new Exception("bad char in line" + m_line + builder.ToString());
                    }
                }

                // 校验quot
                if(quotSize > 0)
                {
                    if (quotSize != 2)
                        throw new Exception("quot not equal 2" + m_line + builder.ToString());
                    if (builder.Length < 7 || !builder.ToString().StartsWith("import "))
                        throw new Exception("just import can has quot");
                }
            }

            // 末尾必须是单词词素,去除多余字符
            while(builder.Length != 0)
            {
                char ch = builder[builder.Length - 1];
                if (ch != ' ' && ch != ',' && ch != ';')
                    break;
                builder.Remove(builder.Length - 1, 1);
            }
            
            return builder.ToString();
        }
    }
}
