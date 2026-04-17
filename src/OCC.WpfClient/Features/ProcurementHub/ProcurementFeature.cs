using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.ProcurementHub
{
    public class ProcurementFeature : IFeature
    {
        public string Name => "Procurement";
        public string Description => "Supply Chain, Inventory and Supplier Management";
        public string Icon => "IconSummary";
        public int Order => 40;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<ProcurementViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<PurchaseOrderViewModel>();
            services.AddTransient<SupplierViewModel>();

            services.AddTransient<ISupplierService, SupplierService>();
            services.AddTransient<IOrderService, OrderService>();
            services.AddTransient<IProjectService, ProjectService>();
            services.AddSingleton<IPdfService, PdfService>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.Procurement, typeof(ProcurementViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Inventory, typeof(InventoryViewModel));
            navigationService.RegisterRoute(NavigationRoutes.PurchaseOrder, typeof(PurchaseOrderViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Suppliers, typeof(SupplierViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var procurement = new NavItem("Procurement", "IconSummary", string.Empty, "Operations");

            procurement.Children.Add(new NavItem(
                "Procurement Dashboard",
                "IconSummary",
                NavigationRoutes.Procurement,
                "Operations"));

            procurement.Children.Add(new NavItem(
                "Suppliers",
                "IconTeam",
                NavigationRoutes.Suppliers,
                "Operations"));

            procurement.Children.Add(new NavItem(
                "Inventory Management",
                "IconList",
                NavigationRoutes.Inventory,
                "Operations"));

            procurement.Children.Add(new NavItem(
                "Purchase Order",
                "IconFile",
                NavigationRoutes.PurchaseOrder,
                "Operations"));

            yield return procurement;
        }
    }
}
