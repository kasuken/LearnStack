namespace LearnStack.Data.Models;

public class ContentIdeaResource
{
    public int ContentIdeaId { get; set; }
    public ContentIdea? ContentIdea { get; set; }

    public int LearningResourceId { get; set; }
    public LearningResource? LearningResource { get; set; }
}

