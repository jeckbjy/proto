using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace proto
{
    enum TokenType
    {
        Error,          // 发生错误
        LineEof,        // 一行结束
        Eof,            // 结束
        Notes,          // 注释
        String,         // 字符
        Number,         // 整数数字
        SeparateBeg,    // 分隔字符
        LTBraceBracket, // {
        RTBraceBracket, // }
        LTSquareBracket,// [
        RTSquareBracket,// ]
        LTAngleBracket, // <
        RTAngleBracket, // >
        LTParenthes,    // (
        RTParenthes,    // )
        Equal,          // =
        Opposite,       // !
        Comma,          // ,
        Asterisk,       // *
        Semicolon,      // ;
        SeparateEnd,    // 分隔符结束

        // alias
        ScopeOpen  = LTBraceBracket,
        ScopeClose = RTBraceBracket,
    }

    struct Token
    {
        public TokenType type;
        public string data;
    }

    public class ProtoParser
    {
        const string DELIM = ";,:*={}[]<>";
        private int m_line = 0;
        private string m_path;
        private StreamReader m_reader;
        private StringBuilder m_builder;
        private Token m_token;
        private Proto m_proto;

        public ProtoParser()
        {
            m_builder = new StringBuilder();
        }

        public Proto Parse(string path)
        {
            m_path = path;
            m_reader = new StreamReader(File.OpenRead(path));
            m_builder.Length = 0;
            
            m_proto = new Proto(path);
            while (true)
            {
                Next();

                if (IsLineBreak(m_token.type))
                    continue;

                // todo:log
                if (m_token.type != TokenType.String)
                    break;

                string str = m_token.data as string;
                switch (str)
                {
                    case "import":
                        {
                            ParseImport();
                        }
                        break;
                    case "enum":
                        {
                            ParseEnum();
                        }
                        break;
                    case "struct":
                        {
                            ParseStruct();
                        }
                        break;
                }
            }

            m_reader.Close();
            return m_proto;
        }

        private void ParseImport()
        {
            Next();
            if (m_token.type != TokenType.String)
                Throw("import parse error, must string!");

            string file = m_token.data as string;
            if (!file.EndsWith(".proto"))
                throw new Exception("import must end with .proto");

            file = file.Substring(0, file.Length - 6);

            m_proto.AddImport(file);
        }

        private void ParseEnum()
        {
            Next();
            if (m_token.type != TokenType.LTBraceBracket)
                Throw("enum must start with {");

            EnumScope scope = new EnumScope();

            while (true)
            {
                Next();
                // check
                if (m_token.type == TokenType.RTBraceBracket)
                    break;

                if (IsLineBreak(m_token.type))
                    continue;

                // fields
                if (m_token.type != TokenType.String)
                    Throw("enum filed must start with word!");

                Field field = new Field();
                field.name = m_token.data;

                Next();

                if(m_token.type == TokenType.Equal)
                {
                    Next();
                    if (m_token.type != TokenType.Number)
                        Throw("parse enum filed error, index must be number!");
                    field.index = Int32.Parse(m_token.data);
                }
                else if(!IsLineBreak(m_token.type))
                {
                    Throw("parse enum field error!");
                }

                scope.AddField(field);
            }

            m_proto.AddScope(scope);
        }

        private void ParseStruct()
        {
            Next();

            StructScope scope = new StructScope();

            // like: struct StrutName = S2C_LoginMsg
            if(m_token.type == TokenType.Equal)
            {
                Next();

                if (!IsTokenWorld(m_token.type))
                    Throw("bad struct!");

                scope.id_name = m_token.data;
            }

            if (m_token.type != TokenType.LTBraceBracket)
                Throw("enum must start with {");

            while(true)
            {
                Next();

                // check
                if (m_token.type == TokenType.RTBraceBracket)
                    break;

                if (IsLineBreak(m_token.type))
                    continue;

                // fields
                if (m_token.type != TokenType.String)
                    Throw("enum filed must start with word!");

                StructField field = new StructField();
                ParseStructField(field);
                scope.AddField(field);
            }

            m_proto.AddScope(scope);
        }

        private void ParseStructField(StructField field)
        {
            // parse type
            field.container = Util.GetContainerType(m_token.data);
            if (field.container != Container.NONE)
            {
                Next();
                if (m_token.type != TokenType.LTAngleBracket)
                    Throw("not find <!");

                switch (field.container)
                {
                    case Container.MAP:
                    case Container.HMAP:
                        field.key.SetName(m_token.data);
                        if (field.key.IsStruct)
                            Throw("key cannot struct type!!!");
                        Next();
                        if (m_token.type != TokenType.Comma)
                            Throw("bad field,not find ,!!!");
                        Next();
                        if (m_token.type != TokenType.String)
                            Throw("bad field, not value type!");
                        field.value.SetName(m_token.data);
                        break;
                    default:
                        field.value.SetName(m_token.data);
                        break;
                }

                Next();
                if (m_token.type != TokenType.RTAngleBracket)
                    Throw("bad field type, not find >");
            }
            else
            {
                field.value.SetName(m_token.data);
            }

            // parse field pointer
            Next();
            if (m_token.type == TokenType.Asterisk)
            {
                field.pointer = true;
                Next();
            }

            // parse filed name
            if (m_token.type != TokenType.String)
                Throw("bad field, not find field name!");

            field.name = m_token.data;

            // others
            Next();

            if (m_token.type == TokenType.Equal)
            {
                Next();
                if (m_token.type != TokenType.Number)
                    Throw("bad struct filed!");

                field.index = Int32.Parse(m_token.data);

                Next();
            }

            // options
            if (m_token.type == TokenType.LTSquareBracket)
            {
                ParseOptions(field);
            }
        }

        private void ParseOptions(StructField field)
        {
            while (true)
            {
                Next();
                if (m_token.type == TokenType.RTSquareBracket)
                    break;

                if (m_token.type == TokenType.Comma)
                    continue;

                if (m_token.type != TokenType.String)
                    Throw("bad filed options");
                
                string key = m_token.data;

                Next();
                if (m_token.type != TokenType.Equal)
                    Throw("bad filed options");
                Next();

                if (m_token.type != TokenType.String &&
                    m_token.type != TokenType.Number)
                    Throw("bad filed options");

                string value = m_token.data;
                switch (key)
                {
                    case "delete":
                    case "deprecated":
                        field.deprecated = bool.Parse(value);
                        break;
                }
            }
        }

        public void Next(bool ignorNote = true)
        {
            m_token.type = TokenType.Error;

            char ch;

            while(true)
            {
                if (!m_reader.EndOfStream)
                    return;
                ch = (char)m_reader.Read();
                if (!char.IsWhiteSpace(ch) && ch != '\r')
                    break;
            }

            switch (ch)
            {
                case '\n':
                    m_token.type = TokenType.LineEof;
                    break;
                case '#':
                    {// 单行注释
                        m_token.type = TokenType.Notes;
                        m_token.data = m_reader.ReadLine();
                    }
                    break;
                case '/':
                    {// 单行或者多行注释
                        ParseNote();
                    }
                    break;
                case '\"':
                    {// 字符串
                        ParseString();
                    }
                    break;
                default:
                    {
                        if (DELIM.IndexOf(ch) != -1)
                        {
                            ParseSeparate(ch);
                        }
                        else if (char.IsLetterOrDigit(ch))
                        {
                            ParseWord(ch);
                        }
                    }
                    break;
            }

            if (m_token.type == TokenType.Error)
                Throw("parse error!");
        }

        private void ParseNote()
        {
            char ch;
            m_token.type = TokenType.Error;

            ch = ReadChar();
            if (ch == '/')
            {
                m_token.type = TokenType.Notes;
                m_token.data = ReadLine();
            }
            else if (ch == '*')
            {
                m_builder.Length = 0;
                bool prevChar = false;
                while (!m_reader.EndOfStream)
                {
                    ch = ReadChar();
                    if (prevChar)
                    {
                        if (ch == '/')
                        {
                            m_token.type = TokenType.Notes;
                            m_token.data = m_builder.ToString();
                            break;
                        }
                        else
                        {
                            m_builder.Append('*');
                            prevChar = false;
                        }
                    }
                    else if (ch == '*')
                    {
                        prevChar = true;
                    }
                    else
                    {
                        m_builder.Append(ch);
                    }
                }
            }
        }

        private void ParseString()
        {
            char ch;
            m_builder.Length = 0;

            m_token.type = TokenType.Error;

            while(!m_reader.EndOfStream)
            {
                ch = (char)m_reader.Read();

                if (IsLineBreak(ch))
                    break;

                if(ch == '\"')
                {
                    m_token.type = TokenType.String;
                    m_token.data = m_builder.ToString();
                    break;
                }

                m_builder.Append(ch);
            }
        }

        private void ParseWord(char ch)
        {
            m_builder.Length = 0;
            m_builder.Append(ch);
            while(!m_reader.EndOfStream)
            {
                ch = (char)m_reader.Read();
                if (!char.IsLetterOrDigit(ch))
                    break;
                m_builder.Append(ch);
            }

            string data = m_builder.ToString();
            if(char.IsDigit(data[0]))
            {
                m_token.type = TokenType.Number;
                m_token.data = data;
            }
            else
            {
                m_token.type = TokenType.String;
                m_token.data = data;
            }
        }

        private void ParseSeparate(char ch)
        {
            m_token.data = new string(ch, 1);
            TokenType type = TokenType.Error;
            switch(ch)
            {
                case '{': 
                    type = TokenType.LTBraceBracket;
                    break;
                case '}': 
                    type = TokenType.RTBraceBracket;
                    break;
                case '[': 
                    type = TokenType.LTSquareBracket;
                    break;
                case ']': 
                    type = TokenType.RTSquareBracket;
                    break;
                case '<': 
                    type = TokenType.LTAngleBracket;
                    break;
                case '>': 
                    type = TokenType.RTAngleBracket;
                    break;
                case '(':
                    type = TokenType.LTParenthes;
                    break;
                case ')':
                    type = TokenType.RTParenthes;
                    break;
                case '=': 
                    type = TokenType.Equal;
                    break;
                case ',': 
                    type = TokenType.Comma;
                    break;
                case '*':
                    type = TokenType.Asterisk;
                    break;
                case '!': 
                    type = TokenType.Opposite; 
                    break;
                case ';':
                    type = TokenType.Semicolon;
                    break;
                default:
                    break;
            }

            m_token.type = type;
        }

        private bool IsLineBreak(char ch)
        {
            return ch == '\n' || ch == ';';
        }

        private bool IsLineBreak(TokenType type)
        {
            return type == TokenType.LineEof || type == TokenType.Semicolon;
        }

        private bool IsTokenWorld(TokenType type)
        {
            return type == TokenType.String || type == TokenType.Number;
        }

        private bool IsSeparateChar(TokenType type)
        {
            return type > TokenType.SeparateBeg && type < TokenType.SeparateEnd;
        }

        private char ReadChar()
        {
            char ch = (char)0;
            if(!m_reader.EndOfStream)
            {
                ch = (char)m_reader.Read();
                if (ch == '\n')
                    ++m_line;
            }

            return ch;
        }

        private string ReadLine()
        {
            if(!m_reader.EndOfStream)
            {
                ++m_line;
                return m_reader.ReadLine();
            }

            return string.Empty;
        }

        private void Throw(string str)
        {
            throw new Exception(string.Format("{0} in {1}:{2}", str, m_path, m_line));
        }
    }
}
