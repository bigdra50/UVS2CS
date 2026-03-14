using System.Text;

namespace UVS2CS.IRToCSharp
{
    public sealed class IndentWriter
    {
        readonly StringBuilder _sb = new();
        int _indentLevel;
        bool _lineStart = true;
        const string IndentUnit = "    ";

        public void Indent() => _indentLevel++;
        public void Unindent() => _indentLevel--;

        public void Write(string text)
        {
            if (_lineStart && _indentLevel > 0)
            {
                for (var i = 0; i < _indentLevel; i++)
                    _sb.Append(IndentUnit);
                _lineStart = false;
            }
            _sb.Append(text);
        }

        public void WriteLine(string text)
        {
            Write(text);
            _sb.AppendLine();
            _lineStart = true;
        }

        public void WriteLine()
        {
            _sb.AppendLine();
            _lineStart = true;
        }

        public void OpenBrace()
        {
            WriteLine("{");
            Indent();
        }

        public void CloseBrace()
        {
            Unindent();
            WriteLine("}");
        }

        public override string ToString() => _sb.ToString();
    }
}
