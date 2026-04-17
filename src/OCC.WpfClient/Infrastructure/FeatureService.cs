using System.Collections.Generic;
using System.Linq;
using OCC.WpfClient.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Infrastructure
{
    public class FeatureService : IFeatureService
    {
        private readonly IEnumerable<IFeature> _features;

        public FeatureService(IEnumerable<IFeature> features)
        {
            _features = features.OrderBy(f => f.Order);
        }

        public IEnumerable<IFeature> GetAllFeatures() => _features;

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var allItems = _features.SelectMany(f => f.GetNavigationItems());
            var merged = new List<NavItem>();
            
            foreach (var item in allItems)
            {
                var existing = merged.FirstOrDefault(m => m.Label == item.Label);
                if (existing != null)
                {
                    foreach (var child in item.Children)
                        existing.Children.Add(child);
                }
                else
                {
                    merged.Add(item);
                }
            }
            return merged;
        }
    }
}
