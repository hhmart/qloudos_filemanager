namespace QloudosFileManager.Models
{
    /// <summary>
    /// Benutzerrepräsentation für R3.
    /// </summary>
    public class UserEntry
    {
        public long Id { get; set; }
        /// <summary>Benutzername kurz (z.B. "jdoe")</summary>
        public string Username { get; set; } = string.Empty;
        /// <summary>Anzeigename oder vollständiger Name</summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}