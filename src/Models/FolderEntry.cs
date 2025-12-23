using System;

namespace QloudosFileManager.Models
{
    /// <summary>
    /// Repräsentiert einen Ordner im R3-Dateisystem.
    /// </summary>
    public class FolderEntry
    {
        public long Id { get; set; }
        /// <summary>Relativer Pfad des Ordners</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Owner UserId</summary>
        public long OwnerId { get; set; }
        /// <summary>Erstellungsdatum UTC</summary>
        public DateTime CreatedUtc { get; set; }
    }
}