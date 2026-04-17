using System;
using System.Collections.Generic;
using OCC.Shared.Models; // For Enums and possibly InventoryItem if shared

namespace OCC.Shared.DTOs
{
    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string Branch { get; set; } = string.Empty;
        public Guid? SupplierId { get; set; }
        public OrderType OrderType { get; set; }
        public string DestinationDisplay { get; set; } = string.Empty;
    }

    public class InventorySummaryDto
    {
        public Guid Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double JhbQuantity { get; set; }
        public double CptQuantity { get; set; }
        public double QuantityOnHand { get; set; }
        public decimal Price { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string UnitOfMeasure { get; set; } = string.Empty;
        public InventoryStatus InventoryStatus { get; set; }
    }

    public class SupplierSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public string VatNumber { get; set; } = string.Empty;
    }

    public class OrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public OrderType OrderType { get; set; }
        public Branch Branch { get; set; }
        
        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        
        public string EntityAddress { get; set; } = string.Empty;
        public string EntityTel { get; set; } = string.Empty;
        public string EntityVatNo { get; set; } = string.Empty;
        
        public OrderDestinationType DestinationType { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string Attention { get; set; } = string.Empty;
        
        public decimal TaxRate { get; set; }
        public OrderStatus Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string DeliveryInstructions { get; set; } = string.Empty;
        public string ScopeOfWork { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public string Terms { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        
        public List<OrderLineDto> Lines { get; set; } = new();
        public byte[]? RowVersion { get; set; }

        // Calculated helper for convenience
        public decimal TotalAmount { get; set; }
    }

    public class OrderLineDto
    {
        public Guid Id { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double QuantityOrdered { get; set; }
        public double QuantityReceived { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal VatAmount { get; set; }
        public decimal LineTotal { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class RestockCandidateDto
    {
        public InventoryItem Item { get; set; } = new();
        public double QuantityOnOrder { get; set; }
        public Branch TargetBranch { get; set; }

        public double TargetReorderPoint => TargetBranch == Branch.JHB 
            ? Item.JhbReorderPoint 
            : Item.CptReorderPoint;

        public double Gap => Math.Max(0, TargetReorderPoint - (
            (TargetBranch == Branch.JHB ? Item.JhbQuantity : Item.CptQuantity) 
            + QuantityOnOrder));

        public double QuantityOnHandForBranch => TargetBranch == Branch.JHB 
            ? Item.JhbQuantity 
            : Item.CptQuantity;
    }
}
