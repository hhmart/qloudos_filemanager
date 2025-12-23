namespace QloudosFileManager.Models
{
    /// <summary>
    /// Repräsentiert Rechte-Zuordnung zwischen Benutzer und Objekt.
    /// </summary>
    public class PermissionEntry
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        /// <summary>Objekttyp: "file" oder "folder"</summary>
        public string ObjectType { get; set; } = string.Empty;
        /// <summary>Objekt-Id (FileId oder FolderId)</summary>
        public long ObjectId { get; set; }
        /// <summary>Rechte als Text, z.B. "rwx" oder "r"</summary>
        public string Rights { get; set; } = string.Empty;
    }
}