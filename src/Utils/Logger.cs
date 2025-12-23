using System;

namespace QloudosFileManager.Utils
{
    /// <summary>
    /// Einfache Logger-Klasse mit drei Ausgabestufen.
    /// </summary>
    public class Logger
    {
        public enum Stufe { None, Short, Verbose }
        public Stufe Level { get; set; } = Stufe.Short;

        public Logger(Stufe level)
        {
            Level = level;
        }

        /// <summary>Gibt eine kurze Nachricht aus (wenn Level >= Short).</summary>
        public void Short(string nachricht)
        {
            if (Level >= Stufe.Short) Console.WriteLine(nachricht);
        }

        /// <summary>Gibt eine ausführliche Nachricht aus (wenn Level == Verbose).</summary>
        public void Verbose(string nachricht)
        {
            if (Level >= Stufe.Verbose) Console.WriteLine(nachricht);
        }

        /// <summary>Gibt eine Fehlernachricht immer aus.</summary>
        public void Error(string nachricht)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(nachricht);
            Console.ForegroundColor = prev;
        }
    }
}