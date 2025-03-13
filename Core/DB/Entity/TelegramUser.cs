using System.ComponentModel.DataAnnotations;

namespace Core.DB.Entity {
    public class TelegramUser : IEquatable<TelegramUser?> {
        [Key]
        public long ChatID { get; set; }

        public bool IsDeactivated { get; set; } = false;

        public override bool Equals(object? obj) => Equals(obj as TelegramUser);
        public bool Equals(TelegramUser? user) => user is not null && ChatID == user.ChatID;
        public static bool operator ==(TelegramUser? left, TelegramUser? right) => left?.Equals(right) ?? false;
        public static bool operator !=(TelegramUser? left, TelegramUser? right) => !(left == right);
        public override int GetHashCode() => ChatID.GetHashCode();

        public TelegramUser() { }

        public TelegramUser(TelegramUser telegramUser) => ChatID = telegramUser.ChatID;
    }
}
