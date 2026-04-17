using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class EntityUpdatedMessage : ValueChangedMessage<(string EntityType, string Action, Guid Id)>
    {
        public EntityUpdatedMessage(string entityType, string action, Guid id) : base((entityType, action, id))
        {
        }
    }
}
