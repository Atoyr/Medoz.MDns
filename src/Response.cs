namespace Medoz.Mdns;

public record Response(
        int Id, 
        int Flags, 
        int QuestionCount, 
        int AnswerCount, 
        int Offset, 
        // List<Question> Questions, 
        IEnumerable<Answer> Answers
        );