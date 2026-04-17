using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Shared.Interfaces
{
    public interface IGoogleMapsService
    {
        Task<IEnumerable<AddressSuggestion>> GetAddressSuggestionsAsync(string input, string sessionToken);
        Task<ProjectAddressInfo?> GetPlaceDetailsAsync(string placeId, string sessionToken);
    }

    public class AddressSuggestion
    {
        public string PlaceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MainText { get; set; } = string.Empty;
    }

    public class ProjectAddressInfo
    {
        public string StreetLine1 { get; set; } = string.Empty;
        public string StreetLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string StateOrProvince { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
