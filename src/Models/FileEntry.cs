using System;

namespace QloudosFileManager.Models
{
    /// <summary>
    /// Repräsentiert eine Datei im R3-Dateisystem.
    /// </summary>
    public class FileEntry
    {
        /// <summary>Primärschlüssel der Datei</summary>
        public long Id { get; set; }
        /// <summary>Dateiname inklusive Erweiterung</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Relativer Pfad innerhalb des R3-Systems (englisch standardisiert)</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Binärinhalt der Datei</summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();
        /// <summary>Erstellungsdatum UTC</summary>
        public DateTime CreatedUtc { get; set; }
        /// <summary>Owner UserId</summary>
        public long OwnerId { get; set; }
    }
}