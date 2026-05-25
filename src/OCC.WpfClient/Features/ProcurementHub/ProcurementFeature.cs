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
        public string Icon => "ProcurementIcon";
        public int Order => 40;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<ProcurementViewModel>();
            services.AddTransient<InventoryListViewModel>();
            services.AddTransient<PurchaseOrderDetailViewModel>();
            services.AddTransient<SupplierListViewModel>();
            services.AddTransient<ViewModels.Dialogs.ReceiveStockViewModel>();
            services.AddTransient<ViewModels.Dialogs.FindOrderViewModel>();

            services.AddTransient<ISupplierService, SupplierService>();
            services.AddTransient<IOrderService, OrderService>();
            services.AddTransient<IProjectService, ProjectService>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.Procurement, typeof(ProcurementViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Inventory, typeof(InventoryListViewModel));
            navigationService.RegisterRoute(NavigationRoutes.PurchaseOrder, typeof(PurchaseOrderDetailViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Suppliers, typeof(SupplierListViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var procurement = new NavItem("Procurement", string.Empty, "Operations", iconColor: "#008000", iconCode: "\uE7BF");

            procurement.Children.Add(new NavItem(
                "Procurement Dashboard",
                NavigationRoutes.Procurement,
                "Operations",
                iconColor: "#004B50",
                iconCode: "\uE7BF"));

            procurement.Children.Add(new NavItem(
                "Suppliers",
                NavigationRoutes.Suppliers,
                "Operations",
                iconColor: "LightGreen",
                iconCode: "\uE716"));

            procurement.Children.Add(new NavItem(
                "Inventory Management",
                NavigationRoutes.Inventory,
                "Operations",
                iconColor: "#001064",
                iconCode: "\uE950"));

            procurement.Children.Add(new NavItem(
                "Purchase Order",
                NavigationRoutes.PurchaseOrder,
                "Operations",
                iconColor: "#0078D4",
                iconCode: "\uE8A1"));

            yield return procurement;
        }
    }
}
