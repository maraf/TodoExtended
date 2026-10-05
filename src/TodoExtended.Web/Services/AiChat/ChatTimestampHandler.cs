using Microsoft.AspNetCore.Components.AI;
using Microsoft.Extensions.AI;

namespace TodoExtended.Web.Services.AiChat;

public sealed class ChatTimestampHandler(BrowserConversationThread thread) : ContentBlockHandler<object>
{
    public override BlockMappingResult<object> Handle(BlockMappingContext context, object state)
    {
        foreach (var content in context.UnhandledContents)
        {
            if (content is not TextContent)
                continue;
            thread.RecordTimestamp(context.Update);
            break;
        }
        // Leave text rendering and streaming coalescence to the framework's handlers.
        return BlockMappingResult<object>.Pass();
    }
}
