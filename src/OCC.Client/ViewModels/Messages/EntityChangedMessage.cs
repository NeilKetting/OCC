using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public enum EntityChangeType
    {
        Created,
        Updated,
        Deleted
    }

    public class EntityChangedMessage<T> : ValueChangedMessage<T>
    {
        public EntityChangeType ChangeType { get; }

        public EntityChangedMessage(T entity, EntityChangeType changeType) : base(entity)
        {
            ChangeType = changeType;
        }
    }
}
