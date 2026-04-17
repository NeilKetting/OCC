using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.Models;

namespace OCC.Client.Services.Managers
{
    public class OrderManager : IOrderManager
    {
        #region Private Members

        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly ISupplierService _supplierService;
        private readonly IRepository<Customer> _customerRepository;
        private readonly IRepository<Project> _projectRepository;

        #endregion

        #region Constructors

        public OrderManager(
            IOrderService orderService,
            IInventoryService inventoryService,
            ISupplierService supplierService,
            IRepository<Customer> customerRepository,
            IRepository<Project> projectRepository)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _supplierService = supplierService;
            _customerRepository = customerRepository;
            _projectRepository = projectRepository;
        }

        #endregion

        #region Methods

        #region Order Operations

        /// <inheritdoc/>
        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersAsync() => await _orderService.GetOrdersAsync();

        /// <inheritdoc/>
        public async Task<Order?> GetOrderByIdAsync(Guid id)
        {
            var dto = await _orderService.GetOrderAsync(id);
            return dto == null ? null : ToEntity(dto);
        }

        /// <inheritdoc/>
        public async Task<Order> CreateOrderAsync(Order order)
        {
            var dto = ToDto(order);
            var resultDto = await _orderService.CreateOrderAsync(dto);
            return ToEntity(resultDto);
        }

        /// <inheritdoc/>
        public async Task UpdateOrderAsync(Order order)
        {
            var dto = ToDto(order);
            await _orderService.UpdateOrderAsync(dto);
        }

        /// <inheritdoc/>
        public async Task DeleteOrderAsync(Guid orderId) => await _orderService.DeleteOrderAsync(orderId);

        /// <inheritdoc/>
        public async Task<OrderEntryData> GetOrderEntryDataAsync()
        {
            var tSuppliers = GetSuppliersAsync();
            var tCustomers = GetCustomersAsync();
            var tProjects = GetProjectsAsync();
            var tInventory = GetInventoryAsync();

            await Task.WhenAll(tSuppliers, tCustomers, tProjects, tInventory);

            return new OrderEntryData(
                await tSuppliers, 
                await tCustomers, 
                await tProjects, 
                await tInventory);
        }

        /// <inheritdoc/>
        public async Task ReceiveOrderAsync(Order order, List<OrderLine> receipts)
        {
            var dtos = receipts.Select(l => new OrderLineDto
            {
                Id = l.Id,
                InventoryItemId = l.InventoryItemId,
                ItemCode = l.ItemCode,
                Description = l.Description,
                Category = l.Category,
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.UnitPrice,
                VatAmount = l.VatAmount,
                LineTotal = l.LineTotal
            }).ToList();

            var resultDto = await _orderService.ReceiveOrderAsync(order.Id, dtos);
            
            if (resultDto != null)
            {
                var updatedEntity = ToEntity(resultDto);
                // Update local object to reflect status changes
                order.Status = updatedEntity.Status;
                
                // Update lines quantities
                foreach(var line in updatedEntity.Lines)
                {
                    var local = order.Lines.FirstOrDefault(l => l.Id == line.Id);
                    if (local != null) 
                    {
                        local.QuantityReceived = line.QuantityReceived;
                    }
                }
            }
        }

        #endregion

        #region Supplier Operations

        public async Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync() => await _supplierService.GetSupplierSummariesAsync();
        public async Task<IEnumerable<Supplier>> GetSuppliersAsync() => await _supplierService.GetSuppliersAsync();
        public async Task<Supplier?> GetSupplierByIdAsync(Guid id) => await _supplierService.GetSupplierAsync(id);
        public async Task DeleteSupplierAsync(Guid supplierId) => await _supplierService.DeleteSupplierAsync(supplierId);
        public async Task UpdateSupplierAsync(Supplier supplier) => await _supplierService.UpdateSupplierAsync(supplier);
        public async Task<Supplier> CreateSupplierAsync(Supplier supplier) => await _supplierService.CreateSupplierAsync(supplier);
        public async Task<Supplier> QuickCreateSupplierAsync(string name)
        {
            var supplier = new Supplier { Name = name };
            return await _supplierService.CreateSupplierAsync(supplier);
        }

        #endregion

        #region Inventory Operations

