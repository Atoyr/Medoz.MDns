namespace Medoz.Mdns;

public record Response(
        int Id, 
        Header Header, 
        int QuestionCount, 
        int AnswerCount, 
        int Offset, 
        // List<Question> Questions, 
        IEnumerable<Answer> Answers
        );