using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.Models; // For RestockCandidate if needed, but we should use DTO or map

namespace OCC.Client.Services.Managers.Interfaces
{
    /// <summary>
    /// Centralized manager for Order-related business logic and data access.
    /// Aggregates services and repositories to simplify ViewModels, providing a single entry point 
    /// for all order management operations including lifecycle management, dependency retrieval, 
    /// and quick-creation workflows.
    /// </summary>
    public interface IOrderManager
    {
        // --- Order Lifecycle ---

        /// <summary>
        /// Retrieves all orders from the data store asynchronously.
        /// </summary>
        /// <returns>A collection of <see cref="OrderSummaryDto"/> objects.</returns>
        Task<IEnumerable<OrderSummaryDto>> GetOrdersAsync();

        /// <summary>
        /// Retrieves a specific order by its unique identifier.
        /// </summary>
        /// <param name="id">The GUID of the order to retrieve.</param>
        /// <returns>The <see cref="Order"/> if found; otherwise, null.</returns>
        Task<Order?> GetOrderByIdAsync(Guid id);

        /// <summary>
        /// Creates a new order in the system.
        /// </summary>
        /// <param name="order">The order object to create.</param>
        /// <returns>The created <see cref="Order"/> with assigned ID and metadata.</returns>
        Task<Order> CreateOrderAsync(Order order);

        /// <summary>
        /// Updates an existing order's details.
        /// </summary>
        /// <param name="order">The order object with updated information.</param>
        Task UpdateOrderAsync(Order order);

        /// <summary>
        /// Deletes an order from the system permanently.
        /// </summary>
        /// <param name="orderId">The GUID of the order to delete.</param>
        Task DeleteOrderAsync(Guid orderId);

        // --- Dependencies Data Access ---

        /// <summary>
        /// Retrieves all registered suppliers as lightweight summaries.
        /// </summary>
        /// <returns>A collection of <see cref="SupplierSummaryDto"/> objects.</returns>
        Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync();

        /// <summary>
        /// Retrieves all registered suppliers.
        /// </summary>
        /// <returns>A collection of <see cref="Supplier"/> objects.</returns>
        Task<IEnumerable<Supplier>> GetSuppliersAsync();
        Task<Supplier?> GetSupplierByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all registered customers.
        /// </summary>
        /// <returns>A collection of <see cref="Customer"/> objects.</returns>
        Task<IEnumerable<Customer>> GetCustomersAsync();

        /// <summary>
        /// Retrieves all active projects.
        /// </summary>
        /// <returns>A collection of <see cref="Project"/> objects.</returns>
        Task<IEnumerable<Project>> GetProjectsAsync();

        /// <summary>
        /// Retrieves a specific project by its unique identifier.
        /// </summary>
        /// <param name="id">The GUID of the project.</param>
        /// <returns>The <see cref="Project"/> if found; otherwise, null.</returns>
        Task<Project?> GetProjectByIdAsync(Guid id);

        /// <summary>
        /// Updates details for a specific project (e.g., updating delivery location).
        /// </summary>
        /// <param name="project">The project with updated details.</param>
        Task UpdateProjectAsync(Project project);

        /// <summary>
        /// Retrieves all inventory items available for ordering as lightweight summaries.
        /// </summary>
        /// <returns>A collection of <see cref="InventorySummaryDto"/> objects.</returns>
        Task<IEnumerable<InventorySummaryDto>> GetInventorySummariesAsync();

        /// <summary>
        /// Retrieves all inventory items available for ordering.
        /// </summary>
        /// <returns>A collection of <see cref="InventoryItem"/> objects.</returns>
        Task<IEnumerable<InventoryItem>> GetInventoryAsync();

        // --- Quick Creation ---

        /// <summary>
        /// Quickly creates a new product/inventory item during the order creation flow.
        /// </summary>
        /// <param name="name">Name of the product.</param>
        /// <param name="uom">Unit of measure (e.g., "ea", "kg").</param>
        /// <param name="category">Product category.</param>
        /// <param name="supplierName">Name of the supplier associated with this item.</param>
        /// <returns>The newly created <see cref="InventoryItem"/>.</returns>
        Task<InventoryItem> QuickCreateProductAsync(string name, string uom, string category, string supplierName);

        /// <summary>
        /// Quickly creates a new supplier during the order creation flow.
        /// </summary>
        /// <param name="name">Name of the new supplier.</param>
        /// <returns>The newly created <see cref="Supplier"/>.</returns>
        Task<Supplier> QuickCreateSupplierAsync(string name);

