using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class OrderService : IOrderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<OrderService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public OrderService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<OrderService> logger, ConnectionSettings connectionSettings)
        {
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _logger = logger;
            _connectionSettings = connectionSettings;
        }

        private void EnsureAuthorization(HttpClient client)
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<Order>> GetOrdersAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Orders");
            try
            {
                var summaries = await client.GetFromJsonAsync<IEnumerable<OrderSummaryDto>>(url);
                return summaries?.Select(s => new Order 
                { 
                    Id = s.Id, 
                    OrderNumber = s.OrderNumber, 
                    OrderDate = s.OrderDate, 
                    ExpectedDeliveryDate = s.ExpectedDeliveryDate,
                    SupplierName = s.SupplierName,
                    ProjectName = s.ProjectName,
                    Status = s.Status,
                    SupplierId = s.SupplierId,
                    OrderType = s.OrderType,
                    Branch = (Branch)Enum.Parse(typeof(Branch), s.Branch)
                }) ?? new List<Order>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders from {Url}", url);
                throw;
            }
        }

        public async Task<Order?> GetOrderAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Orders/{id}");
            try
            {
                var dto = await client.GetFromJsonAsync<OrderDto>(url);
                return dto != null ? ToEntity(dto) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Orders");
            try
            {
                var dto = ToDto(order);
                var response = await client.PostAsJsonAsync(url, dto);
                response.EnsureSuccessStatusCode();
                var resultDto = await response.Content.ReadFromJsonAsync<OrderDto>();
                return resultDto != null ? ToEntity(resultDto) : order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order at {Url}", url);
                throw;
            }
        }

        public async Task UpdateOrderAsync(Order order)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Orders/{order.Id}");
            try
            {
                var dto = ToDto(order);
                var response = await client.PutAsJsonAsync(url, dto);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order {Id} at {Url}", order.Id, url);
                throw;
            }
        }

        public async Task DeleteOrderAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Orders/{id}");
            try
            {
                var response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {Id} at {Url}", id, url);
                throw;
            }
        }

        public async Task<Order> CreateNewOrderTemplateAsync(OrderType type)
        {
            // For now, generate locally to avoid extra API roundtrip if not strictly needed
            // In a real app, this might call an API to reserve a number
            string prefix = type switch
            {
                OrderType.PurchaseOrder => "PO",
                OrderType.PickingOrder => "PK",
                OrderType.ReturnToInventory => "RET",
                _ => "ORD"
            };

            return new Order
            {
                Id = Guid.NewGuid(),
                OrderDate = DateTime.Now,
                OrderNumber = $"{prefix}-{DateTime.Now:yyMM}-{new Random().Next(1000, 9999)}",
                OrderType = type,
                TaxRate = 0.15m,
                DestinationType = OrderDestinationType.Stock,
                Status = OrderStatus.Draft
            };
        }

        public async Task<Order?> ReceiveOrderAsync(Guid orderId, List<OrderLine> updatedLines)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Orders/{orderId}/receive");
            try
            {
                var dtos = updatedLines.Select(l => new OrderLineDto
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
                    LineTotal = l.LineTotal,
                    Remarks = l.Remarks
                }).ToList();

                var response = await client.PostAsJsonAsync(url, dtos);
                response.EnsureSuccessStatusCode();
                var resultDto = await response.Content.ReadFromJsonAsync<OrderDto>();
                return resultDto != null ? ToEntity(resultDto) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving order {Id} at {Url}", orderId, url);
                throw;
            }
        }

        private static Order ToEntity(OrderDto dto)
        {
            var order = new Order
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
                Template = dto.Template,
                Terms = dto.Terms,
                ReferenceNo = dto.ReferenceNo
            };

            foreach (var l in dto.Lines)
            {
                order.Lines.Add(new OrderLine
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
                    LineTotal = l.LineTotal,
                    Remarks = l.Remarks
                });
            }

            return order;
        }

        private static OrderDto ToDto(Order order)
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
                Template = order.Template,
                Terms = order.Terms,
                ReferenceNo = order.ReferenceNo,
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
                    LineTotal = l.LineTotal,
                    Remarks = l.Remarks
                }).ToList()
            };
        }
    }
}
