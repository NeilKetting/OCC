using System.Collections.Generic;

namespace OCC.WpfClient.Infrastructure
{
    public interface IFeatureService
    {
        IEnumerable<IFeature> GetAllFeatures();
        IEnumerable<NavItem> GetNavigationItems();
    }
}
