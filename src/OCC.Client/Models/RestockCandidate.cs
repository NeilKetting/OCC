using System;
using OCC.Shared.Models;

namespace OCC.Client.Models
{
    public class RestockCandidate
    {
        public InventoryItem Item { get; set; } = new();
        public double QuantityOnOrder { get; set; } // Approved/Ordered POs
        public Branch TargetBranch { get; set; }

        public double TargetReorderPoint => TargetBranch == Branch.JHB 
            ? Item.JhbReorderPoint 
            : Item.CptReorderPoint;

        public double Gap => Math.Max(0, TargetReorderPoint - (
            (TargetBranch == Branch.JHB ? Item.JhbQuantity : Item.CptQuantity) 
            + QuantityOnOrder));
    }
}
