namespace RepPortal.Models;

public class ChatRequest
{
    public string Model { get; set; } = "gpt-4";
    public List<Message> Messages { get; set; } = new();
}

public class Message
{
    public string Role { get; set; } // "system", "user", or "assistant"
    public string Content { get; set; }
}
