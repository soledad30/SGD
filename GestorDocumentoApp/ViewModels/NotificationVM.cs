namespace GestorDocumentoApp.ViewModels
{
    public class NotificationVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Link { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationBellVM
    {
        public int UnreadCount { get; set; }
        public IEnumerable<NotificationVM> Latest { get; set; } = [];
    }
}