        /// <summary>
        /// Updates an inventory item, typically used for updating average costs or details during ordering.
        /// </summary>
        /// <param name="item">The inventory item to update.</param>
        Task UpdateInventoryItemAsync(InventoryItem item);

        // --- Refactoring Support ---

        /// <summary>
        /// Retrieves all necessary data for creating a new order (Suppliers, Customers, Projects, Inventory) in a single parallel operation.
        /// </summary>
        Task<OrderEntryData> GetOrderEntryDataAsync();

        /// <summary>
        /// Creates a new order template with default values and a generated order number.
        /// </summary>
        Order CreateNewOrderTemplate(OrderType type = OrderType.PurchaseOrder);

        // --- Supplier Management ---

        /// <summary>
        /// Deletes a supplier permanently if they have no associated orders.
        /// </summary>
        /// <param name="supplierId">The GUID of the supplier to delete.</param>
        Task DeleteSupplierAsync(Guid supplierId);

        /// <summary>
        /// Updates an existing supplier's information.
        /// </summary>
        /// <param name="supplier">The supplier object with updated details.</param>
        Task UpdateSupplierAsync(Supplier supplier);

        /// <summary>
        /// Creates a new supplier in the system.
        /// </summary>
        /// <param name="supplier">The supplier object to create.</param>
        /// <returns>The created <see cref="Supplier"/>.</returns>
        Task<Supplier> CreateSupplierAsync(Supplier supplier);

        // --- Inventory Management ---

        /// <summary>
        /// Creates a new inventory item/product.
        /// </summary>
        /// <param name="item">The inventory item to create.</param>
        /// <returns>The created <see cref="InventoryItem"/>.</returns>
        Task<InventoryItem> CreateItemAsync(InventoryItem item);

        /// <summary>
        /// Deletes an inventory item permanently if possible.
        /// </summary>
        /// <param name="itemId">The GUID of the inventory item to delete.</param>
        Task DeleteItemAsync(Guid itemId);
        Task<InventoryItem?> GetItemByIdAsync(Guid id);

        /// <summary>
        /// Updates an existing inventory item's details.
        /// </summary>
        /// <param name="item">The inventory item to update.</param>
        Task UpdateItemAsync(InventoryItem item);

        // --- Receiving ---

        /// <summary>
        /// Processes a receipt of goods against an existing order.
        /// </summary>
        /// <param name="order">The order being received.</param>
        /// <param name="receipts">A list of order lines containing the quantities being received.</param>
        Task ReceiveOrderAsync(Order order, List<OrderLine> receipts);

        // --- Dashboard & Analytics ---

        /// <summary>
        /// Calculates and retrieves statistical data for the order dashboard, filtered by branch if provided.
        /// </summary>
        /// <param name="branch">Optional branch to filter by.</param>
        /// <returns>A <see cref="OrderDashboardStats"/> object containing processed metrics.</returns>
        Task<OrderDashboardStats> GetDashboardStatsAsync(Branch? branch = null);

        /// <summary>
        /// Creates a new order template populated with all inventory items that are currently low on stock.
        /// </summary>
        /// <returns>A new <see cref="Order"/> object containing lines for low stock items.</returns>
        Task<Order> GetRestockOrderTemplateAsync();

        /// <summary>
        /// Retrieves list of low stock items grouped by supplier with on-order calculations, optionally filtered by branch.
        /// </summary>
        /// <param name="branch">Optional branch to filter by.</param>
        Task<IEnumerable<RestockCandidateDto>> GetRestockCandidatesAsync(Branch? branch = null);
    }

    /// <summary>
    /// Data structure for order entry dependencies.
    /// </summary>
    public record OrderEntryData(
        IEnumerable<Supplier> Suppliers,
        IEnumerable<Customer> Customers,
        IEnumerable<Project> Projects,
        IEnumerable<InventoryItem> Inventory);

    /// <summary>
    /// Data structure for order dashboard metrics and alerts.
    /// </summary>
    public record OrderDashboardStats(
        int OrdersThisMonth,
        string MonthGrowthText,
        string MonthGrowthColor,
        int PendingDeliveriesCount,
        string PendingDeliveriesText,
        string PendingDeliveriesColor,
        IEnumerable<OrderSummaryDto> RecentOrders,
        IEnumerable<RestockCandidateDto> LowStockItems,
        int LowStockCount);
}
