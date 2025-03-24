namespace Bookify.Domain.Shared
{
    public record Currency
    {
        internal static readonly Currency None = new(""); // We dont want to expose it outside our domain project. The internal keyword hides the property from the Domain assembly
        public static readonly Currency Usd = new("USD");
        public static readonly Currency Eur = new("EUR");

        private Currency(string code) => Code = code;
        public string Code { get; init; }

        public static Currency FromCode(string code)
        {
            return All.FirstOrDefault(c => c.Code == code) ??
                throw new ApplicationException("The currency code is invalid");
        }

        public static readonly IReadOnlyCollection<Currency> All = new[]
        {
            Usd,
            Eur
        };
    }
}