        public async Task<IEnumerable<InventorySummaryDto>> GetInventorySummariesAsync() => await _inventoryService.GetInventorySummariesAsync();
        public async Task<IEnumerable<InventoryItem>> GetInventoryAsync() => await _inventoryService.GetInventoryAsync();
        public async Task<InventoryItem> CreateItemAsync(InventoryItem item) => await _inventoryService.CreateItemAsync(item);
        public async Task DeleteItemAsync(Guid itemId) => await _inventoryService.DeleteItemAsync(itemId);
        public async Task<InventoryItem?> GetItemByIdAsync(Guid id) => await _inventoryService.GetInventoryItemAsync(id);
        public async Task UpdateItemAsync(InventoryItem item) => await _inventoryService.UpdateItemAsync(item);
        public async Task UpdateInventoryItemAsync(InventoryItem item) => await _inventoryService.UpdateItemAsync(item);
        public async Task<InventoryItem> QuickCreateProductAsync(string name, string uom, string category, string supplierName)
        {
            var item = new InventoryItem
            {
                Description = name,
                UnitOfMeasure = uom,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
                Supplier = supplierName
            };
            return await _inventoryService.CreateItemAsync(item);
        }

        #endregion

        #region Project & Customer Operations

        public async Task<IEnumerable<Customer>> GetCustomersAsync() => await _customerRepository.GetAllAsync();
        public async Task<IEnumerable<Project>> GetProjectsAsync() => await _projectRepository.GetAllAsync();
        public async Task<Project?> GetProjectByIdAsync(Guid id) => await _projectRepository.GetByIdAsync(id);
        public async Task UpdateProjectAsync(Project project) => await _projectRepository.UpdateAsync(project);

        #endregion

        #region Dashboard & Analytics

        /// <inheritdoc/>
        public async Task<OrderDashboardStats> GetDashboardStatsAsync(Branch? branch = null)
        {
            var allOrders = (await _orderService.GetOrdersAsync()).ToList(); // Now Summary DTOs

            // Filter orders by branch if requested
            if (branch.HasValue)
            {
                allOrders = allOrders.Where(o => o.Branch == branch.Value.ToString()).ToList(); 
                // Wait, in Controller I did: Branch = o.Branch.ToString()
                // Let's verify DTO definition. OrderDashboardStats expects OrderSummaryDto?
            }

            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            var thisMonthOrders = allOrders.Where(o => o.OrderDate >= startOfMonth).ToList();
            var lastMonthOrders = allOrders.Where(o => o.OrderDate >= startOfLastMonth && o.OrderDate <= endOfLastMonth).ToList();

            int ordersThisMonthCount = thisMonthOrders.Count;
            double growth = 0;
            if (lastMonthOrders.Count > 0)
            {
                growth = ((double)(ordersThisMonthCount - lastMonthOrders.Count) / lastMonthOrders.Count) * 100;
            }
            else if (ordersThisMonthCount > 0)
            {
                growth = 100;
            }

            string growthSign = growth >= 0 ? "+" : "";
            string growthText = $"{growthSign}{growth:0}% from last month";
            string growthColor = growth >= 0 ? "#10B981" : "#EF4444";

            var pendingOrders = allOrders.Where(o => o.Status == OrderStatus.Ordered || o.Status == OrderStatus.PartialDelivery).ToList();
            int pendingCount = pendingOrders.Count;
            string pendingText;
            string pendingColor;

            var today = DateTime.Today;
            // Uses ExpectedDeliveryDate from DTO
            var arrivingTodayCount = pendingOrders.Count(o => o.ExpectedDeliveryDate?.Date == today);

            if (arrivingTodayCount > 0)
            {
                pendingText = $"{arrivingTodayCount} Arriving Today";
                pendingColor = "#F59E0B";
            }
            else
            {
                var nextOrder = pendingOrders.Where(o => o.ExpectedDeliveryDate?.Date > today)
                                             .OrderBy(o => o.ExpectedDeliveryDate)
                                             .FirstOrDefault();

                if (nextOrder != null && nextOrder.ExpectedDeliveryDate.HasValue)
                {
                    pendingText = $"Next order will arrive {nextOrder.ExpectedDeliveryDate.Value:dd MMM}";
                    pendingColor = "#64748B";
                }
                else
                {
                    pendingText = "No upcoming deliveries";
                    pendingColor = "#94A3B8";
                }
            }

            var recentOrders = allOrders.OrderByDescending(o => o.OrderDate).Take(5).ToList();
            
            // Get Restock Candidates from API
            var candidates = (await _orderService.GetRestockCandidatesAsync(branch)).ToList();
            var lowStockItems = candidates.Take(5).ToList(); 

            return new OrderDashboardStats(
                ordersThisMonthCount,
                growthText,
                growthColor,
                pendingCount,
                pendingText,
                pendingColor,
                recentOrders,
                lowStockItems,
                candidates.Count);
        }

