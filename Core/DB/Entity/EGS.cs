namespace Core.DB.Entity {

#pragma warning disable CS8618
    public class EGS : IEquatable<EGS?> {
        public long ID { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Thumbnail { get; set; }
        public string Page { get; set; }
        public string OriginalPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public EGS() { }

        public EGS(string title, string description, string thumbnail, string page, string originalPrice, DateTime startDate, DateTime endDate) {
            Title = title;
            Description = description;
            Thumbnail = thumbnail;
            Page = "https://store.epicgames.com/en-US/p/" + page;
            OriginalPrice = originalPrice;
            StartDate = startDate;
            EndDate = endDate;
        }

        public override bool Equals(object? obj) => Equals(obj as EGS);
        public bool Equals(EGS? egs) => egs is not null && egs.Title == Title && egs.Description == Description;

        public static bool operator ==(EGS? left, EGS? right) => left?.Equals(right) ?? false;
        public static bool operator !=(EGS? left, EGS? right) => !(left == right);

        public override int GetHashCode() {
            int hash = 17;

            hash += Title.GetHashCode();
            hash += Description.GetHashCode();

            return hash.GetHashCode();
        }
    }
}
