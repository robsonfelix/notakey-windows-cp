using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace Notakey.Utility
{
    public class Logger
    {
        static readonly object ThreadLock = new object();

        protected TextWriter output;

        public Logger(string name = "", Logger parentLogger = null)
        {
            this.Name = name;
            this.output = Console.Out;
            this.Parent = parentLogger;
        }

        public string Name { get; internal set; }
        public Logger Parent { get; internal set; }

        public void WriteMessage(string message, bool includeNames = true, List<string> childNames = null)
        {
            lock (ThreadLock) {
                _WriteMessage_WhileLocked(message, false, ConsoleColor.White, ConsoleColor.Black, includeNames, true, childNames);
            }
        }

        public void WriteColorMessage(string message, 
            ConsoleColor fgColor = ConsoleColor.White,
            ConsoleColor bgColor = ConsoleColor.Black,
            bool includeNames = true, List<string> childNames = null)
        {
            lock (ThreadLock) {
                _WriteMessage_WhileLocked(message, true, fgColor, bgColor, includeNames, true, childNames);
            }
        }

        public void WriteHeader(string text, ConsoleColor fore, ConsoleColor bg)
        {
            lock (ThreadLock)
            {
                WriteFullLine_WhileLocked("", fore, bg);
                WriteFullLine_WhileLocked("", fore, bg);

                WriteFullLine_WhileLocked(CenterTextWithLeftPad(text), fore, bg);
            
                WriteFullLine_WhileLocked("", fore, bg);
                WriteFullLine_WhileLocked("", fore, bg);
            }
        }

        protected virtual string CenterTextWithLeftPad(string text)
        {
            int leftPad = ((Console.WindowWidth - 1) - text.Length) / 2;
            return text.PadLeft(text.Length + leftPad, ' ');
        }

        public void LineWithEmphasis(string firstPart, string emPart, ConsoleColor emColor)
        {
            lock (ThreadLock)
            {
                _WriteMessage_WhileLocked(firstPart, false, ConsoleColor.White, ConsoleColor.Black, true, false);
                _WriteMessage_WhileLocked(": ", false, ConsoleColor.White, ConsoleColor.Black, false, false);
                _WriteMessage_WhileLocked(emPart, true, emColor, ConsoleColor.Black, false, true);
            }
        }

        public virtual void ErrorLine(string message, Exception e = null)
        {
            lock (ThreadLock) 
            {
                output = Console.Error;
                _WriteMessage_WhileLocked("ERROR", true, ConsoleColor.White, ConsoleColor.Red, true, false);
                _WriteMessage_WhileLocked(": ", true, ConsoleColor.White, ConsoleColor.Black, false, false);
                _WriteMessage_WhileLocked(message, true, ConsoleColor.White, ConsoleColor.Black, false, true);
                if (e != null)
                {
                     _WriteMessage_WhileLocked(e.ToString(), true, ConsoleColor.White, ConsoleColor.DarkRed);
                }
                output = Console.Out;
            }
        }

        public void Debug(string p)
        {
            lock (ThreadLock) {
                System.Diagnostics.Debug.WriteLine(p);
            }
        }

        protected virtual void _WriteMessage_WhileLocked(string message, bool useColor = true,
            ConsoleColor fgColor = ConsoleColor.White,
            ConsoleColor bgColor = ConsoleColor.Black,
            bool includeNames = true, bool terminateLine = true, List<string> childNames = null)
        {
            // NOTE: Call from a locked context and don't lock in here

            childNames = childNames ?? new List<string>();
            if (!string.IsNullOrEmpty(Name))
            {
                childNames.Insert(0, Name);
            }

            if (Parent != null)
            {
                Parent._WriteMessage_WhileLocked(message, useColor, fgColor, bgColor, includeNames, terminateLine, childNames);
                return;
            }
            else
            {
                int leftPadding = 0;

                if (includeNames)
                {
                    string threadPrefix = string.Format("[thread {0}] => ", Thread.CurrentThread.ManagedThreadId);
                    leftPadding += threadPrefix.Length;

                    output.Write(threadPrefix);
                }

                if (includeNames && childNames.Any())
                {
                    childNames.ForEach(name =>
                    {
                        output.Write("[");

                        // Always use color for names, if they are included
                        WriteColorAndReset_WhileLocked(name, ConsoleColor.Yellow, ConsoleColor.Black);
                        output.Write("]");

                        leftPadding += (2 + name.Length);
                    });

                    output.Write(": ");
                    leftPadding += 2;
                }

                var lines = message.Split(Environment.NewLine.ToCharArray())
                    .Where(s => s != String.Empty)
                    .Select(l => l.Trim(Environment.NewLine.ToCharArray()));
                foreach (var line in lines)
                {
                    if (useColor)
                    {
                        WriteColorAndReset_WhileLocked(line, fgColor, bgColor);
                    }
                    else
                    {
                        output.Write(line);
                    }

                    if (line != lines.Last())
                    {
                        output.WriteLine();
                        output.Write(" ".PadLeft(leftPadding));
                    }
                }

                if (terminateLine)
                {
                    output.WriteLine();
                }
            }
        }

        protected virtual void WriteFullLine_WhileLocked(string text, ConsoleColor fore, ConsoleColor bg)
        { 
            // NOTE: do not lock here, call from a locked context !
            SetColors(fore, bg);
            output.Write(text.PadRight(Console.WindowWidth));
            ResetColors();
        }

        protected virtual void ResetColors()
        {
            Console.ResetColor();
        }

        protected virtual void SetColors(ConsoleColor fore, ConsoleColor bg)
        {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fore;
        }

        protected virtual void WriteColorAndReset_WhileLocked(string name, ConsoleColor fgColor, ConsoleColor bgColor)
        {
            // NOTE: do not lock here, call from a locked context !

            SetColors(fgColor, bgColor);
            output.Write(name);
            ResetColors();
        }

        public virtual void WarningLine(string msg)
        {
            WriteColorMessage(msg, ConsoleColor.White, ConsoleColor.DarkYellow);
        }
    }
}
