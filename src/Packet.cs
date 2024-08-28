namespace Medoz.Mdns;

public record Packet(
        Header Header, 
        IEnumerable<Question> Questions, 
        IEnumerable<ResourceRecord> Answers
        );