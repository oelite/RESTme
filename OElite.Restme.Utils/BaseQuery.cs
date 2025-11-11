namespace OElite.Common
{
    public class BaseQuery
    {
        /// <summary>
        /// PageSize
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > 1000 ? 1000 : (value <= 0 ? 100 : value);
        }

        private int _pageSize = 100;

        /// <summary>
        /// PageIndex
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// Name Keywords
        /// </summary>
        public string? NameKeywords { get; set; }

        public bool? ExpectTotalCount { get; set; }

        /// <summary>
        /// Sort
        /// </summary>
        public string? Sort { get; set; }

        public EntityStatus[]? Statuses { get; set; }
    }
}