        /// <inheritdoc/>
        public async Task<Order> GetRestockOrderTemplateAsync()
        {
            var dto = await _orderService.GetRestockTemplateAsync();
            return ToEntity(dto);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<RestockCandidateDto>> GetRestockCandidatesAsync(Branch? branch = null)
        {
            return await _orderService.GetRestockCandidatesAsync(branch);
        }

        #endregion

        #endregion

        #region Helper Methods

        /// <inheritdoc/>
        public Order CreateNewOrderTemplate(OrderType type = OrderType.PurchaseOrder)
        {
            // Keeping this local helper for 'Create Order' button which might want a blank slate
            string prefix = type switch
            {
                OrderType.PurchaseOrder => "PO",
                OrderType.PickingOrder => "PK",
                OrderType.ReturnToInventory => "RET",
                _ => "ORD"
            };

            return new Order
            {
                Id = Guid.Empty,
                OrderDate = DateTime.Now,
                OrderNumber = $"{prefix}-{DateTime.Now:yyMM}-{new Random().Next(1000, 9999)}",
                OrderType = type,
                TaxRate = 0.15m,
                DestinationType = OrderDestinationType.Stock,
                Attention = string.Empty
            };
        }

        private OrderDto ToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                OrderType = order.OrderType,
                Branch = order.Branch,
                SupplierId = order.SupplierId,
                SupplierName = order.SupplierName,
                CustomerId = order.CustomerId,
                EntityAddress = order.EntityAddress,
                EntityTel = order.EntityTel,
                EntityVatNo = order.EntityVatNo,
                DestinationType = order.DestinationType,
                ProjectId = order.ProjectId,
                ProjectName = order.ProjectName,
                Attention = order.Attention,
                TaxRate = order.TaxRate,
                Status = order.Status,
                Notes = order.Notes,
                DeliveryInstructions = order.DeliveryInstructions,
                ScopeOfWork = order.ScopeOfWork,
                TotalAmount = order.TotalAmount,
                Lines = order.Lines.Select(l => new OrderLineDto
                {
                    Id = l.Id,
                    InventoryItemId = l.InventoryItemId,
                    ItemCode = l.ItemCode,
                    Description = l.Description,
                    Category = l.Category,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityReceived = l.QuantityReceived,
                    UnitOfMeasure = l.UnitOfMeasure,
                    UnitPrice = l.UnitPrice,
                    VatAmount = l.VatAmount,
                    LineTotal = l.LineTotal
                }).ToList()
            };
        }

        private Order ToEntity(OrderDto dto)
        {
            return new Order
            {
                Id = dto.Id,
                OrderNumber = dto.OrderNumber,
                OrderDate = dto.OrderDate,
                ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
                OrderType = dto.OrderType,
                Branch = dto.Branch,
                SupplierId = dto.SupplierId,
                SupplierName = dto.SupplierName,
                CustomerId = dto.CustomerId,
                EntityAddress = dto.EntityAddress,
                EntityTel = dto.EntityTel,
                EntityVatNo = dto.EntityVatNo,
                DestinationType = dto.DestinationType,
                ProjectId = dto.ProjectId,
                ProjectName = dto.ProjectName,
                Attention = dto.Attention,
                TaxRate = dto.TaxRate,
                Status = dto.Status,
                Notes = dto.Notes,
                DeliveryInstructions = dto.DeliveryInstructions,
                ScopeOfWork = dto.ScopeOfWork,
                // Lines need to be ObservableCollection
                Lines = new ObservableCollection<OrderLine>(
                    dto.Lines.Select(l => new OrderLine
                    {
                        Id = l.Id,
                        OrderId = dto.Id,
                        InventoryItemId = l.InventoryItemId,
                        ItemCode = l.ItemCode,
                        Description = l.Description,
                        Category = l.Category,
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        UnitOfMeasure = l.UnitOfMeasure,
                        UnitPrice = l.UnitPrice,
                        VatAmount = l.VatAmount,
                        LineTotal = l.LineTotal
                    })
                )
            };
        }

        #endregion
    }
}
