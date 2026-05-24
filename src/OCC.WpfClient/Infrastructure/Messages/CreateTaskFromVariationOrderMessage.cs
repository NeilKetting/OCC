using System;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public record CreateTaskFromVariationOrderMessage(ProjectVariationOrder VariationOrder);
}
