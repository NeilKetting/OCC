using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public record NoticeBoardUpdatedMessage(Guid NoticeId, string Action);
}
