using OCC.Shared.Models;
using System.Collections.Generic;

namespace OCC.Client.Services.Infrastructure
{
    public class OrderStateService
    {
        public Order? SavedOrder { get; private set; }
        public OrderLine? PendingLine { get; private set; }
        
        public bool HasSavedState => SavedOrder != null;
        
        // Validation check flags to know if we are returning from a specific action
        public bool IsReturningFromItemCreation { get; private set; }
        public string? PendingSearchTerm { get; private set; } // The term they searched for

        public virtual void SaveState(Order order, OrderLine? pendingLine, string? searchTerm = null)
        {
            SavedOrder = order;
            PendingLine = pendingLine;
            PendingSearchTerm = searchTerm;
            IsReturningFromItemCreation = true;
        }

        public virtual void ClearState()
        {
            SavedOrder = null;
            PendingLine = null;
            PendingSearchTerm = null;
            IsReturningFromItemCreation = false;
        }

        public virtual (Order? Order, OrderLine? Line, string? Term) RetrieveState()
        {
            var s = SavedOrder;
            var p = PendingLine;
            var t = PendingSearchTerm;
            ClearState();
            return (s, p, t);
        }
    }
}
