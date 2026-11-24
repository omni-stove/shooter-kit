using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shooter.Server.Tests
{
    // 数分ぶんの試合の行を StringWriter に溜めて毎 tick 読み直すと遅いので、行に分けて溜め、増えた分だけを見られるようにする
    internal sealed class LineLog : TextWriter
    {
        private readonly StringBuilder _line = new StringBuilder();

        public List<string> Lines { get; } = new List<string>();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                Lines.Add(_line.ToString().TrimEnd('\r'));
                _line.Clear();
                return;
            }

            _line.Append(value);
        }
    }
}
