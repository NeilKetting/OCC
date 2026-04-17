using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.Shared.Models;
using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message broadcast when an inventory item's stock level or details are updated.
    /// Used to decouple InventoryHub from PickingHub.
    /// </summary>
    public class StockUpdatedMessage : ValueChangedMessage<InventoryItem>
    {
        public StockUpdatedMessage(InventoryItem value) : base(value)
        {
        }
    }
}
